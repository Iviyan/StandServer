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
				<a class="hide-password" @click="newUser.passwordShow = !newUser.passwordShow">
					{{ newUser.passwordShow ? 'Hide' : 'Show' }}</a>
			</p>
			<label class="cb" style="align-self: center">
				<input type="checkbox" v-model="newUser.isAdmin">
				<span>Администратор</span>
			</label>

			<input type="submit" value="Создать">
		</form>

		<p class="error-message">{{ newUser.error }}</p>

		<table class="users-table mt-16">
			<tr>
				<th>Логин</th>
				<th>Администратор</th>
				<th style="overflow-wrap: anywhere;">Телеграм аккаунты</th>
			</tr>
			<tr v-for="user in users" @click="selectUser(user)" :class="{ 'is-admin': user.is_admin }">
				<td>{{ user.login }}</td>
				<td>{{ user.is_admin ? "Да" : "Нет" }}</td>
				<td class="telegram-bot-users-list">
					<p v-for="telegramBotUser in user.telegram_bot_users">
						{{ telegramBotUser.telegram_user_id }} (@{{ telegramBotUser.username }})
					</p>
				</td>
			</tr>
		</table>

	</div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue';
import { useStore } from 'vuex'
import { useRouter } from 'vue-router';
import { call_get, call_delete, call_post, call_patch } from '@/utils/api';
import { useModal } from 'vue-final-modal'
import UserModal from "@/components/UserModal.vue";
import { errorToText, isEmpty } from "@/utils/utils";

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

const selectedUser = ref(null);

onMounted(async () => {
	isLoading.value = true;
	users.value = await call_get(`/api/users`);
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
			newUser.error = errorToText(err);
	}
}

const userVfmModal = useModal({
	component: UserModal,
	attrs: {
		onEdit: editUser,
		onDelete: deleteUser,
		onLogoutTelegramBotUser: logoutTelegramBotUser,
		onCancel() { userVfmModal.close(); }
	}
})
const userVfmModalAttrs = userVfmModal.options.attrs;

function selectUser(user) {
	selectedUser.value = user;
	userVfmModalAttrs.user = user;
	userVfmModal.open();
}

async function editUser(patch) {
	try {
		if (!isEmpty(patch)) {
			await call_patch(`/api/users/${ selectedUser.value.id }`, patch);
			if (patch.is_admin) selectedUser.value.is_admin = patch.is_admin;
		}
		userVfmModalAttrs.error = '';
		await userVfmModal.close();
	} catch (err) {
		userVfmModalAttrs.error = errorToText(err);
	}
}

async function deleteUser() {
	try {
		await call_delete(`/api/users/${ selectedUser.value.id }`);

		let index = users.value.findIndex(u => u.id === selectedUser.value.id);
		users.value.splice(index, 1);
		userVfmModalAttrs.error = '';
		await userVfmModal.close();
	} catch (err) {
		userVfmModalAttrs.error = errorToText(err);
	}
}

async function logoutTelegramBotUser(telegramUserId) {
	try {
		await call_delete(`/api/telegram-users/${ telegramUserId }`);

		let telegramUserIndex = selectedUser.value.telegram_bot_users
			.findIndex(u => u.telegram_user_id === telegramUserId);
		selectedUser.value.telegram_bot_users.splice(telegramUserIndex, 1);
		userVfmModalAttrs.error = '';
	} catch (err) {
		userVfmModalAttrs.error = errorToText(err);
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
