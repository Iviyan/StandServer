<template>
	<div>
		<h2 class="header-sample-id">{{ sampleIdFormat(props.id) }}</h2>

		<div class="measurements-control-elements">
			<input ref="datepickerEl" />
			<button class="load-btn" @click="load" :disabled="isLoading">Загрузить</button>
			<button class="load-csv-btn" @click="loadCsv">Скачать (csv)</button>
			<button class="del-sample-btn"
					v-if="store.getters.isAdmin"
					@click="deleteSampleVfmModal.open()">Удалить образец</button>
		</div>

		<label class="cb mt-8" style="display: block;">
			<input type="checkbox" v-model="showOffStateRecords">
			<span>Показывать измерения в выключенном состоянии</span>
		</label>
		<label class="cb" style="display: block;">
			<input type="checkbox" v-model="reverseRecords">
			<span>Отображать сначала последние измерения</span>
		</label>

		<div class="sample-history-graphs mt-8">
			<div>
				<measurements-chart :data="data" title="Температура" x-axis="secondsFromStart" y-axis="t" />
			</div>
			<div>
				<measurements-chart :data="data" title="Ток" x-axis="secondsFromStart" y-axis="i" />
			</div>
		</div>

		<div class="mt-16">
			<Pass v-if="data.length > 0" :measurement="data.at(-1)" v-slot="{ measurement }">
				<fieldset class="sample-info show-1000">
					<legend>Скрытые данные из последнего измерения</legend>
					<p class="show-500">tu, *C: {{ measurement.t }}</p>
					<p class="show-900">Период, us: {{ measurement.period }}</p>
					<p class="show-1000">Работа, min: {{ measurement.work }}</p>
					<p class="show-1000">Отдых, min: {{ measurement.relax }}</p>
					<p class="show-900">Частота, GHz: {{ measurement.frequency }}</p>
				</fieldset>
			</Pass>

			<table class="measurements-table mt-16">
				<tr>
					<th>Дата и время</th>
					<th>Время с начала работы</th>
					<th class="nowrap">S, %</th>
					<th class="nowrap">t, *C</th>
					<th class="nowrap hide-500">tu, *C</th>
					<th class="nowrap">I, mA</th>
					<th class="hide-900">Период, us</th>
					<th class="hide-1000">Работа, min</th>
					<th class="hide-1000">Отдых, min</th>
					<th class="hide-900">Частота, GHz</th>
					<th style="overflow-wrap: anywhere;">Состояние</th>
				</tr>
				<tr v-for="measurement in reverseIterate(data, reverseRecords)" :class="[measurement.state, { alarm: !isSampleOk(measurement) }]">
					<template v-if="measurement.state !== 'off' || showOffStateRecords || !isSampleOk(measurement)">
						<td>{{ millisToDateTime(measurement.time) }}</td>
						<td>{{ secondsToInterval(measurement.secondsFromStart) }}</td>
						<td>{{ measurement.dutyCycle }}</td>
						<td>{{ measurement.t }}</td>
						<td class="hide-500">{{ measurement.tu }}</td>
						<td>{{ measurement.i }}</td>
						<td class="hide-900">{{ measurement.period }}</td>
						<td class="hide-1000">{{ measurement.work }}</td>
						<td class="hide-1000">{{ measurement.relax }}</td>
						<td class="hide-900">{{ measurement.frequency }}</td>
						<td>{{ measurement.state.toUpperCase() }}</td>
					</template>
				</tr>
			</table>
		</div>
	</div>
</template>

<script setup>
import { ref, watch, shallowRef, onMounted } from 'vue';
import { useStore } from 'vuex'
import { useRouter } from 'vue-router';

import { easepick } from '@easepick/core';
import { RangePlugin } from '@easepick/range-plugin';
import { LockPlugin } from "@easepick/lock-plugin";

import ConfirmModal from "@/components/ConfirmModal.vue";
import { useModal } from 'vue-final-modal'

import MeasurementsChart from '@/components/MeasurementsChart'

import Pass from "@/components/Pass";
import { callGet, callDelete, downloadFile, callPost } from '@/utils/api';
import { sampleIdFormat } from "@/utils/stringUtils";
import { secondsToInterval, millisToDateTime, floorToDay } from "@/utils/timeUtils";
import { sleep, isSampleOk, errorToText } from "@/utils/utils";
import iziToast from "izitoast";
import { RequestError } from "@/exceptions";
import { reverseIterate } from "@/utils/arrayUtils";
import ChangePasswordModal from "@/components/ChangePasswordModal.vue";

const store = useStore();
const router = useRouter();

const props = defineProps({
	id: Number
});

const data = shallowRef([]);
const showOffStateRecords = ref(localStorage.getItem("showOffStateRecords") ?? true);
const reverseRecords = ref(localStorage.getItem("reverseRecords") ?? false);

watch(showOffStateRecords, async b => localStorage.setItem("showOffStateRecords", b));
watch(reverseRecords, async b => localStorage.setItem("reverseRecords", b));

// Period

let samplePeriod = ref(null);

const getSamplePeriod = async () => samplePeriod.value ??= await loadSamplePeriod();
const loadSamplePeriod = async () => await callGet(`/api/samples/${ props.id }/period`);

function setPeriod({ from, to }) {
	datepicker.setStartDate(floorToDay(new Date(from)));
	datepicker.setEndDate(floorToDay(new Date(to)));
}

// Period selecting via datepicker

const datepickerEl = ref(null);
let datepicker = null;

// load data

const isLoading = ref(false);

