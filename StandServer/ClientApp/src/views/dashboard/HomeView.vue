<template>
	<loader :show="!loaded" />

	<div class="" v-if="loaded">

		<div class="summary">
			<div class="header">
				<div style="flex: 1;" />
				<h4>Образцы</h4>
				<p class="">(Обновлено: {{ lastMeasurementTime ? secondsToDateTime(lastMeasurementTime) : '-' }})</p>
			</div>

			<div class="sample-info mt-8" v-for="(measurement, sampleId) in lastSampleMeasurements"
				 :class="[measurement.state, { alarm: !isSampleOk(measurement) }]">
				<p class="id" @click="monitorSampleId = monitorSampleId === sampleId ? 0 : sampleId">{{ sampleIdFormat(sampleId) }}</p>
				<p class="state">Состояние: {{ measurement.state }}</p>
				<p class="t">t: {{ measurement.t }}</p>
				<router-link class="open-link" :to="{ name: 'sample', params: { id: sampleId }}">Просмотр истории</router-link>
				<p class="work-time" style="grid-row: span 1;">Время работы: {{ secondsToInterval(measurement.seconds_from_start) }}</p>
				<p class="i">I: {{ measurement.i }}</p>

				<p v-if="!isSampleOk(measurement)"
				   class="alarm-msg">Высокий ток в выключенном состоянии</p>
			</div>
		</div>

		<div class="sample-preview" v-if="monitorSampleId > 0">
			<Pass :measurements="lastMeasurements[monitorSampleId]" v-slot="{ measurements }">
				<div class="header">
					<h3 >{{ sampleIdFormat(monitorSampleId) }}</h3>
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
						<tr v-for="measurement in new ReverseIterable(measurements)"
							:class="[measurement.state, { alarm: !isSampleOk(measurement) }]">
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
							<td>{{ measurement.state.toUpperCase() }}</td>
						</tr>
					</table>
				</div>
			</Pass>
		</div>

	</div>
</template>

<script setup>
import { computed, inject, onMounted, onUnmounted, ref, watch } from 'vue';
import { useStore } from 'vuex'

import MeasurementsChart from '@/components/MeasurementsRealtimeChart'
import Loader from "@/components/Loader";

import Pass from "@/components/Pass";
import { objectMap, isSampleOk } from "@/utils/utils";
import { ReverseIterable } from "@/utils/arrayUtils";
import { sampleIdFormat } from "@/utils/stringUtils";
import { secondsToInterval, secondsToDateTime } from "@/utils/timeUtils";

/*let {
	signalRConnection,	onNewMeasurementsCallbacks
} = inject('signalr');*/

const store = useStore();

const sampleIds = computed(() => store.state.dashboard.sampleIds);
const lastMeasurements = computed(() => store.state.dashboard.lastMeasurements);
const lastSampleMeasurements = computed(() => objectMap(store.state.dashboard.lastMeasurements, v => v.at(-1)));
const lastMeasurementTime = computed(() => store.getters.lastMeasurementTime);

const loaded = computed(() => store.state.dashboard.lastMeasurementsInitialized);

const monitorSampleId = ref(0);

onMounted(async () => {
	if (store.state.dashboard.homeViewVisited) return;
	store.commit("setHomeViewVisited", true);

	// onNewMeasurementsCallbacks.push((measurements) => {	});
});

</script>

<style>
.summary > .header {
	display: flex;
}

.summary > .header > h4 {
	margin: 0;
	font-weight: 400;
	font-size: 1.3rem;
	text-align: center;
	flex: 1;
}
.summary > .header > p {
	margin: 0;
	text-align: right;
	flex: 1;
}

.summary > .sample-info {
	display: grid;
	border: solid 1px #aaa;
	border-radius: 6px;
	margin: 8px 0 0 0;
	padding: 8px;
	grid-template-columns: auto auto auto;
	/*grid-template-rows: auto auto;*/
	grid-column-gap: 30px;
	grid-row-gap: 4px;
}

.summary > .sample-info > p {
	margin: 0;
}

.summary > .sample-info > .id {
	font-weight: 500;
	margin-left: 4px;

}

.summary > .sample-info > .id:hover {
	color: #666;
	cursor: pointer;
}

.summary > .sample-info.off { background-color: rgba(0, 0, 0, 0.04); }

.summary > .sample-info.work { background-color: rgba(0, 0, 255, 0.04); }

.summary > .sample-info.relax { background-color: rgba(0, 255, 0, 0.04); }

.summary > .sample-info.alarm { background-color: rgba(255, 0, 0, 0.1) !important; }

.summary > .sample-info > .open-link {
	text-decoration: none;
	border: solid 1px #aaa;
	border-radius: 4px;
	color: #000;
	padding: 0 4px 1px;
	margin: 0;
	justify-self: flex-start;
	background-color: rgba(0,0,0,.02);
}

.summary > .sample-info > .open-link:hover {
	border-color: #777;
	color: #000;
	background-color: rgba(0,0,0,.07);
}

.summary > .sample-info > .alarm-msg {
	color: #f00;
	grid-column: span 3;
}

.sample-preview {
	margin-top: 16px;
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

.measurements-table tr.off { background-color: rgba(0, 0, 0, 0.03); }

.measurements-table tr.work { background-color: rgba(0, 0, 255, 0.03); }

.measurements-table tr.relax { background-color: rgba(0, 255, 0, 0.03); }

.measurements-table tr.alarm { background-color: rgba(255, 0, 0, 0.08) !important; }

/* settings modal */

.show-home-settings-btn-wrapper {
	margin: 0 0 10px 0;
	text-align: right;
}

.sample-ids-select > p {
	margin: 0;
}
</style>
