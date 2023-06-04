export class ReverseIterable {
	constructor(arr) { this.arr = arr; }

	* [Symbol.iterator]() {
		const arr = this.arr;
		for (let i = arr.length - 1; i >= 0; i--) yield arr[i];
	}

	getIterable(isReversedOrder) {
		return isReversedOrder ? this : this.arr;
	}
}

/**
 * Get reverse iterator for array
 * @param {*[]} arr
 * @param {boolean} b If false, the function does nothing.
 * @returns {ReverseIterable|*}
 */
export const reverseIterate = (arr, b = true) => b === true ? new ReverseIterable(arr) : arr;

/**
 * Check arrays equality
 * @param {*[]} a
 * @param {*[]} b
 * @returns {boolean}
 */
export function arraysEqual(a, b) {
	if (a === b) return true;
	if (a == null || b == null) return false;
	if (a.length !== b.length) return false;

	for (let i = 0; i < a.length; ++i) {
		if (a[i] !== b[i]) return false;
	}
	return true;
}

/**
 * Get the elements present in A and which are not in B.
 * @param {*[]} setA
 * @param {*[]} setB
 * @returns {any[]}
 */
export function difference(setA, setB) {
	const _difference = new Set(setA);
	for (const elem of setB)
		_difference.delete(elem);
	return Array.from(_difference);
}
