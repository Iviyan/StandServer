// arrayUtils

Array.prototype.minBy = function(fn) {
	return this.extremumBy(fn, Math.min);
};

Array.prototype.maxBy = function(fn) {
	return this.extremumBy(fn, Math.max);
};

Array.prototype.extremumBy = function(pluck, extremum) {
	return this.reduce((best, next) => {
		let pair = [ pluck(next), next ];
		if (!best)
			return pair;
		else if (extremum.apply(null, [ best[0], pair[0] ]) === best[0])
			return best;
		else
			return pair;
	},null)[1];
}

// timeUtils

Date.prototype.addDays = function (days) {
	let date = new Date(this.valueOf());
	date.setDate(date.getDate() + days);
	return date;
}
