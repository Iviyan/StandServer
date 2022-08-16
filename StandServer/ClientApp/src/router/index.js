import { createRouter, createWebHistory } from 'vue-router'
import store from '../store'

import LoginView from '../views/Login.vue'

const DashboardView = () => import(/* webpackChunkName: "dashboard" */ '../views/Dashboard.vue')
const HomeView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/HomeView.vue')
const SampleView = () => import(/* webpackChunkName: "dashboard" */ '../views/dashboard/SampleView.vue')

const ifNotAuthenticated = (to, from) => {
	console.log(store);
	if (store.getters.isAuth)
		return '/';
}

const ifAuthenticated = (to, from) => {
	if (!store.getters.isAuth && to.name !== 'Login') {
		return { name: 'login' }
	}
}

const routes = [
	{
		path: '/login',
		name: 'login',
		component: LoginView,
		beforeEnter: ifNotAuthenticated,
		meta: { title: 'Вход' }
	},
	{
		path: '/',
		name: 'dashboard',
		component: DashboardView,
		beforeEnter: ifAuthenticated,
		children: [
			{ path: '', name: 'home', component: HomeView, meta: { title: 'Главная' } },
			{
				path: 'samples/:id(\\d+)',
				component: SampleView,
				props: route => ({ id: Number(route.params.id) })
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
