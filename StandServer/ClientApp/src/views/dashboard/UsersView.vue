<template>
	<div>
		<h2 class="header">Пользователи</h2>

		<form class="new-user-form" @submit.prevent="addUser">
			<input v-model="newUser.login"
				   style="align-self: center"
				   placeholder="Логин">
			<p class="fieldset">
				<input v-model="newUser.password"
					   autocomplete="new-password"
					   :type="newUser.passwordShow ? 'text' : 'password'"
					   placeholder="Пароль">
				<a class="hide-password"
				   @click="newUser.passwordShow = !newUser.passwordShow">{{
						newUser.passwordShow ? 'Hide' : 'Show'
					}}</a>
			</p>
			<label class="cb" style="align-self: center">
				<input type="checkbox" v-model="newUser.isAdmin">
				<span>Возможность редактировать пользоваетелей</span>
			</label>

			<input type="submit" value="Создать">
		</form>

		<p class="error-message">{{ newUser.error }}</p>

		<table class="users-table mt-16">
			<tr>
				<th>Логин</th>
				<th>Возможность редактирования пользователей</th>
				<th style="overflow-wrap: anywhere;">Телеграм аккаунты</th>
			</tr>
			<tr v-for="user in users" @click="userModal.user = user" :class="{ 'is-admin': user.is_admin }">
				<td>{{ user.login }}</td>
				<td>{{ user.is_admin ? "Есть" : "Нет" }}</td>
				<td class="telegram-bot-users-list">
					<p v-for="telegramBotUser in user.telegram_bot_users">
						{{ telegramBotUser.telegram_user_id }} (@{{ telegramBotUser.username }})
					</p>
				</td>
			</tr>
		</table>

	</div>

	<vue-final-modal v-model="userModal.show" classes="modal-container" content-class="modal-content"
					 :escToClose="true">
		<div class="modal-header">
			<span class="modal-title">Пользователь {{ userModal.user?.login }}</span>
			<button class="modal-close" @click="userModal.show = false"><mdi-close /></button>
		</div>
		<div class="modal__content">
			<div class="s-form">
				<p class="fieldset">
					<input v-model="userModal.newPassword"
						   class="full-width has-padding has-border"
						   :type="userModal.newPasswordShow ? 'text' : 'password'"
						   placeholder="Новый пароль">
					<a class="hide-password"
					   @click="userModal.newPasswordShow = !userModal.newPasswordShow">{{
							userModal.newPasswordShow ? 'Hide' : 'Show'
						}}</a>
				</p>

				<label class="cb" style="display: block">
					<input type="checkbox" v-model="userModal.isAdmin">
					<span>Возможность редактировать пользоваетелей</span>
				</label>
			</div>

			<p class="error-message">{{ userModal.error }}</p>
		</div>
		<div class="modal__action" style="padding: 0">
			<button @click="changeUser">Изменить</button>
			<button @click="deleteUser">Удалить</button>
			<button @click="userModal.show = false">Отмена</button>
		</div>
		<hr style="margin: 16px 0 8px 0">
		<div class="modal__content telegram-bot-users" style="margin-top: 0">
			<h6>Телеграм аккаунты</h6>
			<div v-for="telegramBotUser in userModal.user?.telegram_bot_users">
				<p>{{ telegramBotUser.telegram_user_id }} (@{{ telegramBotUser.username }})</p>
				<svg class="icon-18" @click="logoutTelegramBotUser(telegramBotUser.telegram_user_id)">
					<use href="#icon-x" />
				</svg>
			</div>
		</div>
	</vue-final-modal>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue';
import { useStore } from 'vuex'
import { useRouter } from 'vue-router';

import { VueFinalModal } from 'vue-final-modal'
import MdiClose from '@/components/MdiClose.vue'

import { call_get, call_delete, call_post, call_patch } from '@/utils/api';
import { RequestError } from "@/exceptions";

const store = useStore();
const router = useRouter();

const users = ref([]);

const isLoading = ref(false);

const newUser = reactive({
	login: '',
	password: '',
	passwordShow: false,
	isAdmin: false,
	error: ''
});

const userModal = reactive({
	_user: null,
	get user() { return this._user; },
	set user(u) {
		this._user = u;
		this.isAdmin = !!u?.is_admin;
	},
	get show() { return !!this.user; },
	set show(value) { if (!value) this.user = null; else throw new Error("Set user property to open modal"); },
	newPassword: '',
	newPasswordShow: false,
	resetTelegram: false,
	isAdmin: false,
	error: ''
});

onMounted(async () => {
	isLoading.value = true;
	users.value = await call_get(`/api/users`);
	console.log(users.value);
	isLoading.value = false;
});

async function addUser() {
	try {
		let createdUser = await call_post('/api/users', {
			login: newUser.login,
			password: newUser.password,
			is_admin: newUser.isAdmin
		});
		users.value.push(createdUser);
		newUser.error = '';
	} catch (err) {
		if (err instanceof RequestError)
			newUser.error = err.rfc7807 ? err.message : 'Ошибка запроса';
		else
			newUser.error = 'Неизвестная ошибка';
	}
}

