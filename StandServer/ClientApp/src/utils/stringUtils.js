/**
 * Delete a character at the ends of a string
 * @param {string} str
 * @param {string} ch
 * @returns {string}
 */
export function trim(str, ch) {
	let start = 0,
		end = str.length;

	while (start < end && str[start] === ch)
		++start;

	while (end > start && str[end - 1] === ch)
		--end;

	return (start > 0 || end < str.length) ? str.substring(start, end) : str;
}

/**
 * Format number like 12 -> 00000012
 * @param {number} id
 * @returns {string}
 */
export const sampleIdFormat = (id) =>
	id >= 0
		? String(id).padStart(8, '0')
		: '-' + String(-id).padStart(8, '0');
