import { postj } from '@/utils/fetch'
import jwt_decode from "jwt-decode"
import { call_post } from '@/utils/api';

export default {
	state: {
		jwt: null,
		jwtData: null
	},
	getters: {
		isAuth: state => !!state.jwt,
		jwtData: state => state.jwtData,
		isAdmin: state => state.jwtData?.IsAdmin?.toLowerCase() === 'true',
	},
	mutations: {
		auth(state, value) {
			console.log('auth, ', value)
			state.jwt = value;
			if (!!value) {
				state.jwtData = jwt_decode(value);
				localStorage.setItem('jwt', value);
			} else
				localStorage.removeItem('jwt');
		},
		logout(state) {
			state.jwt = state.jwtData = null;
			localStorage.removeItem('jwt');
		},
	},
	actions: {
		initAuth(ctx) {
			ctx.commit('auth', localStorage.getItem('jwt'))
		},
		async login(ctx, { login, password }) {
			let res = await postj('/login', {
				login: login,
				password: password
			});

			let jwt = res.access_token;
			console.log('jwt: ', jwt);
			console.assert(!!jwt, "JWT must be not null here");
			ctx.commit('auth', jwt);
		},
		async logout({ commit }) {
			await call_post('/logout');
			commit('logout');
		}
	}
}
