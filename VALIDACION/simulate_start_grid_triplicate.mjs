import assert from 'node:assert/strict';

// Independent executable model for the proposed start-grid hierarchy.
// It intentionally does not import the Fulcrum production source: this gate
// validates the behavior before the production package is modified.

class GridModel {
  constructor() {
    this.grid = new Map();
    this.quality = new Map();
  }

  update({classes, live, state, official = null}) {
    const formation = state >= 1 && state <= 3;
    const gainPhase = state >= 4 && state <= 6;
    const result = new Map();

    for (const [classId, cars] of classes) {
      const current = orderFor(cars, live);
      const history = orderFor(cars, official);
      const currentQuality = this.quality.get(classId) ?? 0;

      // Observed pre-green order is exact and has highest priority.
      if (formation && current && currentQuality < 3) {
        this.grid.set(classId, new Map(current));
        this.quality.set(classId, 3);
      // iRacing's current-session QualifyPositions replaces only a provisional
      // mid-race baseline. It never overwrites an observed formation grid.
      } else if (history && currentQuality < 2) {
        this.grid.set(classId, new Map(history));
        this.quality.set(classId, 2);
      // Last resort for sessions where iRacing exposes no start metadata.
      } else if (!currentQuality && current) {
        this.grid.set(classId, new Map(current));
        this.quality.set(classId, 1);
      }

      const baseline = this.grid.get(classId);
      for (const car of cars) {
        const position = current?.get(car) ?? 0;
        result.set(car, {
          position,
          gain: gainPhase && baseline && position ? baseline.get(car) - position : 0,
          available: Boolean(gainPhase && baseline && position),
          quality: this.quality.get(classId) ?? 0,
        });
      }
    }
    return result;
  }
}

function orderFor(cars, source) {
  if (!source) return null;
  const seen = new Set();
  const result = new Map();
  for (const car of cars) {
    const rank = source.get(car) ?? 0;
    if (rank < 1 || rank > cars.length || seen.has(rank)) return null;
    seen.add(rank);
    result.set(car, rank);
  }
  return result;
}

function rng(seed) {
  let value = seed >>> 0;
  return () => {
    value ^= value << 13;
    value ^= value >>> 17;
    value ^= value << 5;
    return (value >>> 0) / 4294967296;
  };
}

function shuffled(values, random) {
  const out = values.slice();
  for (let i = out.length - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1));
    [out[i], out[j]] = [out[j], out[i]];
  }
  return out;
}

function ranks(cars, order) {
  return new Map(order.map((car, index) => [car, index + 1]));
}

function checkGain(result, cars, baseline, current, label) {
  for (const car of cars) {
    assert.equal(result.get(car).available, true, `${label}: availability ${car}`);
    assert.equal(result.get(car).gain, baseline.get(car) - current.get(car), `${label}: gain ${car}`);
  }
}

function simulate(run, seed) {
  const random = rng(seed);
  let scenarios = 0;
  let assertions = 0;
  const equal = (...args) => { assert.equal(...args); assertions++; };

  for (let iteration = 0; iteration < 250; iteration++) {
    const sizeA = 3 + Math.floor(random() * 12);
    const sizeB = 2 + Math.floor(random() * 10);
    const classA = Array.from({length: sizeA}, (_, i) => i);
    const classB = Array.from({length: sizeB}, (_, i) => 32 + i);
    const classes = new Map([[100, classA], [200, classB]]);
    const official = new Map([
      ...ranks(classA, shuffled(classA, random)),
      ...ranks(classB, shuffled(classB, random)),
    ]);
    const live = new Map([
      ...ranks(classA, shuffled(classA, random)),
      ...ranks(classB, shuffled(classB, random)),
    ]);

    // 1) SimHub starts/restarts at racing state. Persistent session metadata
    // must reconstruct the original per-class baseline immediately.
    const restarted = new GridModel();
    const afterRestart = restarted.update({classes, live, state: 4, official});
    checkGain(afterRestart, classA, official, live, 'mid-race restart A');
    checkGain(afterRestart, classB, official, live, 'mid-race restart B');
    assertions += 2 * (classA.length + classB.length);
    equal(afterRestart.get(classA[0]).quality, 2, 'mid-race history quality');
    scenarios++;

    // 2) Metadata arrives after a coherent live frame. The provisional grid
    // must upgrade once, then remain fixed through pits/tow/yellow frames.
    const delayed = new GridModel();
    const provisional = delayed.update({classes, live, state: 4});
    for (const car of [...classA, ...classB]) {
      equal(provisional.get(car).gain, 0, 'provisional starts at zero');
      equal(provisional.get(car).quality, 1, 'provisional quality');
    }
    const upgraded = delayed.update({classes, live, state: 4, official});
    checkGain(upgraded, classA, official, live, 'delayed upgrade A');
    checkGain(upgraded, classB, official, live, 'delayed upgrade B');
    assertions += 2 * (classA.length + classB.length);
    const nextLive = new Map([
      ...ranks(classA, shuffled(classA, random)),
      ...ranks(classB, shuffled(classB, random)),
    ]);
    const cautionWithoutMetadata = delayed.update({classes, live: nextLive, state: 4});
    checkGain(cautionWithoutMetadata, classA, official, nextLive, 'extended caution A');
    checkGain(cautionWithoutMetadata, classB, official, nextLive, 'extended caution B');
    assertions += 2 * (classA.length + classB.length);
    equal(cautionWithoutMetadata.get(classB[0]).quality, 2, 'history never downgrades');
    scenarios++;

    // 3) An observed formation grid outranks conflicting metadata and cannot
    // be overwritten at green or during a later rejoin.
    const observedOrder = new Map([
      ...ranks(classA, shuffled(classA, random)),
      ...ranks(classB, shuffled(classB, random)),
    ]);
    const observed = new GridModel();
    observed.update({classes, live: observedOrder, state: 3, official});
    const racing = observed.update({classes, live, state: 4, official});
    checkGain(racing, classA, observedOrder, live, 'observed formation A');
    checkGain(racing, classB, observedOrder, live, 'observed formation B');
    assertions += 2 * (classA.length + classB.length);
    equal(racing.get(classA[0]).quality, 3, 'observed grid remains authoritative');
    scenarios++;

    // Invalid historical rows (duplicate class position) must never replace a
    // valid baseline and a different class remains isolated.
    const invalid = new Map(official);
    invalid.set(classA[1], invalid.get(classA[0]));
    const guarded = observed.update({classes, live, state: 4, official: invalid});
    equal(guarded.get(classA[0]).quality, 3, 'duplicate metadata rejected');
    equal(guarded.get(classB[0]).quality, 3, 'other class baseline unaffected');
    scenarios++;
  }

  return {run, seed, status: 'PASS', scenarios, assertions};
}

const results = [
  simulate(1, 0x415701),
  simulate(2, 0x415702),
  simulate(3, 0x415703),
];

console.log(JSON.stringify({status: 'PASS', results}, null, 2));
