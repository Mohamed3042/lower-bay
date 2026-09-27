import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { validateSceneMap } from '../lib/scene-validation.js';
import { createCollisionWorld, createPlayer, stepPlayer, playerIntersects, V2_PLAYER } from '../lib/physics.js';

const map = JSON.parse(fs.readFileSync(new URL('../output/lower-bay.strikemap.json', import.meta.url), 'utf8'));

test('complete Lower Bay static scene connects all required actors and permits return from every sampled surface', () => {
  const result = validateSceneMap(map);
  assert.equal(result.schemaValid, true);
  assert.equal(result.valid, true, result.errors.join('\n'));
  assert.equal(result.metrics.reachablePercent, 100);
  assert.equal(result.metrics.returnReachablePercent, 100);
  assert.equal(map.spawns.length, 6);
  assert.equal(map.pickups.length, 11);
  assert.ok(result.routeResults.length >= 9);
  assert.equal(result.routeResults.filter(route => route.id.startsWith('track-crossing-')).length, 2);
  assert.ok(result.routeResults.every(route => route.valid));
});

test('removing authored support from physically stepable rails reproduces inaccessible track pickups', () => {
  const broken = { ...map, entities: map.entities.map(entity => entity.id.startsWith('rail-') ? { ...entity, walkable: false } : entity) };
  const result = validateSceneMap(broken);
  assert.equal(result.schemaValid, true);
  assert.equal(result.valid, false);
  assert.equal(result.errors.filter(error => /Pickup track-health-.*sampled movement graph/.test(error)).length, 2);
});

test('former ramps buried inside platform slabs fail explicit crossing routes and controller traversal', () => {
  const broken = { ...map, entities: map.entities.map(entity => entity.id.startsWith('track-access-') ? {
    ...entity, position: [entity.position[0], -.6, entity.position[2] < 0 ? -2 : 2], size: [2.8, 1.2, 2.8],
  } : entity) };
  const result = validateSceneMap(broken);
  assert.equal(result.schemaValid, true);
  assert.equal(result.valid, false);
  assert.ok(result.routeResults.filter(route => route.id.startsWith('track-crossing-')).every(route => !route.valid));
  assert.ok(result.metrics.returnReachablePercent < 90);
  const world = createCollisionWorld(broken.entities, { profile: V2_PLAYER });
  const player = createPlayer([-26, 0, 3.4]);
  for (let frame = 0; frame < 84; frame++) stepPlayer(player, { z: -1 }, world, 1 / 60);
  assert.ok(player.z > -2, JSON.stringify(player));
});

test('tunnel walls intruding upstairs reproduce isolated spawn-room corner samples with actionable positions', () => {
  const broken = { ...map, entities: map.entities.map(entity => {
    if (/^tunnel-wall-(-1|1)-/.test(entity.id) && !entity.id.endsWith('-dado')) return { ...entity, position: [Math.sign(entity.position[0]) * 52, entity.position[1], entity.position[2]], size: [28, 6.7, .4] };
    if (/^(red|blue)-end-wall-south$/.test(entity.id)) return { ...entity, position: [entity.position[0], 2.75, entity.position[2]], size: [.3, 5.5, 2.3] };
    return entity;
  }) };
  const result = validateSceneMap(broken);
  assert.equal(result.schemaValid, true);
  assert.ok(result.metrics.reachablePercent < 100);
  assert.equal(result.diagnostics.unreachableSamples.length, 2);
  assert.ok(result.diagnostics.unreachableSamples.every(sample => sample.supportIds.some(id => id.endsWith('spawn-floor'))));
});

test('Lower Bay capsule can cross the trench in both directions at both authored access pairs', () => {
  const world = createCollisionWorld(map.entities, { profile: V2_PLAYER });
  for (const x of [-26, 26]) for (const direction of [-1, 1]) {
    const player = createPlayer([x, 0, -direction * 3.4]);
    for (let frame = 0; frame < 84; frame++) {
      stepPlayer(player, { x: 0, z: direction }, world, 1 / 60);
      assert.equal(playerIntersects(world, player), false, `Embedded at x=${x}, direction=${direction}, frame=${frame}`);
    }
    assert.ok(player.z * direction > 3.1, JSON.stringify({ x, direction, player }));
    assert.ok(Math.abs(player.y) < .01, JSON.stringify(player));
  }
});
