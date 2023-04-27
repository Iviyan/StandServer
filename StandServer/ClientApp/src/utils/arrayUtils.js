export class ReverseIterable {
	constructor(arr) { this.arr = arr; }

	*[Symbol.iterator]() {
		const arr = this.arr;
		for (let i = arr.length - 1; i >= 0; i--) yield arr[i];
	}

	getIterable(isReversedOrder) {
		return isReversedOrder ? this : this.arr;
	}
}

export const reverseIterate = (arr, b = true) => b !== false ? new ReverseIterable(arr) : arr;

export function arraysEqual(a, b) {
	if (a === b) return true;
	if (a == null || b == null) return false;
	if (a.length !== b.length) return false;

	for (let i = 0; i < a.length; ++i) {
		if (a[i] !== b[i]) return false;
	}
	return true;
}

export function difference(setA, setB) {
	const _difference = new Set(setA);
	for (const elem of setB)
		_difference.delete(elem);
	return Array.from(_difference);
}
