<template>
	<vue-final-modal class="modal-container"
					 content-class="modal-content"
					 content-transition="vfm-fade"
					 overlay-transition="vfm-fade">
		<div class="modal--header">
			<span class="modal-title">Пользователь {{ user.login }}</span>
			<button class="modal-close" @click="emit('cancel')"><mdi-close /></button>
		</div>
		<div class="modal--content">
			<div class="s-form">
				<p class="fieldset">
					<input v-model="newPassword"
						   class="full-width has-padding has-border"
						   :type="newPasswordShow ? 'text' : 'password'"
						   placeholder="Новый пароль">
					<a class="hide-password" @click="newPasswordShow = !newPasswordShow">
						{{ newPasswordShow ? 'Hide' : 'Show' }}</a>
				</p>

				<label class="cb" style="display: block">
					<input type="checkbox" v-model="isAdmin">
					<span>Администратор</span>
				</label>
			</div>

			<p class="error-message" v-if="error">{{ error }}</p>
		</div>
		<div class="modal--action" style="padding: 0">
			<button @click="edit" :disabled="inProgress">Изменить</button>
			<button @click="emit('delete')" :disabled="inProgress">Удалить</button>
		</div>
		<template v-if="user.telegramBotUsers?.length > 0">
			<hr style="margin: 16px 0 8px 0">
			<div class="modal--content telegram-bot-users" style="margin-top: 0">
				<h6>Телеграм аккаунты</h6>
				<div v-for="telegramBotUser in user.telegramBotUsers">
					<p>{{ telegramBotUser.telegramUserId }} (@{{ telegramBotUser.username }})</p>
					<svg class="icon-18" @click="emit('logoutTelegramBotUser', telegramBotUser.telegramUserId)">
						<use href="#icon-x" />
					</svg>
				</div>
			</div>
		</template>
	</vue-final-modal>
</template>

<script setup>
import { ref, watch } from "vue";
import { VueFinalModal } from 'vue-final-modal'
import MdiClose from "@/components/MdiClose.vue";

const emit = defineEmits(['edit', "delete", 'logoutTelegramBotUser', 'cancel'])
const props = defineProps({
	user: Object,
	inProgress: { type: Boolean, default: false },
	error: String,
})

const newPassword = ref('');
const newPasswordShow = ref(false);
const isAdmin = ref(false);

function edit() {
	let patch = {};
	if (newPassword.value) patch.newPassword = newPassword.value;
	if (isAdmin.value !== props.user.isAdmin) patch.isAdmin = isAdmin.value;
	emit('edit', patch);
}

watch(() => props.user, user => {
	isAdmin.value = user.isAdmin;
}, { immediate: true });
</script>
