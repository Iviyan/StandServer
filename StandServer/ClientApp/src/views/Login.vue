<template>
	<teleport to="body">
		<div class="login-modal">
			<form class="login-form" @submit.prevent="tryLogin">
				<div class="flex-row">
					<label class="lf--label" for="username">
						<svg x="0px" y="0px" width="12px" height="13px">
							<path fill="#B1B7C4"
								  d="M8.9,7.2C9,6.9,9,6.7,9,6.5v-4C9,1.1,7.9,0,6.5,0h-1C4.1,0,3,1.1,3,2.5v4c0,0.2,0,0.4,0.1,0.7 C1.3,7.8,0,9.5,0,11.5V13h12v-1.5C12,9.5,10.7,7.8,8.9,7.2z M4,2.5C4,1.7,4.7,1,5.5,1h1C7.3,1,8,1.7,8,2.5v4c0,0.2,0,0.4-0.1,0.6 l0.1,0L7.9,7.3C7.6,7.8,7.1,8.2,6.5,8.2h-1c-0.6,0-1.1-0.4-1.4-0.9L4.1,7.1l0.1,0C4,6.9,4,6.7,4,6.5V2.5z M11,12H1v-0.5 c0-1.6,1-2.9,2.4-3.4c0.5,0.7,1.2,1.1,2.1,1.1h1c0.8,0,1.6-0.4,2.1-1.1C10,8.5,11,9.9,11,11.5V12z" />
						</svg>
					</label>
					<input v-model="login" class="lf--input" placeholder="Username" type="text">
				</div>
				<div class="flex-row">
					<label class="lf--label" for="password">
						<svg x="0px" y="0px" width="15px" height="5px">
							<g>
								<path fill="#B1B7C4"
									  d="M6,2L6,2c0-1.1-1-2-2.1-2H2.1C1,0,0,0.9,0,2.1v0.8C0,4.1,1,5,2.1,5h1.7C5,5,6,4.1,6,2.9V3h5v1h1V3h1v2h1V3h1 V2H6z M5.1,2.9c0,0.7-0.6,1.2-1.3,1.2H2.1c-0.7,0-1.3-0.6-1.3-1.2V2.1c0-0.7,0.6-1.2,1.3-1.2h1.7c0.7,0,1.3,0.6,1.3,1.2V2.9z" />
							</g>
						</svg>
					</label>
					<input v-model="password" class="lf--input" placeholder="Password" type="password">
				</div>
				<input class="lf--submit" type="submit" value="LOGIN">
				<p class="error-text" v-if="error">{{ error }}</p>
			</form>
		</div>
	</teleport>
</template>

<script setup>
import { ref } from 'vue'
import { useStore } from 'vuex'
import { useRouter } from 'vue-router'
import { errorToText } from "@/utils/utils";

const store = useStore();
const router = useRouter();

const error = ref('');
const login = ref('');
const password = ref('');

async function tryLogin() {
	try {
		await store.dispatch('login', { login: login.value, password: password.value });
		error.value = '';
		await router.push("/");
	}
	catch (err) {
		error.value = errorToText(err);
	}
}
</script>

<style>

.login-modal {
	display: flex;
	align-items: center;
	position: fixed;
	justify-content: center;
	flex-direction: column;
	z-index: 1;
	left: 0;
	top: 0;
	width: 100%;
	height: 100%;
	overflow: auto;
	padding-top: 40px;
	padding-bottom: 40px;
	background-color: #f5f5f5;
}

.login-form {
	width: 100%;
	padding: 2em;
	position: relative;
	box-sizing: border-box;
	background: rgba(0, 0, 0, 0.15);
}

.login-form:before {
	content: "";
	position: absolute;
	top: -2px;
	left: 0;
	height: 2px;
	width: 100%;
	background: linear-gradient(to right, #35c3c1, #00d6b7);
}

@media screen and (min-width: 600px) {
	.login-form {
		width: 50vw;
		max-width: 500px;
	}
}

.flex-row {
	display: flex;
	margin-bottom: 1em;
}

.lf--label {
	width: 2em;
	display: flex;
	align-items: center;
	justify-content: center;
	background: #f5f6f8;
	cursor: pointer;
}

.lf--input {
	flex: 1;
	padding: 8px;
	border: 0;
	color: #8f8f8f;
	font-size: 1rem;
}

.lf--input:focus {
	outline: none;
	transition: transform 0.15s ease;
}

.lf--submit {
	display: block;
	padding: 8px;
	width: 100%;
	background: linear-gradient(to right, #35c3c1, #00d6b7);
	border: 0;
	color: #fff;
	cursor: pointer;
	font-weight: 600;
	text-shadow: 0 1px 0 rgba(0, 0, 0, 0.2);
}

.lf--submit:focus {
	background: linear-gradient(to right, #32b6b4, #03c7ac);
}

.lf--input::placeholder {
	color: #8f8f8f;
}

.error-text {
	text-align: center;
	margin: 8px 0 0 0;
	color: #bb2626;
	white-space: pre-wrap;
	word-break: break-word;
	line-height: 1.25;
}
</style>
