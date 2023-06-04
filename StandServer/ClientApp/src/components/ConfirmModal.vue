<template>
	<vue-final-modal class="modal-container"
					 content-class="modal-content"
					 content-transition="vfm-fade"
					 overlay-transition="vfm-fade">
		<div class="modal--header">
			<span class="modal-title">{{ title }}</span>
			<button class="modal-close" @click="emit('cancel')">
				<mdi-close />
			</button>
		</div>
		<div class="modal--content">
			<p class="cm-error" v-if="error">{{ error }}</p>
		</div>
		<div class="modal--action">
			<button @click="emit('submit')" :disabled="inProgress">{{ actionName }}</button>
		</div>
	</vue-final-modal>
</template>

<script setup>
import { VueFinalModal } from 'vue-final-modal'
import MdiClose from "@/components/MdiClose.vue";

const emit = defineEmits([ 'submit', 'cancel' ])
const props = defineProps({
	title: String,
	actionName: { type: String, default: 'Подтвердить' },
	inProgress: { type: Boolean, default: false },
	error: String,
})

</script>

<style>
.cm-error {
	color: #c00;
	text-align: center;
	word-break: break-word;
}
</style>
