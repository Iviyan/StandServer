<template>
	<h2 class="header">Настройки</h2>

	<form class="configuration-form" @submit.prevent="save">
		<label>
			Максимальный ток в нерабочем состоянии
			<input v-model.number="mutableConfiguration.offSampleMaxI"
				   style="align-self: center"
				   :readonly="!isAdmin"
				   placeholder="offSampleMaxI">
		</label>

		<button type="submit" v-show="isAdmin" :disabled="!isAdmin">Сохранить</button>
	</form>

	<p class="error-message" v-if="configurationSaveError">{{ configurationSaveError }}</p>


	<h3 v-if="isAdmin">Загрузка измерений</h3>
	<form class="configuration-form" @submit.prevent="loadMeasurements" v-if="isAdmin">

		<textarea v-model="rawMeasurements"
				  :readonly="!isAdmin"
				  placeholder="00000123 12:00 01.01.2023|0:01| 10|39|50|7213|1000| 50| 10|10000|W&#xA;2 00000123 12:00 01.01.2023|0:01| 10|39|50|7213|1000| 50| 10|10000|W"></textarea>

		<button type="submit" :disabled="isMeasurementsLoading">Загрузить</button>
	</form>

	<p class="error-message" v-if="loadMeasurementsError">{{ loadMeasurementsError }}</p>

	<form class="configuration-form mt-16" @submit.prevent="reloadCache" v-if="isAdmin">
		<button type="submit">Перезагрузить кэш</button>
		<p class="info">
			Прерагрузка кэша может понадобится после ручного редактирования данных в БД
			для отображения	актуального списка образцов и последних измерений.
		</p>
	</form>

	<!-- TODO: Telegram bot link -->

</template>

<script setup>
import { ref, reactive, onMounted, computed } from 'vue';
import { useStore } from 'vuex'
import { callGet, callPatch, callPost } from '@/utils/api';
import { errorToText, isEmpty } from "@/utils/utils";
import iziToast from "izitoast";

const store = useStore();

const configuration = computed(() => store.state.dashboard.configuration);
const isAdmin = computed(() => store.getters.isAdmin);

const mutableConfiguration = reactive({
	offSampleMaxI: 0
});
const configurationSaveError = ref('');

const rawMeasurements = ref('');
const loadMeasurementsError = ref('');
const isMeasurementsLoading = ref(false);

onMounted(() => {
	Object.assign(mutableConfiguration, configuration.value);
});

async function save() {
	const origin = configuration.value;
	const cur = mutableConfiguration;
	let patch = {};
	for (let p in cur) {
		if (cur[p] !== origin[p])
			patch[p] = cur[p];
	}
	console.debug(patch);
	if (isEmpty(patch)) {
		configurationSaveError.value = '';
		return;
	}

	try {
		await callPatch(`/api/configuration`, patch);
		store.commit('updateConfiguration', patch);

		configurationSaveError.value = '';

		iziToast.success({
			title: 'Настройки сохранены',
			timeout: 2000,
			layout: 2
		});
	} catch (err) {
		configurationSaveError.value = errorToText(err);
	}
}

async function loadMeasurements() {
	try {
		isMeasurementsLoading.value = true;
		await callPost(`/api/samples?silent=true`, rawMeasurements.value);
		isMeasurementsLoading.value = false;
		rawMeasurements.value = '';
		loadMeasurementsError.value = '';

		iziToast.success({
			title: 'Загрузка измерений завершена',
			message: 'Перезагрузите страницу для того, чтобы увидеть изменения.',
			timeout: 5000,
			layout: 2
		});
	} catch (err) {
		loadMeasurementsError.value = errorToText(err);
		isMeasurementsLoading.value = false;
	}
}

async function reloadCache() {
	await callGet(`/api/reload-cache`);

	iziToast.success({
		title: 'Кэш перезагружен',
		message: 'Перезагрузите страницу для того, чтобы увидеть изменения.',
		timeout: 5000,
		layout: 2
	});
}

</script>

<style>

.header {
	margin: 0 0 10px 0;
	text-align: center;
	font-weight: 500;
}

.configuration-form {

}

.configuration-form input:not([type]), .configuration-form input[type=text] {
	box-sizing: border-box;
	width: 100%;
	padding: 4px 16px;
	border-radius: 2px;
	border: 1px solid #aaa;
	appearance: none;
	margin: 0 0 12px 0;
}

.configuration-form input:focus {
	border-color: #333;
	box-shadow: 0 0 5px rgba(52, 54, 66, 0.1);
	outline: none;
}

.configuration-form textarea {
	box-sizing: border-box;
	width: 100%;
	resize: vertical;
	padding: 4px 16px;
	border-radius: 4px;
	border: 1px solid #aaa;
	appearance: none;
	margin: 0;
	min-height: 50px;
	height: 150px;
}


.configuration-form button[type=submit] {
	padding: 0.25rem 0.5rem;
	border-width: 1px;
	border-radius: 0.25rem;
	background-color: transparent;
	cursor: pointer;
	width: 100%;
}

</style>

<style scoped>
h3 {
	font-weight: normal;
	font-size: 1.25rem;
	margin: 24px 0 6px 0;
	text-align: center;
}

.info {
	margin: 0;
}

.info > a {
	color: unset;
}
</style>
