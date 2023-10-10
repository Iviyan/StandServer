import { createRouter, createWebHistory } from 'vue-router'
import store from '../store'

import LoginView from '../views/Login.vue'

// Lazy loading
const DashboardView = () => import(/* webpackChunkName: "dashboard" */ '../views/Dashboard.vue')
const StandView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/StandView.vue')
const SampleView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/SampleView.vue')
const UsersView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/UsersView.vue')
const ConfigurationView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/ConfigurationView.vue')

const isNotAuthenticated = (to, from) => {
	console.debug(store);
	if (store.getters.isAuth)
		return '/';
}

const isAuthenticated = (to, from) => {
	if (!store.getters.isAuth && to.name !== 'Login') {
		return { name: 'login' }
	}
}

const isAdmin = (to, from) => isAuthenticated(to, from) ?? store.getters.isAdmin;

const routes = [
	{
		path: '/login',
		name: 'login',
		component: LoginView,
		beforeEnter: isNotAuthenticated,
		meta: { title: 'Вход' }
	},
	{
		path: '/',
		name: 'dashboard',
		component: DashboardView,
		beforeEnter: isAuthenticated,
		children: [
			{
				name: 'stand',
				path: '/stand/:id(-?\\d+)',
				component: StandView,
				props: route => ({ id: Number.parseInt(route.params.id) }),
			},
			{
				name: 'sample',
				path: 'samples/:id(-?\\d+)',
				component: SampleView,
				props: route => ({ id: Number(route.params.id) })
			},
			{
				path: 'users', name: 'users',
				component: UsersView, meta: { title: 'Пользователи' },
				beforeEnter: isAdmin
			},
			{
				path: 'configuration', name: 'configuration',
				component: ConfigurationView, meta: { title: 'Настройки' }
			},
		]
	},
	{ path: '/:catchAll(.*)*', redirect: { name: 'dashboard' } }
]

const router = createRouter({
	history: createWebHistory(),
	routes
})

router.afterEach((to, from) => {
	if (to.meta.title) document.title = to.meta.title;
})

export default router
