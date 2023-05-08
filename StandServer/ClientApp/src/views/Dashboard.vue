<template>
	<nav class="main-menu">

		<div class="scrollbar">
			<p class="stand-status">
				<span v-if="standState.lastMeasurementTime">Последнее измерение: {{millisToDateTime(standState.lastMeasurementTime) }}</span>
				<span v-else>Измерений нет</span>
			</p>
			<ul>
				<li class="darker-shadow-down pd-b" :class="{active: router.currentRoute.value.name === 'home'}">
					<router-link :to="{ name: 'home' }">
						<span class="nav-text">Главная</span>
					</router-link>
				</li>

				<!--suppress EqualityComparisonWithCoercionJS -->
				<li class="darker" v-for="sampleId in sampleIds"
					:class="{active: router.currentRoute.value.name === 'sample' && router.currentRoute.value.params.id == sampleId}">
					<router-link :to="{ name: 'sample', params: { id: sampleIdFormat(sampleId) }}">
						<span class="nav-text">{{ sampleIdFormat(sampleId) }}</span>
					</router-link>
				</li>

				<li class="darker-shadow pd-t">
					<a href="#" @click="logout">
						<span class="nav-text">Выход</span>
					</a>
				</li>
				<li class="dark">
					<a href="#" @click="changePasswordVfmModal.open()">
						<span class="nav-text">Изменить пароль</span>
					</a>
				</li>
				<li class="dark" v-if="store.getters.isAdmin" :class="{active: router.currentRoute.value.name === 'users'}">
					<router-link :to="{ name: 'users' }">
						<span class="nav-text">Пользователи</span>
					</router-link>
				</li>
			</ul>
		</div>
	</nav>

	<main>
		<router-view v-if="isDashboardReady" />
	</main>
</template>

<script setup>
import { onMounted, onUnmounted, computed, ref, provide, shallowRef, watch, reactive } from 'vue'
import { useStore } from 'vuex'
import { useRouter } from 'vue-router'
import * as signalR from '@microsoft/signalr'
import iziToast from 'izitoast'
import { useModal } from 'vue-final-modal'
import ChangePasswordModal from "@/components/ChangePasswordModal.vue";
import { callPost } from '@/utils/api';
import { sampleIdFormat } from "@/utils/stringUtils";
import { millisToDateTime } from "@/utils/timeUtils";
import { errorToText } from "@/utils/utils";

const store = useStore();
const router = useRouter();

const isDashboardReady = ref(false);
const standState = reactive({
	lastMeasurementTime: null
});

const sampleIds = computed(() => store.state.dashboard.sampleIds);

// SignalR
let signalRConnectionRef = shallowRef(null);
let signalROnReconnectActions = [];
let signalRStartTimeout = null;
let signalRManuallyDisconnect = false;
let onNewMeasurementsCallbacks = [];

let signalRConnection = null;

provide("signalr", {
	signalRConnection: signalRConnectionRef,
	signalROnReconnectActions,
	onNewMeasurementsCallbacks
});

async function signalRStart() {
	try {
		await signalRConnection.start();
		console.assert(signalRConnection.state === signalR.HubConnectionState.Connected);
		console.info("SignalR Connected.");

		let toast = document.querySelector('.iziToast-signalr-connection-failed');
		if (toast) iziToast.hide({}, toast);
	} catch (err) {
		console.assert(signalRConnection.state === signalR.HubConnectionState.Disconnected);
		console.error("SignalR error\n", err.toString(), "\nReconnect after 5 seconds...");
		iziToast.error({
			title: 'Ошибка подключения.',
			message: 'Повторная попытка подключения через 5 секунд...<br>Ошибка: ' + err.toString(),
			timeout: 5000,
			layout: 2,
			class: "iziToast-signalr-connection-failed"
		});
		signalRStartTimeout = setTimeout(() => signalRStart(), 5000);
	}
}

