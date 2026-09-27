import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
const map = JSON.parse(fs.readFileSync(new URL('../output/lower-bay.strikemap.json', import.meta.url), 'utf8'));

test('phase 2 restores source-sized piers and complete station fixture groups', () => {
  const halls = map.entities.filter(e => /hall-piers-.*\/tile$/.test(e.id));
  const concourse = map.entities.filter(e => /concourse-piers-.*\/tile$/.test(e.id));
  assert.equal(halls.length, 36);
  assert.equal(concourse.length, 22);
  assert.ok(halls.every(e => e.size[0] === 1.1 && e.size[2] === 1.1));
  assert.ok(concourse.every(e => e.size[0] === .9 && e.size[2] === .9));
  assert.equal(map.entities.filter(e => /^fare-gate-\d+-body$/.test(e.id)).length, 12);
  assert.equal(map.entities.filter(e => /^ticket-machine-\d+-body$/.test(e.id)).length, 4);
  for (const id of ['info-booth-canopy', 'restroom-roof', 'service-west-roof', 'service-east-roof']) assert.ok(map.entities.some(e => e.id === id), id);
  assert.equal(map.entities.filter(e => /^fanlight-(red|blue)-pane-\d+$/.test(e.id)).length, 48);
});

test('phase 2 verifies all five glass entrances, two flank entrances and new room entry/return routes', () => {
  for (const prefix of ['glass-entrance-', 'flank-entrance-']) {
    const matches = map.routes.filter(route => route.id.startsWith(prefix));
    assert.equal(matches.length, prefix.startsWith('glass') ? 5 : 2);
    assert.ok(matches.every(route => route.direction === 'both'));
  }
  for (const id of ['fare-gate-passage', 'ticket-machines-access', 'info-booth-access', 'restroom-access', 'service-west-access', 'service-east-access']) {
    assert.ok(map.routes.some(route => route.id === id && route.direction === 'both'), id);
  }
  assert.equal(map.analysis.validation.valid, true, map.analysis.validation.errors.join('\n'));
  assert.equal(map.analysis.validation.metrics.reachablePercent, 100);
  assert.equal(map.analysis.validation.metrics.returnReachablePercent, 100);
  assert.ok(map.analysis.provenance.reconciliation.some(item => item.feature === 'station-fixtures' && item.status === 'source-anchored-inferred-details'));
  assert.ok(map.analysis.provenance.reconciliation.some(item => item.feature === 'catwalk-roof-transitions' && item.status === 'source-gameplay-restored'));
  assert.ok(map.assets['textures/winter-poster.png']);
  assert.ok(Buffer.byteLength(JSON.stringify(map, null, 2)) < 24 * 1024 * 1024);
});

// Re-closing this doorway reproduces the paused phase2 trap instead of merely
// comparing dimensions with the generator's own constants.
test('closing the inferred restroom service doorway traps the red under-gallery pocket', async () => {
  const { validateSceneMap } = await import('../lib/scene-validation.js');
  const blocked = { ...map, entities: [...map.entities, {
    id: 'regression-restroom-service-closure', kind: 'wall',
    position: [-31, 1.15, 14.15], size: [.2, 2.3, 1.6], rotation: [0, 0, 0],
    color: '#ffffff', collidable: true, walkable: false,
  }] };
  const result = validateSceneMap(blocked);
  assert.equal(result.schemaValid, true);
  assert.ok(result.metrics.returnReachablePercent < 100);
  assert.ok(result.diagnostics.noReturnSamples.some(sample => sample.position[0] < -31 && sample.position[1] === 0));
  assert.equal(result.routeResults.find(route => route.id === 'red-under-gallery-return')?.valid, false);
  assert.equal(map.routes.find(route => route.id === 'red-under-gallery-return')?.direction, 'both');
  assert.equal(map.analysis.validation.diagnostics.noReturnSamples.length, 0);
});
