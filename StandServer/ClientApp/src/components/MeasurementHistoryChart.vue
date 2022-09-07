<template>
	<canvas ref="chartEl"></canvas>
</template>

<script setup>
import { ref, toRaw, onMounted, computed, watch } from 'vue';
import { Chart, registerables } from 'chart.js';
import 'chartjs-adapter-luxon';
import zoomPlugin from 'chartjs-plugin-zoom';
import { decimation} from '@/utils/decimation'

Chart.register(...registerables);
Chart.register(zoomPlugin);

const props = defineProps({
	data: {
		type: Array,
		default: () => []
	},
	suggestedMax: Number,
	suggestedMin: Number,
	title: String,
	xAxis: String,
	yAxis: String,
	showOffStateRecords: Boolean
});

const data = computed(() => props.showOffStateRecords ? props.data
	: props.data.map(m => m.state !== 'off' ? m : ({ [props.xAxis]: m[props.xAxis] }))
);

const chartProps = computed(() => (
	props.suggestedMin, props.suggestedMax,
		props.title,
		Date.now()))

const chartEl = ref(null);
let chart = null;

const dataCount = ref(0),
	dataSegmentCount = ref(0),
	dataVisibleCount = ref(0);


function fetchData(data, x1, x2) {
	//console.log('dt', data, '\noff - ', props.showOffStateRecords)
	let start = 0, end;
	while (start < data.length && data[start][props.xAxis] < x1) start++;
	end = start;
	while (end < data.length && data[end][props.xAxis] <= x2) end++;
	end--;
	let count = end - start + 1;
	console.log('data length: ', data.length, ', start: ', start, ', end: ', end);

	let dec = decimation({
		data: data,
		xAxis: props.xAxis,
		yAxis: props.yAxis,
		start: start === 0 ? start : start - 1,
		count: end === data.length - 1 ? count : (start === 0 ? 0 : 1) + count + 1,
		availableWidth: chart.width / 6,
		skipIf: props.showOffStateRecords ? m => m.state === 'off' : null
	});

	dataCount.value = data.length;
	dataSegmentCount.value = count;
	dataVisibleCount.value = dec.length;

	return dec;
}

let timer;

const getDisplayDataInfo = () => `${ dataVisibleCount.value }/${ dataSegmentCount.value }/${ dataCount.value }`;

function startFetch({ chart }) {
	const { min, max } = chart.scales.x;
	clearTimeout(timer);
	timer = setTimeout(() => {
		console.log('Fetched data between ' + min + ' and ' + max);
		chart.data.datasets[0].data = fetchData(data.value, min, max);
		chart.options.plugins.subtitle.text = getDisplayDataInfo();
		chart.stop(); // make sure animations are not running
		chart.update('none');
	}, 500);
}

onMounted(() => {
	chart = new Chart(chartEl.value, {
		type: 'line',
		data: {
			datasets: [ {
				clip: 1000,
				backgroundColor: 'rgb(255, 99, 132)',
				borderColor: 'rgb(255, 99, 132)',
				//data: toRaw(data),
				label: props.title,
				parsing: {
					xAxisKey: props.xAxis,
					yAxisKey: props.yAxis
				},
				segment: {
					borderColor: (ctx, value) => {
						//console.dir(ctx);
						return ctx.p0.raw.state === 'off' ? 'rgb(0,0,0,0.2)' : undefined
					}
				},
			} ]
		},
		options: {
			//animation: false,
			parsing: false,
			interaction: {
				intersect: false
			},
			elements: {
				point: {
					radius: 1,
				},
				line: {
					borderWidth: 1
				},
			},
			scales: {
				x: {
					type: 'time',
					time: {
						displayFormats: {
							'second': 'HH:mm:ss',
							'minute': 'HH:mm',
							'hour': 'HH:mm',
						},
					},
					ticks: {
						autoSkip: true,
						autoSkipPadding: 30,
						maxRotation: 0
					}
				},
				y: {
					suggestedMax: props.suggestedMax,
					suggestedMin: props.suggestedMin
				}
			},
			plugins: {
				legend: {
					display: true,
				},
				tooltip: {
					callbacks: {
						label: function (context) {
							return [
								` t: ${ context.raw.t }`,
								` i: ${ context.raw.i }`,
								` S: ${ context.raw.duty_cycle }`,
							];
						}
					},
					displayColors: false
				},
				zoom: {
					limits: {
						x: {
							min: 'original',
							max: 'original',
							minRange: 60 * 1000
						},
					},
					pan: {
						enabled: true,
						mode: 'x',
						onPanComplete: startFetch
					},
					zoom: {
						wheel: {
							enabled: true,
						},
						mode: 'x',
						onZoomComplete: startFetch
					}
				},
				subtitle: {
					display: true,
					text: '0/0/0',
					align: 'end',
				}
			},
			transitions: {
				zoom: {
					animation: {
						duration: 100
					}
				}
			}
		}
	});

	setData(data.value);

	watch(() => chartProps, () => {
		chart.options.scales.y.suggestedMax = props.suggestedMax;
		chart.options.scales.y.suggestedMin = props.suggestedMin;
		chart.update();
		console.log('chart props updated', toRaw(props));
	}, { immediate: true, deep: true });
});


function setData(data, saveZoom = false) {
	//console.log('chart updating by new array', data);
	if (!data) {
		chart.data.datasets[0].data = [];
		chart.stop();
		chart.update('none');
		return;
	}

	let min, max;
	const { min: zoomMin, max: zoomMax } = chart.scales.x;

	min = data[0]?.[props.xAxis];
	max = data.at(-1)?.[props.xAxis];

	console.log('min: ', min, ' max: ', max)

	if (min)
		chart.options.scales.x.min = min;
	if (max && max !== min)
		chart.options.scales.x.max = max;

	if (saveZoom)
		[ min, max ] = [ zoomMin, zoomMax ];

	chart.data.datasets[0].data = data.length > 1 ? fetchData(data, min, max) : data;
	if (data?.length <= 1) {
		dataCount.value = dataSegmentCount.value = dataVisibleCount.value = data?.length ?? 0;
	}
	chart.options.plugins.subtitle.text = getDisplayDataInfo();

	chart.stop();
	chart.update('none');
	console.log('chart updated by new array');

	if (saveZoom)
		chart.zoomScale('x', { min: zoomMin, max: zoomMax }, 'none');
}

watch(() => props.data, (d) => setData(data.value));
watch(() => props.showOffStateRecords, (d) => setData(data.value, true));

</script>

<style>

</style>
