import { RequestError } from '@/exceptions';
import {Mutex} from 'async-mutex';
import store from '../store'
import { trim } from './stringUtils';
import { isEmpty } from './utils'

async function refreshToken() {
	const release = await refreshTokenMutex.acquire();
	try {
		const response = await fetch('/api/refresh-token', {
			method: 'POST'
		});

		if (!response.ok) {
			const text = await response.text();
			if (text.length === 0) {
				console.error('Refresh token request error.\n', response);
			} else {
				const json = JSON.parse(text);
				console.error('Refresh token update error.\n', response, '\n', json);
			}
			store.commit('logout');
			return false;
		}

		const json = await response.json();
		store.commit('auth', json.accessToken);
		console.debug('Refresh token update: ', json);

		return true;
	} finally { release(); }
}

async function authorizedGet(url = '', data = {}) {
	return await fetch(url + (isEmpty(data) ? '' : '?' + new URLSearchParams(data)), {
		method: 'GET',
		headers: {
			'Authorization': `Bearer ${store.state.auth.jwt}`,
		}
	});
}

async function authorizedPost(url = '', data = {}) {
	return await fetch(url, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			'Authorization': `Bearer ${store.state.auth.jwt}`,
		},
		body: JSON.stringify(data)
	});
}

async function authorizedDelete(url = '', data = {}) {
	return await fetch(url, {
		method: 'DELETE',
		headers: {
			'Content-Type': 'application/json',
			'Authorization': `Bearer ${store.state.auth.jwt}`,
		},
		body: JSON.stringify(data)
	});
}

async function authorizedPatch(url = '', data = {}) {
	return await fetch(url, {
		method: 'PATCH',
		headers: {
			'Content-Type': 'application/json',
			'Authorization': `Bearer ${store.state.auth.jwt}`,
		},
		body: JSON.stringify(data)
	});
}

let methods = {
	'get': authorizedGet,
	'post': authorizedPost,
	'delete': authorizedDelete,
	'patch': authorizedPatch,
}

// To avoid multiple refresh-token calls.
const refreshTokenMutex = new Mutex();

export async function call(url = '', method = 'GET', data = {}) {
	if (store.getters.jwtData.exp < Date.now() / 1000 && !refreshTokenMutex.isLocked()) {
		let res = await refreshToken();
		if (!res) throw new Error('Refresh token update error');
	}
	await refreshTokenMutex.waitForUnlock();

	let func = methods[method.toLowerCase()];
	if (!func) throw new Error('Refresh token update error');

	let response = await func(url, data);

	if (response.status === 401) {
		if (!refreshTokenMutex.isLocked()) {
			let res = await refreshToken();
			if (!res) throw new Error('Refresh token update error');
		} else await refreshTokenMutex.waitForUnlock();
		response = await func(url, data);
	}
	if (!response.ok) {
		console.error('Request execution error\n', response);
		if (response.headers.get('content-type')?.includes('application/problem+json')) {
			let json = await response.json();
			console.info('Request response: ', json)

			throw new RequestError(response, json);
		}
		throw new RequestError(response);
	}

	let text = await response.text();
	return text.length > 0 ? JSON.parse(text) : text;
}

export async function callGet(url = '', data = {}) { return await call(url, 'get', data); }
export async function callPost(url = '', data = {}) { return await call(url, 'post', data); }
export async function callDelete(url = '', data = {}) { return await call(url, 'delete', data); }
export async function callPatch(url = '', data = {}) { return await call(url, 'patch', data); }

function extractFilename(contentDisposition, defaultFilename) {
	if (!contentDisposition) return defaultFilename;

	contentDisposition = contentDisposition.split(';').map(s=>s.trim().split('='));
	let filenameUtf8 = contentDisposition.find(e=>e[0] === "filename*");
	if (filenameUtf8) {
		filenameUtf8 = trim(filenameUtf8[1], '"');
		return decodeURI(filenameUtf8.startsWith("UTF-8''") ? filenameUtf8.substring(7) : filenameUtf8);
	}
	let filename = contentDisposition.find(e=>e[0] === "filename");
	if (filename) {
		return trim(filename[1], '"');
	}
	return defaultFilename;
}

export async function downloadFile(url, data = {}, defaultFileName = null) {
	if (store.getters.jwtData.exp < Date.now() / 1000 && !refreshTokenMutex.isLocked()) {
		let res = await refreshToken();
		if (!res) throw new Error('Refresh token update error');
	}
	await refreshTokenMutex.waitForUnlock();

	const fetchFile = () =>  fetch(url + (isEmpty(data) ? '' : '?' + new URLSearchParams(data)), {
		method: 'GET',
		headers: {
			'Authorization': `Bearer ${store.state.auth.jwt}`,
		}
	});
	let response = await fetchFile();

	if (response.status === 401) {
		if (!refreshTokenMutex.isLocked()) {
			let res = await refreshToken();
			if (!res) throw new Error('Refresh token update error');
		} else await refreshTokenMutex.waitForUnlock();
		response = await fetchFile();
	}
	if (!response.ok) {
		console.error('Request execution error\n', response);
		if (response.headers.get('content-type')?.includes('application/problem+json')) {
			let json = await response.json();
			console.info('Request response: ', json)

			throw new RequestError(response, json);
		}
		throw new RequestError(response);
	}

	let blob = await response.blob();
	let objectUrl = window.URL.createObjectURL(blob);

	let filename = extractFilename(response.headers.get('Content-Disposition'), defaultFileName);

	let anchor = document.createElement("a");
	document.body.appendChild(anchor);
	anchor.href = objectUrl;
	anchor.download = filename;
	anchor.click();

	window.URL.revokeObjectURL(objectUrl);
}
