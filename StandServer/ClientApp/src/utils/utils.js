import { RequestError } from "@/exceptions";
import store from '../store'
import { computed } from "vue";

const configuration = computed(() => store.state.dashboard.configuration);

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

export const isSampleOk = measurement => !(measurement.state !== 'work' && measurement.i > configuration.value.offSampleMaxI);

export function errorToText(err) {
	if (!(err instanceof RequestError)) return 'Неизвестная ошибка';
	if (!err.rfc7807) return 'Ошибка запроса';
	let errors = err.response.errors;
	console.log(errors);
	if (errors === undefined || isEmpty(errors)) return err.title;
	let result = err.title;
	for (let prop in errors) {
		console.log(errors[prop]);
		result += '\n';
		result += errors[prop].join("\n");
	}
	return result;
}