onMounted(async () => {
	await store.dispatch('loadSampleIds');

	signalRConnection = signalRConnectionRef.value = new signalR.HubConnectionBuilder()
		.withUrl('/api/stand-hub')
		.configureLogging(signalR.LogLevel.Warning)
		.withAutomaticReconnect([ 0, 2000, 5000 ])
		.build();

	signalRConnection.onreconnecting(error => {
		console.assert(signalRConnection.state === signalR.HubConnectionState.Reconnecting);
		console.warn(`Connection lost due to error "${ error }". Reconnecting.`);
		iziToast.error({
			title: 'Потеря соединения.',
			message: 'Выполняется попытка переподключения.<br>Ошибка: ' + error,
			timeout: false,
			class: "iziToast-signalr-reconnecting"
		});
	});
	signalRConnection.onreconnected(async connectionId => {
		console.assert(signalRConnection.state === signalR.HubConnectionState.Connected);
		console.log(`Connection reestablished. Connected with connectionId "${ connectionId }".`);

		for (let action of signalROnReconnectActions) {
			let r = action();
			if (r instanceof Promise) await r;
		}

		let toast = document.querySelector('.iziToast-signalr-reconnecting');
		if (toast) iziToast.hide({}, toast);

		iziToast.success({
			title: 'Соединение восстановлено',
			timeout: 1000,
			class: "iziToast-signalr"
		});
	});
	signalRConnection.onclose(error => {
		console.assert(signalRConnection.state === signalR.HubConnectionState.Disconnected);
		console.error(`Connection closed due to error "${ error }". Try refreshing this page to restart the connection.`);

		let toast = document.querySelector('.iziToast-signalr-reconnecting');
		if (toast) iziToast.hide({}, toast);

		if (signalRManuallyDisconnect) return;

		iziToast.error({
			title: 'Потеря соединения.',
			message: 'Перезагрузите страницу для восстановления соединения.<br>Ошибка: ' + error,
			timeout: false,
			layout: 2,
			class: "iziToast-signalr-disconnect"
		});
	});

	signalRConnection.on("ActiveInfo", (lastMeasurementTime) =>
		[standState.lastMeasurementTime] = [lastMeasurementTime]);

	signalRConnection.on("Msg", data => console.log(data));

	signalRConnection.on("NewMeasurements", async (measurements) => {
		for (let cb of onNewMeasurementsCallbacks) {
			let r = cb(measurements);
			if (r instanceof Promise) await r;
		}
	});
	onNewMeasurementsCallbacks.push(async (measurements) => {
		await store.dispatch("newMeasurements", measurements);
	});

	let signalRStartPromise = signalRStart();
	signalRStartPromise.then(async () => {
		await store.dispatch('loadLastMeasurements');

		const subscribeFunc = () => signalRConnection.send("SubscribeToMeasurements");
		await subscribeFunc();
		signalROnReconnectActions.push(subscribeFunc);

		store.commit("setLastMeasurementsInitialized", true);
	});

	await signalRStartPromise;

	isDashboardReady.value = true;
})

onUnmounted(async () => {
	signalRManuallyDisconnect = true;
	store.commit("setHomeViewVisited", false);
	store.commit("setLastMeasurementsInitialized", false);

	if (signalRStartTimeout) clearTimeout(signalRStartTimeout);
	if (signalRConnection && signalRConnection?.state !== signalR.HubConnectionState.Disconnected)
		signalRConnection.stop();

	for (let toast of document.querySelectorAll('.iziToast'))
		iziToast.hide({}, toast);
})

async function logout() {
	await store.dispatch('logout');
}

// Change password

const changePasswordVfmModal = useModal({
	component: ChangePasswordModal,
	attrs: {
		async onSubmit({oldPassword, newPassword}) {
			try {
				await callPost('/change-password', { oldPassword, newPassword });

				changePasswordVfmModal.options.attrs.error = '';
				await changePasswordVfmModal.close()
			} catch (err) {
				changePasswordVfmModal.options.attrs.error = errorToText(err);
			}
		},
		onCancel() { changePasswordVfmModal.close(); }
	},
})

</script>

<style>
main {
	margin-left: 220px;
	padding: 16px 10px;
	position: relative;
	min-height: 100vh;
	box-sizing: border-box;
}

@media (max-width: 700px) {
	main {
		margin-left: 0;
	}
}

/* --- sidebar --- */

.scrollbar {
	height: 90%;
	width: 100%;
	overflow-y: scroll;
	overflow-x: hidden;
	margin-top: 20px;
}

/* Scrollbar Style */

.scrollbar {
	/* firefox */
	scrollbar-width: thin;
	scrollbar-color: #BFBFBF #F7F7F7;
}

.scrollbar::-webkit-scrollbar {
	width: 5px;
	background-color: #F7F7F7;
}

.scrollbar::-webkit-scrollbar-thumb {
	border-radius: 10px;
	box-shadow: inset 0 0 6px rgba(0, 0, 0, .3);
	background-color: #BFBFBF;
}

