import { callGet } from '@/utils/api';
import { arraysEqual, difference } from '@/utils/arrayUtils';

export default {
	state: {
		sampleIds: [],
		lastMeasurements: {},
		homeViewVisited: false,
		lastMeasurementsInitialized: false
	},
	getters: {
		lastMeasurementTime(state) {
			for (let sampleId in state.lastMeasurements)
				return state.lastMeasurements[sampleId].at(-1).time;
		}
	},
	mutations: {
		setSampleIds(state, value) { state.sampleIds = value; },
		newSampleIds(state, value) {
			for (let sampleId of value)
				if (!state.sampleIds.includes(sampleId))
					state.sampleIds.push(sampleId);
		},
		removeSample(state, value) {
			let index = state.sampleIds.indexOf(value);
			if (index >= 0) state.sampleIds.splice(index, 1);
		},
		setLastMeasurements(state, value) { state.lastMeasurements = value; },
		newMeasurements(state, measurements) {
			for (let { sampleId, ...measurement } of measurements) {
				let arr = state.lastMeasurements[sampleId];

				if (arr.length >= 20) arr.shift();
				arr.push(measurement);
			}
		},
		setHomeViewVisited(state, value) { state.homeViewVisited = value; },
		setLastMeasurementsInitialized(state, value) { state.lastMeasurementsInitialized = value; },
	},
	actions: {
		async loadSampleIds({ commit, state }) {
			let sampleIds = await callGet('/api/samples');
			commit('setSampleIds', sampleIds);
			console.debug("sample ids: ", state.sampleIds);
		},
		async loadLastMeasurements({ commit, state }) {
			let lastMeasurements = await callGet('/api/samples/last', { count: 20, sample_ids: 'active' });
			commit('setLastMeasurements', lastMeasurements);
			console.debug("last measurements: ", state.lastMeasurements);
		},
		async newMeasurements({ dispatch, commit, state }, measurements) {
			let newMeasurementsSampleIds = new Int32Array(measurements.map(m => m.sampleId));
			let oldMeasurementsSampleIds = new Int32Array(Object.keys(measurements)); // TODO: check it
			newMeasurementsSampleIds.sort();
			oldMeasurementsSampleIds.sort();

			if (!arraysEqual(oldMeasurementsSampleIds, newMeasurementsSampleIds)) {
				await dispatch('loadLastMeasurements');

				let newSampleIds = difference(newMeasurementsSampleIds, oldMeasurementsSampleIds);
				if (newSampleIds.length > 0)
					commit('newSampleIds', newSampleIds);
			} else {
				commit('newMeasurements', measurements);
			}
		},
	}
}
