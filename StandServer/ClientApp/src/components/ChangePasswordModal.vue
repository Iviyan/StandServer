<template>
	<vue-final-modal class="modal-container"
					 content-class="modal-content"
					 content-transition="vfm-fade"
					 overlay-transition="vfm-fade">
		<div class="modal--header">
			<span class="modal-title">Изменение пароля</span>
			<button class="modal-close" @click="emit('cancel')">
				<mdi-close />
			</button>
		</div>
		<div class="modal--content">
			<div class="s-form">
				<p class="fieldset">
					<input v-model="oldPassword"
						   class="full-width has-padding has-border"
						   :type="oldPasswordShow ? 'text' : 'password'"
						   placeholder="Старый пароль">
					<a class="hide-password" @click="oldPasswordShow = !oldPasswordShow">
						{{ oldPasswordShow ? 'Hide' : 'Show' }}</a>
				</p>
				<p class="fieldset">
					<input v-model="newPassword"
						   class="full-width has-padding has-border"
						   :type="newPasswordShow ? 'text' : 'password'" placeholder="Новый пароль">
					<a class="hide-password"
					   @click="newPasswordShow = !newPasswordShow">{{ newPasswordShow ? 'Hide' : 'Show' }}</a>
				</p>

				<p class="error-message" v-if="props.error">{{ props.error }}</p>
			</div>
		</div>
		<div class="modal--action">
			<button @click="emit('submit', {oldPassword, newPassword})">Изменить</button>
		</div>
	</vue-final-modal>
</template>

<script setup>
import { ref } from "vue";
import { VueFinalModal } from 'vue-final-modal'
import MdiClose from "@/components/MdiClose.vue";

const emit = defineEmits([ 'submit', 'cancel' ])
const props = defineProps({
	error: String
})

const oldPassword = ref('');
const oldPasswordShow = ref(false);
const newPassword = ref('');
const newPasswordShow = ref(false);
</script>