/* stand status */

.stand-status {
	margin: 0 8px 12px;
	border: 1px solid #aaa;
	border-radius: 8px;
	text-align: center;
	padding: 2px;
}

.stand-status > span {
	display: block;
}

.stand-status.on {
	border-color: #328f1c;
	background-color: rgba(50, 143, 28, 0.07);
}

.stand-status.off {
	border-color: #8f371c;
	background-color: rgba(143, 55, 28, 0.07);
}

/* Scrollbar End */

.main-menu {
	background: #F7F7F7;
	position: fixed;
	top: 0;
	bottom: 0;
	height: 100%;
	left: 0;
	width: 220px;
	overflow: hidden;
	transition: width .2s linear;
	box-shadow: 1px 0 5px rgb(0 0 0 / 20%)
}

@media (max-width: 700px) {
	.main-menu {
		position: static;
		max-height: 100%;
		height: auto;
		width: 100%;
	}
}

.main-menu li {
	position: relative;
	display: block;
	width: 100%;
}

.main-menu li.pd-t {
	padding-top: 16px;
}

.main-menu li.pd-b {
	padding-bottom: 16px;
}

.main-menu li > a > * {
	padding-left: 12px;
}

.main-menu li > a {
	position: relative;
	width: 100%;
	height: 30px;
	display: table;
	border-collapse: collapse;
	border-spacing: 0;
	color: #444;
	font-size: 14px;
	text-decoration: none;
	transition: all .14s linear;
	font-family: 'Strait', sans-serif;
	border-top: 1px solid #f2f2f2;
	text-shadow: 1px 1px 1px #fff;
}

.main-menu .nav-text {
	position: relative;
	display: table-cell;
	vertical-align: middle;
	font-family: 'Titillium Web', sans-serif;
}

nav {
	user-select: none;
}

nav ul, nav li {
	outline: 0;
	margin: 0;
	padding: 0;
}


/* Darker element side menu Start*/


.main-menu li.darker {
	background-color: #ededed;
}

.main-menu li.dark {
	background-color: #f3f3f3;
}

.main-menu li.darker-shadow {
	background-color: #f3f3f3;
	box-shadow: inset 0px 5px 5px -4px rgba(50, 50, 50, 0.55);
}

.main-menu li.darker-shadow-down {
	background-color: #f3f3f3;
	box-shadow: inset 0px -4px 5px -4px rgba(50, 50, 50, 0.55);
}

/* Darker element side menu End*/


.main-menu li:hover > a {
	color: #fff;
	background-color: #54afaf;
	text-shadow: 0px 0px 0px;
}

.main-menu li.active > a {
	color: #fff;
	background-color: #9dbdbd;
	text-shadow: 0px 0px 0px;
}

/* change password form */

.s-form {
	padding: 6px;
}

.s-form input {
	box-sizing: border-box;
}

.s-form .fieldset {
	position: relative;
	margin: 8px 0;
}

.s-form .fieldset:first-child {
	margin-top: 0;
}

.s-form .fieldset:last-child {
	margin-bottom: 0;
}

.s-form label {
	font-size: 14px;
}

.s-form input {
	margin: 0;
	padding: 0;
	border-radius: 2px;
}

.s-form input.full-width {
	width: 100%;
}

.s-form input.has-padding {
	padding: 8px 20px 8px 20px;
}

.s-form input.has-border {
	border: 1px solid #d2d8d8;
	appearance: none;
}

.s-form input.has-border:focus {
	border-color: #343642;
	box-shadow: 0 0 5px rgba(52, 54, 66, 0.1);
	outline: none;
}

.s-form input[type=password] {
	/* space left for the HIDE button */
	padding-right: 65px;
}

.s-form input[type=submit] {
	padding: 16px 0;
	cursor: pointer;
	background: #2f889a;
	color: #FFF;
	font-weight: bold;
	border: none;
	appearance: none;
}

.s-form .hide-password {
	display: inline-block;
	position: absolute;
	right: 0;
	top: 50%;
	padding: 6px 15px;
	border-left: 1px solid #d2d8d8;
	bottom: auto;
	cursor: pointer;
	transform: translateY(-50%);
	font-size: 14px;
	color: #343642;
}

.s-form .error-message {
	margin: 8px 0;
	color: #c00;
	text-align: center;
	word-break: break-word;
}

</style>

