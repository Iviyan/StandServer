function isNullOrUndef(value) {
	return value === null || value === undefined;
}

export function decimation({ data, xAxis = 'x', yAxis = 'y', start, count, availableWidth, skipIf }) {
	if (count <= 0) return [];
	if (count === 1) return [data[start]];
	if (count === 2) return data.slice(start, start + 2);

	let avgX = 0, avgY = 0, countX = 0;
	let prevX, minIndex, maxIndex, startIndex, minY, maxY;
	const decimated = [];
	const endIndex = start + count - 1;

	const from = data[start][xAxis];
	const to = data[endIndex][xAxis];
	const interval = to - from;

	let i = start;
	for (; i < start + count; ++i) {
		let point = data[i];
		if (isNullOrUndef(point?.[yAxis]))
			decimated.push(point);
		else break;
	}

	decimated.push(data[i++])

	for (; i < endIndex; ++i) {
		let point = data[i];
		const x = ((point[xAxis] - from) / interval * availableWidth) | 0;
		const y = point[yAxis];

		let isPenultimateElement = i + 1 === endIndex;
		let isNextPointNull = isNullOrUndef(data[i + 1][yAxis]) || skipIf && skipIf(data[i + 1]);

		if (x === prevX && !isNextPointNull) {
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
			if (i === startIndex + 1 && !isNextPointNull || countX > 0) { // right after the new x
				decimated.push(data[Math.min(minIndex, maxIndex)]);
				if (minIndex !== maxIndex)
					decimated.push(data[Math.max(minIndex, maxIndex)]);
			}

			prevX = x;
			countX = 0;
			minY = maxY = y;
			minIndex = maxIndex = startIndex = i;

			if (isNextPointNull) {
				decimated.push(...data.slice(i, i + 2 + (isPenultimateElement ? 0 : 1) ));
				i += 2 + (isPenultimateElement ? 0 : 1);
			}
		}
	}

	if (i === endIndex)
		decimated.push(data[endIndex])

	return decimated;
}

/*function lttbDecimation({ data, xAxis = 'x', yAxis = 'y', start, count, samples }) {
	// There are less points than the threshold, returning the whole array
	if (samples >= count) {
		return data.slice(start, start + count);
	}

	const decimated = [];

	const bucketWidth = (count - 2) / (samples - 2);
	let sampledIndex = 0;
	const endIndex = start + count - 1;
	// Starting from offset
	let a = start;
	let i, maxAreaPoint, maxArea, area, nextA;

	decimated[sampledIndex++] = data[a];

	for (i = 0; i < samples - 2; i++) {
		let avgX = 0;
		let avgY = 0;
		let j;

		// Adding offset
		const avgRangeStart = Math.floor((i + 1) * bucketWidth) + 1 + start;
		const avgRangeEnd = Math.min(Math.floor((i + 2) * bucketWidth) + 1, count) + start;
		const avgRangeLength = avgRangeEnd - avgRangeStart;

		for (j = avgRangeStart; j < avgRangeEnd; j++) {
			avgX += data[j][xAxis];
			avgY += data[j][yAxis];
		}

		avgX /= avgRangeLength;
		avgY /= avgRangeLength;

		// Adding offset
		const rangeOffs = Math.floor(i * bucketWidth) + 1 + start;
		const rangeTo = Math.min(Math.floor((i + 1) * bucketWidth) + 1, count) + start;
		const {x: pointAx, y: pointAy} = data[a];

		// Note that this is changed from the original algorithm which initializes these
		// values to 1. The reason for this change is that if the area is small, nextA
		// would never be set and thus a crash would occur in the next loop as `a` would become
		// `undefined`. Since the area is always positive, but could be 0 in the case of a flat trace,
		// initializing with a negative number is the correct solution.
		maxArea = area = -1;

		for (j = rangeOffs; j < rangeTo; j++) {
			area = 0.5 * Math.abs(
				(pointAx - avgX) * (data[j].y - pointAy) -
				(pointAx - data[j].x) * (avgY - pointAy)
			);

			if (area > maxArea) {
				maxArea = area;
				maxAreaPoint = data[j];
				nextA = j;
			}
		}

		decimated[sampledIndex++] = maxAreaPoint;
		a = nextA;
	}

	// Include the last point
	decimated[sampledIndex++] = data[endIndex];

	return decimated;
}*/


// Slightly modified decimation algorithm from chart.js

