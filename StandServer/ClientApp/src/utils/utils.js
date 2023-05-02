import { RequestError } from "@/exceptions";

export const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

export const objectMap = (obj, fn) =>
	Object.fromEntries(
		Object.entries(obj).map(
			([ k, v ], i) => [ k, fn(v, k, i) ]
		)
	)

export function isEmpty(obj) {
	for (let i in obj) return false;
	return true;
}

export const isSampleOk = measurement => !(measurement.state !== 'work' && measurement.i > 200);

export const errorToText = err => err instanceof RequestError
	? err.rfc7807 ? err.message : 'Ошибка запроса'
	: 'Неизвестная ошибка';
