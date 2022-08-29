<template>
	<div>
		<h2 class="header-sample-id">{{ sampleIdFormat(props.id) }}</h2>

		<div class="measurements-control-elements">
			<input ref="datepickerEl" />
			<div>
				<div>
					<!--<button class="all-period-btn" @click="getAndSetPeriod">Всё время</button>-->
					<button class="select-period-btn" @click="showSelectPeriodModal = true">Выбрать из истории
					</button>
				</div>
				<div class="mt-8">
					<button class="load-btn" @click="load" :disabled="isLoading">Загрузить</button>
					<button class="load-csv-btn ms-6" @click="loadCsv">Скачать (csv)</button>
					<button class="load-csv-btn ms-6" @click="showRemoveSampleModal = true">Удалить образец</button>
				</div>
			</div>
		</div>

		<vue-final-modal v-model="showSelectPeriodModal" classes="modal-container" content-class="modal-content">
			<button class="modal-close" @click="showSelectPeriodModal = false">
				<mdi-close />
			</button>
			<span class="modal-title">История работы</span>
			<div class="modal__content">
				<div class="state-history-records">
					<Pass v-for="record in stateHistory"
						  :from="secondsToDateTime(record.from).split(' ')"
						  :to="record.to ? secondsToDateTime(record.to).split(' ') : null"
						  v-slot="{ from, to }">
						<Pass
							:fromDate="from[0]" :fromTime="from[1]"
							:toDate="to?.[0]" :toTime="to?.[1]"
							v-slot="{ fromDate, fromTime, toDate, toTime }">
							<p @click="selectPeriod(record)">
								<span>{{ fromDate }}</span> <span class="time">{{ fromTime }}</span> -
								<template v-if="to">
									<span>{{ toDate }}</span> <span class="time">{{ toTime }}</span>
								</template>
								<span v-else> * </span>
							</p>
						</Pass>
					</Pass>
				</div>
			</div>
		</vue-final-modal>

		<vue-final-modal v-model="showRemoveSampleModal" classes="modal-container" content-class="modal-content">
			<button class="modal-close" @click="showSelectPeriodModal = false">
				<mdi-close />
			</button>
			<span class="modal-title">Удаление образца</span>
			<div class="modal__action">
				<button @click="removeSample" :disabled="sampleRemoving">Удалить</button>
				<button @click="showRemoveSampleModal = false">Отмена</button>
			</div>
		</vue-final-modal>

		<label class="cb">
			<input type="checkbox" v-model="showOffStateRecords">
			<span>Показывать измерения в выключенном состоянии</span>
		</label>

		<div class="sample-history-graphs mt-8">
			<div>
				<measurements-chart :data="data" title="Температура" x-axis="time" y-axis="t"
									:show-off-state-records="showOffStateRecords" />
			</div>
			<div>
				<measurements-chart :data="data" title="Ток" x-axis="time" y-axis="i"
									:show-off-state-records="showOffStateRecords" />
			</div>
		</div>

		<div class="mt-16">
			<table class="measurements-table">
				<tr>
					<th>Дата и время</th>
					<th>Время с начала работы</th>
					<th class="nowrap">S, %</th>
					<th class="nowrap">t, *C</th>
					<th class="nowrap">tu, *C</th>
					<th class="nowrap">I, mA</th>
					<th>Период, us</th>
					<th>Работа, min</th>
					<th>Отдых, min</th>
					<th>Частота, GHz</th>
					<th>Состояние</th>
				</tr>
				<tr v-for="measurement in data" :class="{ 'dark-4': !measurement.state }">
					<template v-if="measurement.state || showOffStateRecords">
						<td>{{ secondsToDateTime(measurement.time) }}</td>
						<td>{{ secondsToInterval(measurement.seconds_from_start) }}</td>
						<td>{{ measurement.duty_cycle }}</td>
						<td>{{ measurement.t }}</td>
						<td>{{ measurement.tu }}</td>
						<td>{{ measurement.i }}</td>
						<td>{{ measurement.period }}</td>
						<td>{{ measurement.work }}</td>
						<td>{{ measurement.relax }}</td>
						<td>{{ measurement.frequency }}</td>
						<td>{{ measurement.state ? "ON" : "OFF" }}</td>
					</template>
				</tr>
			</table>
		</div>
	</div>
