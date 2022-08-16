<template>
	<canvas ref="chartEl"></canvas>
</template>

<script setup>
import { ref, toRaw, onMounted, watch } from 'vue';

import { Chart, registerables } from 'chart.js';
import 'chartjs-adapter-luxon';
import zoomPlugin from 'chartjs-plugin-zoom';

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

const chartEl = ref(null);
let chart = null;

onMounted(() => {
    chart = new Chart(chartEl.value, {
        type: 'line',
        data: {
            datasets: [{
                backgroundColor: 'rgb(255, 99, 132)',
                borderColor: 'rgb(255, 99, 132)',
                data: toRaw(props.data),
				label: props.title,
				parsing: {
					xAxisKey: props.xAxis,
					yAxisKey: props.yAxis
				},
				segment: {
					borderColor: (ctx, value) => {
						return !ctx.p0.raw.state ? 'rgb(0,0,0,0.3)' : undefined
					}
				},
            }]
        },
        options: {
            //animation: false,
            interaction: {
                intersect: false
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
                    adapters: {
                        date: {
                            locale: 'ru'
                        }
                    },
                    ticks: {
                        //source: 'data',
                        major: {
                            enabled: true
                        },
                        autoSkip: true,
                        autoSkipPadding: 30,
                        maxRotation: 0,
                        font: context => context.tick && context.tick.major ? {
                            weight: 'bold'
                        } : null
                    }
                },
                y: {
                    suggestedMax: props.suggestedMax,
                    suggestedMin: props.suggestedMin
                }
            },
            plugins: {
                legend: {
                    display: true
                },
                zoom: {
                    limits: {
                        x: {
                            min: 'original',
                            max: 'original',
                            minRange: 30 * 1000
                        },
                    },
                    pan: {
                        enabled: true,
                        mode: 'x',
                    },
                    zoom: {
                        wheel: {
                            enabled: true,
                        },
                        mode: 'x',
                    }
                }
            }
        }
    });
});

watch(props.data, (data) => {
    data = toRaw(data);
    let min, max;
    if (data.length > 0) {
        min = data[0].x;
        chart.options.scales.x.min = min;
        //chart.options.plugins.zoom.limits.x.min = min;
    }
    if (data.length > 1) {
        max = data[data.length - 1].x;
        chart.options.scales.x.max = max;
        //chart.options.plugins.zoom.limits.x.max = max;
    }
    chart.update();
});
</script>

<style>

</style>