async function load() {
	isLoading.value = true;
	data.value = await callGet(`/api/samples/${ props.id }`, {
		from: datepicker.getStartDate().getTime(),
		to: datepicker.getEndDate().getTime() + (24 * 60 * 60 * 1000 - 1)
	});
	console.debug(data.value);
	isLoading.value = false;
}

function loadCsv() {
	downloadFile(`/api/samples/${ props.id }/csv`, {
		from: datepicker.getStartDate().getTime(),
		to: datepicker.getEndDate().getTime() + (24 * 60 * 60 * 1000 - 1)
	});
}

// Sample remove

const deleteSampleVfmModal = useModal({
	component: ConfirmModal,
	attrs: {
		title: 'Удаление образца',
		actionName: 'Удалить',
		async onSubmit() {
			let attrs = deleteSampleVfmModal.options.attrs;
			try {
				attrs.inProgress = true;
				await callDelete(`/api/samples/${ props.id }`);
				attrs.inProgress = false;
				attrs.error = '';
				await deleteSampleVfmModal.close();

				store.commit("removeSample", props.id);
				await router.push('/');
			} catch (err) {
				attrs.inProgress = false;
				attrs.error = errorToText(err)
			}
		},
		onCancel() { deleteSampleVfmModal.close(); }
	},
})

// Datepicker (easepick) initialization

onMounted(async () => {
	datepicker = new easepick.create({
		element: datepickerEl.value,
		css: [
			'https://cdn.jsdelivr.net/npm/@easepick/core@1.2.0/dist/index.css',
			'https://cdn.jsdelivr.net/npm/@easepick/range-plugin@1.2.0/dist/index.css',
			'https://cdn.jsdelivr.net/npm/@easepick/lock-plugin@1.2.0/dist/index.css',
		],
		plugins: [ RangePlugin, LockPlugin ],
		RangePlugin: {
			startDate: floorToDay(new Date()), //new Date(2022, 0, 1, 0, 0, 0),
			endDate: floorToDay(new Date()) //new Date(2022, 7, 1, 0, 0, 0),
		},
		LockPlugin: {
			minDate: new Date(0),
			maxDate: new Date(0),
		},
		setup(picker) {
			picker.on('select', async (e) => { await load() });
		},
	});
})

watch(() => props.id, async id => {
	console.debug('props.id changed', store.state.dashboard.sampleIds, props.id);
	if (!store.state.dashboard.sampleIds.includes(id)) await router.push('/');

	document.title = "Образец " + sampleIdFormat(id);

	data.value = [];
	samplePeriod.value = null;

	try { // In case the sample is deleted, but the other client doesn't know about it yet.
		let period = await getSamplePeriod();

		datepicker.PluginManager.instances.LockPlugin.options.minDate.setTime(
			floorToDay(new Date(period.from)).getTime());
		datepicker.PluginManager.instances.LockPlugin.options.maxDate.setTime(
			floorToDay(new Date(period.to)).getTime());
		setPeriod(period);

		await load();
	} catch (error) {
		if (error instanceof RequestError && error.rfc7807) {
			iziToast.error({
				title: 'Ошибка открытия страницы образца. Возможно образца не существует.',
				message: 'Ошибка: ' + error.message,
				layout: 2,
				timeout: 5000,
				class: "iziToast-sample"
			});
			await router.push('/');
		} else {
			await router.push('/');
			throw error;
		}
	}

}, { immediate: true })

</script>

<style>

.header-sample-id {
	margin: 0 0 10px 0;
	text-align: center;
	font-weight: 500;
}

.measurements-control-elements {
	display: grid;
	grid-auto-flow: column;
	grid-template-columns: auto auto;
	grid-template-rows: auto auto;
	grid-column-gap: 8px;
	grid-row-gap: 8px;
	justify-content: start;
}

.measurements-control-elements > * {
	padding: 0 16px;
	text-align: center;
}

@media (max-width: 700px) {
	.measurements-control-elements {
		justify-content: normal;
		grid-template-columns: 1fr 1fr;
		grid-auto-flow: row;
	}

	.measurements-control-elements > input, .measurements-control-elements > .load-btn {
		grid-column: span 2;
	}

	.measurements-control-elements > * {
		padding: 6px 8px;
	}
}

/* */

.sample-info {
	border: solid 1px #888;
}

.sample-info > p {
	margin: 0;
}

/* */

.measurements-table {
	text-align: center;
	border-collapse: collapse;
	border: 0px solid grey;
	width: 100%;
}

.measurements-table th, .measurements-table td {
	border: 0.1px solid #aaa;
	padding: 4px;
}

.measurements-table th {
	font-weight: 500;
	padding: 4px 6px;
}

.measurements-table tr.off { background-color: rgba(0, 0, 0, 0.03); }

.measurements-table tr.work { background-color: rgba(0, 0, 255, 0.03); }

.measurements-table tr.relax { background-color: rgba(0, 255, 0, 0.03); }

.measurements-table tr.alarm { background-color: rgba(255, 0, 0, 0.08) !important; }

/* sample graphs */

.sample-history-graphs {
	display: grid;
	grid-template-rows: repeat(2, 1fr);
	grid-template-columns: repeat(1, 1fr);
	row-gap: 16px;
}

.sample-history-graphs > div {
	min-width: 0;
}

@media (min-width: 1200px) {
	.sample-history-graphs {
		grid-template-rows: repeat(1, 1fr);
		grid-template-columns: repeat(2, 1fr);
		column-gap: 12px;
	}
}

</style>
