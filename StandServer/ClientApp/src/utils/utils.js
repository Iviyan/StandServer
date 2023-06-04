import { RequestError } from "@/exceptions";
import store from '../store'
import { computed } from "vue";

const configuration = computed(() => store.state.dashboard.configuration);

export const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

/**
 *
 * @param {object} obj
 * @param {function} fn (value, key, index) => new value
 * @returns {object}
 */
export const objectMap = (obj, fn) =>
	Object.fromEntries(
		Object.entries(obj).map(
			([ k, v ], i) => [ k, fn(v, k, i) ]
		)
	)

/**
 * Check if the object is empty
 * @param {object} obj
 * @returns {boolean}
 */
export function isEmpty(obj) {
	for (let i in obj) return false;
	return true;
}

/**
 * Check whether the sample current does not exceed the set value if the sample is in an inactive state
 * @param measurement
 * @returns {boolean}
 */
export const isSampleOk = measurement => !(measurement.state !== 'work' && measurement.i > configuration.value.offSampleMaxI);

/**
 * Convert RFC7807 to text representation of the error
 * @param {RequestError} err
 * @returns {string}
 */
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