async function changeUser() {
	try {
		let patch = {};
		if (userModal.newPassword) patch.new_password = userModal.newPassword;
		if (userModal.isAdmin !== userModal.user.is_admin) patch.is_admin = userModal.isAdmin;

		await call_patch(`/api/users/${ userModal.user.id }`, patch);

		userModal.user.is_admin = userModal.isAdmin;
		userModal.error = '';
		userModal.show = false;
	} catch (err) {
		if (err instanceof RequestError)
			userModal.error = err.rfc7807 ? err.message : 'Ошибка запроса';
		else
			userModal.error = 'Неизвестная ошибка';
	}
}

async function deleteUser() {
	try {
		await call_delete(`/api/users/${ userModal.user.id }`);

		let index = users.value.findIndex(u => u.id === userModal.user.id);
		users.value.splice(index, 1);
		userModal.error = '';
		userModal.show = false;
	} catch (err) {
		if (err instanceof RequestError)
			userModal.error = err.rfc7807 ? err.message : 'Ошибка запроса';
		else
			userModal.error = 'Неизвестная ошибка';
	}
}

async function logoutTelegramBotUser(telegramUserId) {
	try {
		await call_delete(`/api/telegram-users/${ telegramUserId }`);

		let telegramUserIndex = userModal.user.telegram_bot_users
			.findIndex(u => u.telegram_user_id === telegramUserId);
		userModal.user.telegram_bot_users.splice(telegramUserIndex, 1);
		userModal.error = '';
	} catch (err) {
		if (err instanceof RequestError)
			userModal.error = err.rfc7807 ? err.message : 'Ошибка запроса';
		else {
			userModal.error = 'Неизвестная ошибка';
			console.error(err);
		}
	}
}
</script>

<style>

.header {
	margin: 0 0 10px 0;
	text-align: center;
	font-weight: 500;
}

.new-user-form {
	display: grid;
	grid-auto-flow: column;
	grid-template-columns: 1fr 1fr;
	grid-template-rows: auto auto;
	grid-column-gap: 8px;
	grid-row-gap: 8px;
	justify-content: start;
}

@media (max-width: 700px) {
	.new-user-form {
		grid-template-columns: 1fr;
		grid-template-rows: auto auto auto auto;
	}
}

.new-user-form input:not([type]), .new-user-form input[type=text], .new-user-form input[type=password] {
	box-sizing: border-box;
	width: 100%;
	padding: 4px 16px;
	margin: 0;
	border-radius: 2px;
	border: 1px solid #d2d8d8;
	appearance: none;
}

.new-user-form input:focus {
	border-color: #343642;
	box-shadow: 0 0 5px rgba(52, 54, 66, 0.1);
	outline: none;
}

.new-user-form .fieldset {
	position: relative;
	margin: 0px 0;
}

.new-user-form input[type=password] { padding-right: 65px; }

.new-user-form .fieldset:first-child { margin-top: 0; }

.new-user-form .fieldset:last-child { margin-bottom: 0; }

.new-user-form input[type=submit] {
	padding: 4px;
	cursor: pointer;
	background: #2f889a;
	color: #FFF;
	border: none;
	appearance: none;
}

.new-user-form .hide-password {
	display: inline-block;
	position: absolute;
	right: 0;
	top: 50%;
	padding: 2px 15px;
	border-left: 1px solid #d2d8d8;
	bottom: auto;
	cursor: pointer;
	transform: translateY(-50%);
	font-size: 14px;
	color: #343642;
}

.error-message {
	margin: 8px 0;
	color: #c00;
	text-align: center;
	word-break: break-word;
}

.users-table {
	text-align: center;
	border-collapse: collapse;
	border: 0px solid #bbb;
	width: 100%;
}

.users-table th, .users-table td {
	border: 0.1px solid #aaa;
	padding: 4px;
}

.users-table th {
	font-weight: 500;
	padding: 4px 6px;
}

.users-table tr:not(:first-child):hover {
	background-color: rgba(0, 0, 0, 0.05);
	cursor: pointer;
}

.users-table .telegram-bot-users-list > p {
	margin: 0;
	overflow-wrap: anywhere;
}

.users-table .is-admin { background-color: rgba(157, 24, 46, 0.03); }

.telegram-bot-users {

}

.telegram-bot-users > h6 {
	all: unset;
	display: block;
	margin: 0 0 8px 0;
	text-align: center;
	font-size: 1.08rem;
}

.telegram-bot-users > div {
	display: grid;
	grid-template-columns: 1fr auto;
	column-gap: 8px;
	align-items: center;
	padding: 4px;
	border: solid 1px #aaa;
	border-radius: 4px;
}

.telegram-bot-users > div:not(:first-child) {
	margin-top: 6px;
}

.telegram-bot-users > div > p {
	margin: 0;
}

</style>

<style scoped>
hr {
	width: 100%;
	background-color: #bbb;
	height: 1px;
	border: none;
}
</style>
