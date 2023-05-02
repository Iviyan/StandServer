import { createRouter, createWebHistory } from 'vue-router'
import store from '../store'

import LoginView from '../views/Login.vue'

// Lazy loading
const DashboardView = () => import(/* webpackChunkName: "dashboard" */ '../views/Dashboard.vue')
const HomeView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/HomeView.vue')
const SampleView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/SampleView.vue')
const UsersView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/UsersView.vue')

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
			{ path: '', name: 'home', component: HomeView, meta: { title: 'Главная' } },
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
		]
	},
	{ path: '/:catchAll(.*)*', redirect: '/' }
]

const router = createRouter({
	history: createWebHistory(),
	routes
})

router.afterEach((to, from) => {
	if (to.meta.title) document.title = to.meta.title;
})

export default router
