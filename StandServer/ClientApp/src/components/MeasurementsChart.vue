<template>
	<canvas ref="chartEl"></canvas>
</template>

<script setup>
import { ref, toRaw, onMounted, computed, watch } from 'vue';
import { Chart, registerables } from 'chart.js';
import 'chartjs-adapter-luxon';
import zoomPlugin from 'chartjs-plugin-zoom';
import { secondsToInterval, millisToDateTime } from '@/utils/timeUtils'
import { decimation } from '@/utils/decimation'

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
});

const data = computed(() => props.data.filter(m => m.state !== 'off'));

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
	//console.log('dt', data)
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
					type: 'linear',
					ticks: {
						autoSkip: true,
						autoSkipPadding: 30,
						maxRotation: 0,
						callback: (s) => secondsToInterval(Math.floor(s))
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
						title: context => context[0].raw[props.yAxis],
						label: context => [
							` work time: ${ secondsToInterval(context.raw.seconds_from_start) }`,
							` datetime: ${ millisToDateTime(context.raw.time) }`,
							` t: ${ context.raw.t }`,
							` i: ${ context.raw.i }`,
							` S: ${ context.raw.duty_cycle }`,
							` state: ${ context.raw.state }`,
						],
					},
					displayColors: false,
					backgroundColor: 'rgba(0, 0, 0, 0.7)'
				},
				zoom: {
					limits: {
						x: {
							min: 'original',
							max: 'original',
							minRange: 60 * 20
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


function setData(rawData) {
	let source = data.value;
	//console.log('chart updating by new array', data);
	if (!source) {
		chart.data.datasets[0].data = [];
		chart.stop();
		chart.update('none');
		return;
	}
	let max, min = source[0]?.[props.xAxis];
	max = source.at(-1)?.[props.xAxis];

	console.log('min: ', min, ' max: ', max)

	if (min)
		chart.options.scales.x.min = min;
	if (max && max !== min)
		chart.options.scales.x.max = max;

	chart.options.plugins.zoom.limits.x.min = min ?? 'original';
	chart.options.plugins.zoom.limits.x.max = max ?? 'original';

	chart.data.datasets[0].data = source.length > 1 ? fetchData(source, min, max) : source;
	if (source?.length <= 1) {
		dataCount.value = dataSegmentCount.value = dataVisibleCount.value = source?.length ?? 0;
	}
	chart.options.plugins.subtitle.text = getDisplayDataInfo();

	chart.stop();
	chart.update('none');
	console.log('chart updated by new array');

	//chart.resetZoom();
	chart.zoomScale('x', { min, max }, 'none');
}

watch(() => props.data, (d) => setData(data.value));

</script>

<style>

</style>
