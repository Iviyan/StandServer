// arrayUtils

/**
 * Get min value by selector function
 * @param {function} fn
 * @returns {*}
 */
Array.prototype.minBy = function (fn) {
	return this.extremumBy(fn, Math.min);
};

/**
 * Get max value by selector function
 * @param {function} fn
 * @returns {*}
 */
Array.prototype.maxBy = function (fn) {
	return this.extremumBy(fn, Math.max);
};

/**
 * Get value by selector and condition functions
 * @param {function} pluck selector function
 * @param {function} extremum condition function
 * @returns {*}
 */
Array.prototype.extremumBy = function (pluck, extremum) {
	return this.reduce((best, next) => {
		let pair = [ pluck(next), next ];
		if (!best)
			return pair;
		else if (extremum.apply(null, [ best[0], pair[0] ]) === best[0])
			return best;
		else
			return pair;
	}, null)[1];
}

// timeUtils

/**
 * Add days to date
 * @param {Date} days
 * @returns {Date}
 */
Date.prototype.addDays = function (days) {
	let date = new Date(this.valueOf());
	date.setDate(date.getDate() + days);
	return date;
}
