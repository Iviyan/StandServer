function isNullOrUndef(value) {
	return value === null || value === undefined;
}

/**
 * Measurements decimation for chart
 * @param {*[]} data measurements
 * @param {string} xAxis x axis property key
 * @param {string} yAxis y axis property key
 * @param {number} start start index
 * @param {number} count number of elements
 * @param {number} availableWidth chart available width
 * @param {function} skipIf predicate
 * @returns {*[]}
 */
export function decimation({ data, xAxis = 'x', yAxis = 'y', start, count, availableWidth, skipIf }) {
	if (count <= 0) return [];
	if (count === 1) return [ data[start] ];
	if (count === 2) return data.slice(start, start + 2);

	let avgX = 0, avgY = 0, countX = 0;
	let prevX, minIndex, maxIndex, startIndex, minY, maxY;
	const decimated = [];
	const endIndex = start + count - 1;

	const from = data[start][xAxis];
	const to = data[endIndex][xAxis];
	const interval = to - from;

	const skip = skipIf
		? point => isNullOrUndef(point[yAxis]) || skipIf && skipIf(point)
		: point => isNullOrUndef(point[yAxis]);

	let i = start;
	for (; i < start + count; ++i) {
		let point = data[i];
		if (skip(point)) decimated.push(point);
		else break;
	}

	minIndex = maxIndex = startIndex = i;

	for (; i < endIndex; ++i) {
		let point = data[i];
		const x = ((point[xAxis] - from) / interval * availableWidth) | 0;
		const y = point[yAxis];

		let skipNextPoint = skip(data[i + 1]);

		if (x === prevX && !skipNextPoint) {
			if (y < minY) {
				minY = y;
				minIndex = i;
			} else if (y > maxY) {
				maxY = y;
				maxIndex = i;
			}
			avgX = (countX * avgX + point[xAxis]) / ++countX;
			avgY = ((countX - 1) * avgY + point[xAxis]) / countX;
		} else {
			if (i === startIndex && !skipNextPoint || countX > 0) { // right after the new x
				decimated.push(data[Math.min(minIndex, maxIndex)]);
				if (minIndex !== maxIndex)
					decimated.push(data[Math.max(minIndex, maxIndex)]);
			}

			prevX = x;
			countX = 0;

			if (skipNextPoint) {
				let skipRangeStart = i;
				for (i += 2; i <= endIndex; ++i) {
					if (!skip(data[i]) && (i < endIndex ? !skip(data[i + 1]) : true)) break;
				}
				decimated.push(...data.slice(skipRangeStart, i + 1));
			}

			if (i < endIndex) {
				minY = maxY = data[i + 1][yAxis];
				minIndex = maxIndex = startIndex = i + 1;
			}
		}
	}

	if (i === endIndex)
		decimated.push(data[endIndex])

	return decimated;
}
