import { DateTime } from "luxon";

/**
 * Format UNIX in "dd.MM.yyyy" format
 * @param {number} s
 * @returns {string}
 */
export const millisToDate = s => DateTime.fromMillis(s).toFormat("dd.MM.yyyy");

/**
 * Format UNIX in "dd.MM.yyyy HH:mm:ss" format
 * @param {number} s
 * @returns {string}
 */
export const millisToDateTime = s => DateTime.fromMillis(s).toFormat("dd.MM.yyyy HH:mm:ss");

/**
 * Convert seconds to interval like 5000 -> 01:23:20
 * @param {number} s seconds
 * @returns {`${string}:${string}:${string}`}
 */
export const secondsToInterval = s => `${ String(s / 3600 | 0).padStart(2, '0') }:${ String(s % 3600 / 60 | 0).padStart(2, '0') }:${ String(s % 60).padStart(2, '0') }`;

/**
 * Round Date to day
 * @param {Date} date
 * @returns {Date}
 */
export const floorToDay = date => {
	date.setMilliseconds(0);
	date.setSeconds(0);
	date.setMinutes(0);
	date.setHours(0);
	return date;
}
