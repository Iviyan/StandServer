<template>
	<router-view />
	<ModalsContainer />
	<Icons />
</template>

<script setup>
import { watch, onErrorCaptured } from 'vue';
import { useStore } from 'vuex'
import { useRouter } from 'vue-router'
import iziToast from "izitoast";
import { ModalsContainer } from 'vue-final-modal'
import { RequestError } from "@/exceptions";
import Icons from "@/components/Icons";

const store = useStore();
const router = useRouter();

onErrorCaptured((err, vm, info) => {
	console.log(`error: ${ err.toString() }\ninfo: ${ info }`);
	if (err instanceof RequestError) {
		if (err.rfc7807) {
			iziToast.error({
				title: 'Ошибка запроса.',
				message: err.message,
				layout: 2,
				timeout: 4000,
				class: "iziToast-api-error"
			});
		} else {
			iziToast.error({
				title: 'Неизвестная ошибка запроса.',
				message: 'Код: ' + err.status,
				timeout: 4000,
				class: "iziToast-api-error"
			});
		}
	}
	//return false;
});

watch(() => store.state.auth.jwt, (newJwt) => {
	if (!newJwt) router.push('/login')
})

</script>

<style>

body {
	margin: 0;
	font-size: 1rem;
	line-height: 1.5;
	font-family: system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", "Noto Sans", "Liberation Sans", Arial, sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol", "Noto Color Emoji";
}

button, input, optgroup, select, textarea {
	margin: 0;
	font-family: inherit;
	font-size: inherit;
	line-height: inherit;
}

/* modal styles */

.modal-container {
	display: flex;
	justify-content: center;
	align-items: center;
}

.modal-content { /*relative p-4 rounded-lg bg-white dark:bg-gray-900*/
	position: relative;
	display: flex;
	flex-direction: column;
	max-height: 90%;
	margin: 0 1rem;
	padding: 1rem;
	border: 1px solid #e2e8f0;
	border-radius: 0.25rem;
	background: #fff;
}

.modal--header {
	display: flex;
	align-items: center;
}
.modal-title {
	margin: 0 32px 0 0;
	font-size: 1.5rem;
	font-weight: 600;
	flex-grow: 1;
 }
.modal-close {
	border: none;
	background-color: transparent;
	cursor: pointer;
	padding: 0;
	line-height: 0;
}

.modal--content {
	margin-top: 10px;
	overflow-y: auto;
}

.modal--action {
	display: flex;
	justify-content: center;
	align-items: center;
	flex-shrink: 0;
	padding: 1rem 0 0;
}

.modal--action > button {
	padding: 0.25rem 0.5rem;
	border-width: 1px;
	border-radius: 0.25rem;
	background-color: transparent;
	background-image: none;
	cursor: pointer;
}

.modal--action > button + button {
	margin-left: 12px;
}

.modal--action > button:not(:disabled):hover {
	background-color: rgba(7, 84, 197, 0.1)
}

/* checkbox */

.cb > input {
	position: absolute;
	z-index: -1;
	opacity: 0;
}

.cb > span {
	display: inline-flex;
	align-items: center;
	user-select: none;
}

.cb > span::before {
	content: '';
	display: inline-block;
	width: 1em;
	height: 1em;
	flex-shrink: 0;
	flex-grow: 0;
	border: 1px solid #adb5bd;
	border-radius: 0.25em;
	margin-right: 0.5em;
	background-repeat: no-repeat;
	background-position: center center;
	background-size: 50% 50%;
}

.cb > input:not(:disabled):not(:checked) + span:hover::before {
	border-color: #b3d7ff;
}

.cb > input:not(:disabled):active + span::before {
	background-color: #b3d7ff;
	border-color: #b3d7ff;
}

.cb > input:focus + span::before {
	box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25);
}

.cb > input:focus:not(:checked) + span::before {
	border-color: #80bdff;
}

.cb > input:checked + span::before {
	border-color: #0b76ef;
	background-color: #0b76ef;
	background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 8 8'%3e%3cpath fill='%23fff' d='M6.564.75l-3.59 3.612-1.538-1.55L0 4.26 2.974 7.25 8 2.193z'/%3e%3c/svg%3e");
}

.cb > input:disabled + span::before {
	background-color: #e9ecef;
}

/* utils */

.ms-6 { margin-left: 6px; }

.mt-8 { margin-top: 8px; }

.mt-16 { margin-top: 16px; }

.nowrap { white-space: nowrap; }

@media (max-width: 500px) {
	.hide-500 { display: none; }
}

@media (max-width: 600px) {
	.hide-600 { display: none; }
}

@media (max-width: 800px) {
	.hide-800 { display: none; }
}

@media (max-width: 900px) {
	.hide-900 { display: none; }
}

@media (max-width: 1000px) {
	.hide-1000 { display: none; }
}

@media (min-width: 500px) {
	.show-500 { display: none; }
}

@media (min-width: 700px) {
	.show-700 { display: none; }
}

@media (min-width: 900px) {
	.show-900 { display: none; }
}

@media (min-width: 1000px) {
	.show-1000 { display: none; }
}

</style>
