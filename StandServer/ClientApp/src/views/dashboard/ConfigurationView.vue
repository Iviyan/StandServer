<template>
	<h2 class="header">Настройки</h2>

	<form class="configuration-form" @submit.prevent="save">
		<label>
			Максимальный ток в выключенном состоянии
			<input v-model.number="mutableConfiguration.offSampleMaxI"
				   style="align-self: center"
				   placeholder="offSampleMaxI">
		</label>

		<button type="submit">Сохранить</button>
	</form>

	<p class="error-message">{{ error }}</p>
</template>

<script setup>
import { ref, reactive, onMounted, computed } from 'vue';
import { useStore } from 'vuex'
import { callPatch } from '@/utils/api';
import { errorToText, isEmpty } from "@/utils/utils";

const store = useStore();

const configuration = computed(() => store.state.dashboard.configuration);

const mutableConfiguration = reactive({
	offSampleMaxI: 0
});
const error = ref('');

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
		error.value = '';
		return;
	}

	try {
		await callPatch(`/api/configuration`, patch);
		store.commit('updateConfiguration', patch);

		error.value = '';
	} catch (err) {
		error.value = errorToText(err);
	}
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
	border: 1px solid #d2d8d8;
	appearance: none;
	margin: 0 0 12px 0;
}

.configuration-form input:focus {
	border-color: #343642;
	box-shadow: 0 0 5px rgba(52, 54, 66, 0.1);
	outline: none;
}


.configuration-form button[type=submit] {
	padding: 0.25rem 0.5rem;
	border-width: 1px;
	border-radius: 0.25rem;
	background-color: transparent;
	cursor: pointer;
	width: 100%;
}

.error-message {
	margin: 8px 0;
	color: #c00;
	text-align: center;
	word-break: break-word;
}

</style>