/*function isNullOrUndef(value) {
	return value === null || typeof value === 'undefined';
}

export function minMaxDecimation({ data, xAxis = 'x', yAxis = 'y', start, count, availableWidth }) {
	if (count <= 0) return [];
	if (count === 1) return [data[start]];
	if (count === 2) return data.slice(start, start + 2);

	let avgX = 0;
	let countX = 0;
	let prevX, minIndex, maxIndex, startIndex, minY, maxY;
	const decimated = [];
	let endIndex = start + count - 1;
	if (endIndex < 0) endIndex = 0;

	const xMin = data[start][xAxis];
	const xMax = data[endIndex][xAxis];
	const interval = xMax - xMin;

	decimated.push(data[start])

	for (let i = start + 1; i < start + count - 1; ++i) {
		let point = data[i];
		let x = (point[xAxis] - xMin) / interval * availableWidth;
		let y = point[yAxis];
		if (y == null) {
			decimated.push(point);
			continue;
		}
		const truncX = x | 0;

		if (truncX === prevX) {
			// Determine `minY` / `maxY` and `avgX` while we stay within same x-position
			if (y < minY) {
				minY = y;
				minIndex = i;
			} else if (y > maxY) {
				maxY = y;
				maxIndex = i;
			}
			// For first point in group, countX is `0`, so average will be `x` / 1.
			// Use point[xName] here because we're computing the average data `x` value
			avgX = (countX * avgX + point[xAxis]) / ++countX;
		} else {
			// Push up to 4 points, 3 for the last interval and the first point for this interval
			const lastIndex = i - 1;

			if (!isNullOrUndef(minIndex) && !isNullOrUndef(maxIndex)) {
				// The interval is defined by 4 points: start, min, max, end.
				// The starting point is already considered at this point, so we need to determine which
				// of the other points to add. We need to sort these points to ensure the decimated data
				// is still sorted and then ensure there are no duplicates.
				const intermediateIndex1 = Math.min(minIndex, maxIndex);
				const intermediateIndex2 = Math.max(minIndex, maxIndex);

				if (intermediateIndex1 !== startIndex && intermediateIndex1 !== lastIndex) {
					decimated.push({
						...data[intermediateIndex1],
						//[xAxis]: avgX,
					});
				}
				if (intermediateIndex2 !== startIndex && intermediateIndex2 !== lastIndex && intermediateIndex2 !== intermediateIndex1) {
					decimated.push({
						...data[intermediateIndex2],
						//[xAxis]: avgX
					});
				}
			}

			// lastIndex === startIndex will occur when a range has only 1 point which could
			// happen with very uneven data
			if (i > 0 && lastIndex !== startIndex) {
				// Last point in the previous interval
				decimated.push(data[lastIndex]);
			}

			// Start of the new interval
			decimated.push(point);
			prevX = truncX;
			countX = 0;
			minY = maxY = y;
			minIndex = maxIndex = startIndex = i;
		}
	}

	decimated.push(data[endIndex])

	return decimated;
}*/


// Not a particularly successful attempt to create a data decimation algorithm

/*
dataCount.value = data.length;

// let from = binarySearch(data, e => x1 < e[props.xAxis]) - 1 - 1;
// if (from < 0) from = 0;

// let to = binarySearch(data, e => x2 < e[props.xAxis]) - 1 + 1;
// if (to === data.length) to = data.length - 1;

let from = 0, to = 0;
for (let i = 0; (data[i] ? data[i][props.xAxis] <= x1 : true) && i < data.length; i++)
	if (data[i]?.[props.xAxis]) from = i;
for (let i = from; (data[i] ? data[i][props.xAxis] <= x2 : true) && i < data.length; i++)
	if (data[i]?.[props.xAxis]) to = i;

let segmentNotNullValuesCount = 0;
for (let i = from; i <= to; i++)
	if (data[i]) segmentNotNullValuesCount++;

let segmentLength = dataSegmentCount.value = to - from + 1;
let segmentNullValuesCount = segmentLength - segmentNotNullValuesCount;

const decimateTo = (chart.width / 4) | 0;

console.log(`${from} - ${to} => ${decimateTo} (w: ${chart.width})`);

if (segmentLength - segmentNullValuesCount <= decimateTo) {
	dataVisibleCount.value = segmentLength;
	return data.slice(from, to + 1);
}

let decimatedData;


let score = [];//new Array(data.length - 2);
for (let dataIndex = 1; dataIndex < data.length - 1; dataIndex++) {
	if (!data[dataIndex - 1]) continue;
	if (!data[dataIndex]) { dataIndex += 1; continue; }
	if (!data[dataIndex + 1]) { dataIndex += 2; continue; }

	score.push({
		i: dataIndex,
		v: Math.abs(data[dataIndex - 1][props.yAxis] - data[dataIndex][props.yAxis])
			+ Math.abs(data[dataIndex + 1][props.yAxis] - data[dataIndex][props.yAxis])
	});
}

score.sort((a, b) => a.v - b.v);
score.length = segmentNotNullValuesCount - decimateTo;
score = score.map(e => e.i).sort((a, b) => a - b);

if (score.length === 1) data.splice(score[0], 1);
else {
	decimatedData = new Array(data.length - score.length);
	for (let dataIndex = 0, decDataIndex = 0, scoreIndex = 0, skipIndex = score[0]; dataIndex < data.length; dataIndex++) {
		if (skipIndex === dataIndex) {
			skipIndex = score[++scoreIndex];
			continue;
		}
		decimatedData[decDataIndex++] = data[dataIndex];
	}
}

dataVisibleCount.value = decimatedData.length;

console.log('decimated data: ', decimatedData);
return decimatedData;
*/
