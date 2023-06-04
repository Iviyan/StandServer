import { RequestError } from '@/exceptions';

/**
 * Unauthorized POST request to API
 * @param {string} url
 * @param {object} data
 * @returns {Promise<Response>}
 */
export async function post(url = '', data = {}) {
	return await fetch(url, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json'
		},
		body: JSON.stringify(data)
	});
}

/**
 * Unauthorized POST request to API with parsing response
 * @param {string} url
 * @param {object} data
 * @returns {Promise<object>}
 */
export async function postj(url = '', data = {}) {
	const response = await post(url, data);
	if (!response.ok) {
		console.log('Request execution error\n', response);
		if (response.headers.get('content-type')?.includes('application/problem+json'))
			throw new RequestError(response, await response.json());
		throw new RequestError(response);
	}
	return await response.json();
}

/**
 * Unauthorized GET request to API
 * @param {string} url
 * @param {object} data query params
 * @returns {Promise<Response>}
 */
export async function get(url = '', data = {}) {
	const response = await fetch(url + '?' + new URLSearchParams(data), { method: 'GET' });
	return await response.json();
}

/**
 * Unauthorized GET request to API with parsing response
 * @param {string} url
 * @param {object} data query params
 * @returns {Promise<Response>}
 */
export async function getj(url = '', data = {}) { return await (await get(url, data)).json(); }
