import '@/utils/prototypeModifications' // https://mathiasbynens.be/notes/prototypes

import { createApp } from 'vue'
import App from './App.vue'
import store from './store'
import router from './router'
import { createVfm } from 'vue-final-modal'

import "izitoast/dist/css/iziToast.min.css"
import 'vue-final-modal/style.css'

const vfm = createVfm()

createApp(App)
	.use(store)
	.use(router)
	.use(vfm)
	.mount('#app')
