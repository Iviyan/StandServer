import { callGet } from '@/utils/api';
import { arraysEqual, difference } from '@/utils/arrayUtils';

export default {
	state: {
		sampleIds: [],
		configuration: {},
		lastMeasurements: {},
		lastMeasurementsInitialized: false
	},
	getters: {
		lastMeasurementTime(state) {
			let result = {};
			for (let standId in state.lastMeasurements) {
				for (let sampleId in state.lastMeasurements[standId]) {
					result[standId] = state.lastMeasurements[standId][sampleId].at(-1).time;
					break;
				}
			}
			return result;
		},
		standIds: state => Object.keys(state.lastMeasurements).map(Number)
	},
	mutations: {
		setSampleIds(state, value) { state.sampleIds = value; },
		setConfiguration(state, value) { state.configuration = value; },
		updateConfiguration(state, patch) { Object.assign(state.configuration, patch); },
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
		setLastMeasurementsInitialized(state, value) { state.lastMeasurementsInitialized = value; },
	},
	actions: {
		async loadSampleIds({ commit, state }) {
			let sampleIds = await callGet('/api/samples');
			commit('setSampleIds', sampleIds);
			console.debug("sample ids: ", state.sampleIds);
		},
		async loadConfiguration({ commit, state }) {
			let configuration = await callGet('/api/configuration');
			commit('setConfiguration', configuration);
			console.debug("configuration: ", state.configuration);
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
