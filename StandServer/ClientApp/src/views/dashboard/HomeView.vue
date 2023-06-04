<template>
	<loader :show="!loaded" />

	<template v-if="loaded">

		<div class="summary">
			<div class="header">
				<p style="display: flex; align-items: center;"><span v-if="!isSamplesOk" style="color: red">есть проблемы</span></p>
				<h4>Образцы</h4>
				<p style="text-align: right">(Обновлено:
					{{ lastMeasurementTime ? millisToDateTime(lastMeasurementTime) : '-' }})</p>
			</div>

			<div class="sample-info mt-8" v-for="(measurement, sampleId) in lastSampleMeasurements"
				 :class="[measurement.state, { alarm: !isSampleOk(measurement) }]"
				 @click="sampleClick(sampleId)">
				<p class="id">{{ sampleIdFormat(sampleId) }}</p>
				<p class="state">Состояние: {{ measurement.state }}</p>
				<p class="t">t: {{ measurement.t }}</p>
				<router-link class="open-link" :to="{ name: 'sample', params: { id: sampleId }}">Просмотр истории</router-link>
				<p class="work-time" style="grid-row: span 1;">Время работы: {{ secondsToInterval(measurement.secondsFromStart) }}</p>
				<p class="i">I: {{ measurement.i }}</p>

				<p v-if="!isSampleOk(measurement)" class="alarm-msg">Высокий ток в выключенном состоянии</p>
			</div>
		</div>

		<div class="sample-preview" v-if="monitorSampleId > 0">
			<Pass :measurements="lastMeasurements[monitorSampleId]" v-slot="{ measurements }">
				<div class="header">
					<h3>{{ sampleIdFormat(monitorSampleId) }}</h3>
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

					<Pass :measurement="measurements.at(-1)" v-slot="{ measurement }">
						<fieldset class="sample-info show-1000">
							<legend>Скрытые данные из последнего измерения</legend>
							<p class="show-500">S, %: {{ measurement.dutyCycle }}</p>
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
							<th class="nowrap hide-500">S, %</th>
							<th class="nowrap">t, *C</th>
							<th class="nowrap hide-500">tu, *C</th>
							<th class="nowrap">I, mA</th>
							<th class="hide-900">Период, us</th>
							<th class="hide-1000">Работа, min</th>
							<th class="hide-1000">Отдых, min</th>
							<th class="hide-900">Частота, GHz</th>
							<th style="overflow-wrap: anywhere;">Состояние</th>
						</tr>
						<tr v-for="measurement in reverseIterate(measurements)"
							:class="[measurement.state, { alarm: !isSampleOk(measurement) }]">
							<td>{{ millisToDateTime(measurement.time) }}</td>
							<td>{{ secondsToInterval(measurement.secondsFromStart) }}</td>
							<td class="hide-500">{{ measurement.dutyCycle }}</td>
							<td>{{ measurement.t }}</td>
							<td class="hide-500">{{ measurement.tu }}</td>
							<td>{{ measurement.i }}</td>
							<td class="hide-900">{{ measurement.period }}</td>
							<td class="hide-1000">{{ measurement.work }}</td>
							<td class="hide-1000">{{ measurement.relax }}</td>
							<td class="hide-900">{{ measurement.frequency }}</td>
							<td>{{ measurement.state.toUpperCase() }}</td>
						</tr>
					</table>
				</div>
			</Pass>
		</div>

	</template>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue';
import { useStore } from 'vuex'

import MeasurementsChart from '@/components/MeasurementsRealtimeChart'
import Loader from "@/components/Loader";

import Pass from "@/components/Pass";
import { objectMap, isSampleOk } from "@/utils/utils";
import { reverseIterate } from "@/utils/arrayUtils";
import { sampleIdFormat } from "@/utils/stringUtils";
import { secondsToInterval, millisToDateTime } from "@/utils/timeUtils";

const store = useStore();

const sampleIds = computed(() => store.state.dashboard.sampleIds);
const lastMeasurements = computed(() => store.state.dashboard.lastMeasurements);
const lastSampleMeasurements = computed(() => objectMap(store.state.dashboard.lastMeasurements, v => v.at(-1)));
const lastMeasurementTime = computed(() => store.getters.lastMeasurementTime);
const isSamplesOk = computed(() => {
	for (let sample in lastSampleMeasurements.value)
		if (!isSampleOk(lastSampleMeasurements.value[sample])) return false;
	return true;
});

const loaded = computed(() => store.state.dashboard.lastMeasurementsInitialized);

const monitorSampleId = ref(0);

function sampleClick(sampleId) {
	if (document.getSelection().isCollapsed)
		monitorSampleId.value = monitorSampleId.value === sampleId ? 0 : sampleId;
}

onMounted(async () => {
	if (store.state.dashboard.homeViewVisited) return;
	store.commit("setHomeViewVisited", true);
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
	flex: 1;
}

.summary > .sample-info {
	display: grid;
	border: solid 1px #aaa;
	border-radius: 6px;
	margin: 8px 0 0 0;
	padding: 8px;
	grid-template-columns: auto auto auto;
	grid-column-gap: 20px;
	grid-row-gap: 4px;
}

.sample-info {
	border: solid 1px #888;
}

.sample-info > p {
	margin: 0;
}

.summary > .sample-info:hover {
	cursor: pointer;
}

.summary > .sample-info > .id {
	font-weight: 500;
	margin-left: 4px;
}

.summary > .sample-info.off { background-color: rgba(0, 0, 0, 0.04); }

.summary > .sample-info.work { background-color: rgba(0, 0, 255, 0.04); }

.summary > .sample-info.relax { background-color: rgba(0, 255, 0, 0.04); }

.summary > .sample-info.alarm { background-color: rgba(255, 0, 0, 0.1) !important; }

.summary > .sample-info .open-link {
	text-decoration: none;
	border: solid 1px #aaa;
	border-radius: 4px;
	color: #000;
	padding: 0 4px 1px;
	margin: 0;
	justify-self: flex-start;
	align-self: start;
	background-color: rgba(0, 0, 0, .02);
	/* TODO: Fit block width to content when text wraps. (It seems impossible) */
}

.summary > .sample-info > .open-link:hover {
	border-color: #777;
	color: #000;
	background-color: rgba(0, 0, 0, .07);
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
	grid-template-rows: repeat(2, 1fr);
	overflow: hidden;
	column-gap: 8px;
	grid-row-gap: 8px;
}

@media (min-width: 1000px) {
	.sample-preview > .charts {
		grid-template-columns: repeat(2, 1fr);
		grid-template-rows: repeat(1, 1fr);
	}
}

.sample-preview > .charts > div {
	padding: 2px;
	border: 1px solid #bbb;
	min-width: 0; /* https://css-tricks.com/flexbox-truncated-text/ */
}

</style>
