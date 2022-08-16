<template>
	<loader :show="!loaded" />

	<vue-final-modal v-model="showSettingsModal" classes="modal-container" content-class="modal-content">
		<button class="modal-close" @click="showSettingsModal = false">
			<mdi-close />
		</button>
		<span class="modal-title">Отображаемые образцы</span>
		<div class="modal__content">
			<div class="sample-ids-select">
				<p><label class="cb">
					<input type="checkbox" @change="selectAllSampleIds($event)"
						   :checked="previewSampleIdsTemp.length === sampleIds.length">
					<span>Выбрать все</span>
				</label></p>
				<p v-for="sampleId in sampleIds">
					<label class="cb">
						<input type="checkbox" :value="sampleId" v-model="previewSampleIdsTemp">
						<span>{{ sampleIdFormat(sampleId) }}</span>
					</label>
				</p>
			</div>
		</div>
		<div class="modal__action">
			<button @click="saveSettings">Сохранить</button>
			<button @click="showSettingsModal = false">Отмена</button>
		</div>
	</vue-final-modal>

	<div class="" v-if="loaded">
		<div class="show-home-settings-btn-wrapper">
			<button @click="showSettingsModal = true">Настройки</button>
		</div>

		<div class="sample-preview" v-for="sampleId in previewSampleIds">
			<Pass :measurements="lastMeasurements[sampleId]" v-slot="{ measurements }">
				<div class="header">
					<h3>{{ sampleIdFormat(sampleId) }}</h3>
					<p>{{ measurements.at(-1).t }} | {{ measurements.at(-1).i }}</p>
				</div>
				<div class="charts">
					<div>
						<measurements-chart :data="measurements" title="Температура" x-axis="time" y-axis="t" />
					</div>
					<div>
						<measurements-chart :data="measurements" title="Ток" x-axis="time" y-axis="i" />
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
						<tr v-for="measurement in measurements.slice().reverse()"
							:class="{ 'dark-4': !measurement.state }">
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
						</tr>
					</table>
				</div>
			</Pass>
		</div>

	</div>
</template>

<script setup>
import { computed, inject, onMounted, onUnmounted, ref } from 'vue';
import { useStore } from 'vuex'

import { VueFinalModal } from 'vue-final-modal'
import MdiClose from '@/components/MdiClose.vue'

import MeasurementsChart from '@/components/MeasurementsRealtimeChart'
import Loader from "@/components/Loader";

import Pass from "@/components/Pass";
import { sampleIdFormat } from "@/utils/stringUtils";
import { secondsToInterval, secondsToDateTime } from "@/utils/timeUtils";

let { signalRConnection, signalROnReconnectActions } = inject('signalr');

const store = useStore();

const sampleIds = computed(() => store.state.dashboard.sampleIds);
const lastMeasurements = computed(() => store.state.dashboard.lastMeasurements);

const loaded = ref(store.state.dashboard.homeViewVisited || false);
const showSettingsModal = ref(false);

const previewSampleIds = ref([]);
const previewSampleIdsTemp = ref([]);

function selectAllSampleIds(event) {
	if (event.target.checked)
		previewSampleIdsTemp.value = sampleIds.value.slice();
	else
		previewSampleIdsTemp.value.length = 0;
}

function saveSettings() {
	showSettingsModal.value = false;
	previewSampleIds.value = previewSampleIdsTemp.value;
	localStorage.setItem("Home.Preview.SampleIds", JSON.stringify(previewSampleIds.value));
}

function loadSettings() {
	let previewSampleIdsLS = localStorage.getItem("Home.Preview.SampleIds");
	try {
		previewSampleIdsLS = JSON.parse(previewSampleIdsLS);
	} catch (e) {
		console.error('Failed to parse settings');
		previewSampleIdsLS = null;
	}
	if (previewSampleIdsLS && !(previewSampleIdsLS instanceof Array)) previewSampleIdsLS = null;
	if (previewSampleIdsLS && !(previewSampleIdsLS.every(e => Number.isInteger(e)))) previewSampleIdsLS = null;
	if (previewSampleIdsLS) {
		previewSampleIdsLS = previewSampleIdsLS.filter(e =>  sampleIds.value.includes(e));
		if (previewSampleIdsLS.length === 0) previewSampleIdsLS = null;
	}

	previewSampleIds.value = previewSampleIdsLS ?? sampleIds.value.slice();
	if (!previewSampleIdsLS)
		localStorage.setItem("Home.Preview.SampleIds", JSON.stringify(previewSampleIds.value));

	previewSampleIdsTemp.value = previewSampleIds.value;
}

onMounted(async () => {
	loadSettings();

	if (store.state.dashboard.homeViewVisited) return;
	store.commit("setHomeViewVisited", true);

	await store.dispatch('loadLastMeasurements');
	loaded.value = true;

	signalRConnection.value.on("NewMeasurement", (sampleId, measurement) => {
		store.commit("newMeasurement", { sampleId, measurement });
	});

	const subscribeFunc = () => signalRConnection.value.send("SubscribeToMeasurements");
	await subscribeFunc();
	signalROnReconnectActions.push(subscribeFunc);
});

</script>

<style>
.sample-preview {
	padding: 8px;
	border: 1px solid #999;
	border-radius: 10px;
}

.sample-preview:not(:last-child) {
	margin-bottom: 12px;
}

.sample-preview > .header {
	display: flex;
}

.sample-preview > .header > h3 {
	font-size: calc(1.5rem + 0.25vw);
	font-weight: 300;
	line-height: 1.2;
	margin: 0;
}

.sample-preview > .header > p {
	align-self: center;
	flex-grow: 1;
	margin: 0;
	text-align: right;
	font-weight: 400;
	font-size: 1.2rem;
}

.sample-preview > .charts {
	display: grid;
	grid-template-columns: repeat(2, 1fr);
	overflow: hidden;
	margin-top: 8px;
	column-gap: 8px;
}

.sample-preview > .charts > div {
	padding: 2px;
	border: 1px solid #bbb;
	min-width: 0; /* https://css-tricks.com/flexbox-truncated-text/ */
}

/* measurements table */
.measurements-table {
	text-align: center;
	border-collapse: collapse;
	border: 0px solid #bbb;
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

/* settings modal */

.show-home-settings-btn-wrapper {
	margin: 0 0 10px 0;
	text-align: right;
}

.sample-ids-select > p {
	margin: 0;
}
</style>
