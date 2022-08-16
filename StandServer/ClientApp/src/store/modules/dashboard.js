import { call_get } from '@/utils/api';

export default {
	state: {
		sampleIds: [],
		stateHistory: [],
		lastMeasurements: {},
		homeViewVisited: false,
	},
	getters: {},
	mutations: {
		setSampleIds(state, value) { state.sampleIds = value; },
		removeSample(state, value) {
			let index = state.sampleIds.indexOf(value);
			if (index >= 0) state.sampleIds.splice(index, 1);
		},
		setStateHistory(state, value) { state.stateHistory = value; },
		setLastMeasurements(state, value) {
			state.lastMeasurements = value;
			state.lastMeasurementsLoaded = true;
		},
		//setSampleLastMeasurements(state, { table, value }) {state.lastMeasurements[table] = value; },
		newMeasurement(state, { sampleId, measurement }) {
			if (!state.sampleIds.includes(sampleId)) {
				state.lastMeasurements[sampleId] = [ measurement ];
				state.sampleIds.push(sampleId);
				return;
			}

			let arr = state.lastMeasurements[sampleId];
			console.assert(arr !== undefined, "last measurements array must be not null here");
			if (arr === undefined) return;

			if (arr.length >= 20) arr.shift();
			arr.push(measurement);
		},
		setHomeViewVisited(state, value) { state.homeViewVisited = value; }
	},
	actions: {
		async loadSampleIds({ commit, state }) {
			let sampleIds = await call_get('/api/samples');
			commit('setSampleIds', sampleIds);
			console.log("sample ids: ", state.sampleIds);
		},
		async loadStateHistory({ commit, state }) {
			let stateHistory = await call_get('/api/state-history');
			commit('setStateHistory', stateHistory);
			console.log("state history: ", state.stateHistory);
		},
		async loadLastMeasurements({ commit, state }) {
			let lastMeasurements = await call_get('/api/samples/last');
			commit('setLastMeasurements', lastMeasurements);
			console.log("last measurements: ", state.lastMeasurements);
		}
	}
}