</template>

<script setup>
import { ref, computed, watch, shallowRef, onMounted } from 'vue';
import { useStore } from 'vuex'
import { useRouter } from 'vue-router';

import { easepick } from '@easepick/core';
import { RangePlugin } from '@easepick/range-plugin';
import { LockPlugin } from "@easepick/lock-plugin";

import { VueFinalModal } from 'vue-final-modal'
import MdiClose from '@/components/MdiClose.vue'

import MeasurementsChart from '@/components/MeasurementHistoryChart.vue'

import Pass from "@/components/Pass";
import { call_get, call_delete, downloadFile } from '@/utils/api';
import { sampleIdFormat } from "@/utils/stringUtils";
import { secondsToInterval, secondsToDateTime, floorToDay, sleep } from "@/utils/timeUtils";
import iziToast from "izitoast";
import { RequestError } from "@/exceptions";

const store = useStore();
const router = useRouter();

const props = defineProps({
	id: Number
});

const stateHistory = computed(() => {
	if (!samplePeriod.value) return store.state.dashboard.stateHistory;
	let from = floorToDay(new Date(samplePeriod.value.from)).getTime();
	let to = floorToDay(new Date(samplePeriod.value.to)).addDays(1).getTime();
	return store.state.dashboard.stateHistory.filter(r => r.from > from && r.to < to);
});

const data = shallowRef([]);
const showOffStateRecords = ref(false);

// Period

let samplePeriod = ref(null);

const getSamplePeriod = async () => samplePeriod.value ??= await loadSamplePeriod();
const loadSamplePeriod = async () => await call_get(`/api/samples/${ props.id }/period`);

function setPeriod({ from, to }) {
	datepicker.setStartDate(floorToDay(new Date(from)));
	datepicker.setEndDate(floorToDay(new Date(to)));
}

const getAndSetPeriod = async () => setPeriod(await getSamplePeriod());

// Period selecting via datepicker

const datepickerEl = ref(null);
let datepicker = null;

// Period selecting via state history modal

const showSelectPeriodModal = ref(false);

function selectPeriod(record) {
	setPeriod({ from: record.from, to: record.to });
	showSelectPeriodModal.value = false;
}

// load data

const isLoading = ref(false);

async function load() {
	isLoading.value = true;
	data.value = await call_get(`/api/samples/${ props.id }`, {
		from: datepicker.getStartDate().getTime(),
		to: datepicker.getEndDate().getTime() + (24 * 60 * 60 * 1000 - 1)
	});
	console.log(data.value);
	isLoading.value = false;
}

function loadCsv() {
	downloadFile(`/api/samples/${ props.id }/csv`, {
		from: datepicker.getStartDate().getTime(),
		to: datepicker.getEndDate().getTime() + (24 * 60 * 60 * 1000 - 1)
	});
}

// Sample remove

const showRemoveSampleModal = ref(false);
const sampleRemoving = ref(false);

async function removeSample() {
	sampleRemoving.value = true;
	await call_delete(`/api/samples/${ props.id }`);
	sampleRemoving.value = false;
	showRemoveSampleModal.value = false;
	store.commit("removeSample", props.id);
	await sleep(300); // без этого будет ошибка - enableBodyScroll unsuccessful - targetElement must be provided when calling enableBodyScroll on IOS devices.
	await router.push('/')
}

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
	console.log('props.id changed', store.state.dashboard.sampleIds, props.id);
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
	display: flex;
	flex-wrap: wrap;
}

.measurements-control-elements > input {
	align-self: flex-start;
	margin: 0 8px 8px 0;
}

.measurements-control-elements > div {
	flex-grow: 1;
	display: flex;
	flex-direction: column;
}

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

/* state history modal */

.state-history-records > p {
	margin: 0 0 6px 0;
	padding: 4px 10px;
	cursor: pointer;
}

.state-history-records > p:hover {
	background-color: rgba(0, 0, 0, .1);
}

.state-history-records > p > .time {
	color: #888;
}

</style>
