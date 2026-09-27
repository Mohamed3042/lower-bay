import { validateSceneMap } from './scene-validation.js';

const DEFAULT_PALETTE = { floor: '#343a3c', wall: '#a4a89f', accent: '#dcc56e', sky: '#151c23' };
const clone = (value) => structuredClone(value);
const finiteVector = (value) => Array.isArray(value) && value.length === 3 && value.every(Number.isFinite);
function rotate([x, y, z], yaw) { const angle = yaw * Math.PI / 180; return [x * Math.cos(angle) + z * Math.sin(angle), y, -x * Math.sin(angle) + z * Math.cos(angle)]; }
function hash(value) { let h = 2166136261; for (const c of value) h = Math.imul(h ^ c.charCodeAt(0), 16777619); return (h >>> 0).toString(16); }

/** Compiles an explicit agent-authored scene plan. Image analysis may colour it,
 * but never claims to have measured or independently inferred its geometry. */
export function compileScenePlan(plan, { referenceAnalysis, assets } = {}) {
  if (!plan || typeof plan !== 'object' || Array.isArray(plan)) throw new TypeError('Scene plan must be an object.');
  if (!plan.bounds || !finiteVector(plan.bounds.min) || !finiteVector(plan.bounds.max)) throw new TypeError('Scene plan requires finite rectangular bounds.');
  const palette = { ...DEFAULT_PALETTE, ...clone(plan.palette || {}), ...clone(referenceAnalysis?.palette || {}) };
  const entities = [];
  function append(source, fallbackId, transform = null) {
    if (!source || typeof source !== 'object') throw new TypeError('Each scene primitive must be an object.');
    const entity = clone(source);
    entity.id ??= fallbackId; entity.kind ??= entity.type === 'ramp' ? 'ramp' : 'wall';
    entity.rotation ??= [0, 0, 0]; entity.collidable ??= true;
    entity.walkable ??= ['floor', 'platform', 'ramp'].includes(entity.kind);
    const role = entity.colorRole || (typeof entity.color === 'string' && entity.color.startsWith('$') ? entity.color.slice(1) : null);
    entity.color = role ? palette[role] : entity.color || palette[entity.kind === 'floor' ? 'floor' : 'wall'];
    delete entity.type; delete entity.colorRole;
    if (transform) {
      if (!finiteVector(entity.position) || !finiteVector(entity.rotation)) throw new TypeError(`Module primitive ${entity.id} requires finite transforms.`);
      const local = entity.position.map((n, axis) => n + transform.step[axis]);
      entity.position = rotate(local, transform.yaw).map((n, axis) => n + transform.position[axis]);
      entity.rotation[1] += transform.yaw;
      entity.id = `${transform.prefix}/${entity.id}`;
      entity.zone ??= transform.zone;
    }
    entities.push(entity);
    if (entities.length > 10_000) throw new TypeError('Scene plan expands to more than 10,000 entities.');
  }
  if (plan.entities !== undefined && !Array.isArray(plan.entities)) throw new TypeError('Plan entities must be an array.');
  for (const [index, entity] of (plan.entities || []).entries()) append(entity, `entity-${index + 1}`);
  if (plan.modules !== undefined && !Array.isArray(plan.modules)) throw new TypeError('Plan modules must be an array.');
  for (const [index, module] of (plan.modules || []).entries()) {
    if (!module || typeof module !== 'object') throw new TypeError('Each module must be an object.');
    if (module.type === 'box' || module.type === 'ramp') { append(module, `module-${index + 1}`); continue; }
    const template = module.entities ? module : plan.moduleDefinitions?.[module.template];
    if (!template || !Array.isArray(template.entities)) throw new TypeError(`Missing module template: ${module.template || index}.`);
    const position = module.position || [0, 0, 0], rotation = module.rotation || [0, 0, 0];
    const count = module.repeat?.count ?? 1, step = module.repeat?.step || [0, 0, 0];
    if (!finiteVector(position) || !finiteVector(rotation) || rotation[0] || rotation[2] || !finiteVector(step) || !Number.isInteger(count) || count < 1 || count > 4096) throw new TypeError('Module placement, rotation or repetition is invalid.');
    for (let repeat = 0; repeat < count; repeat++) for (const [part, primitive] of template.entities.entries()) append(primitive, `part-${part + 1}`, { position, yaw: rotation[1], step: step.map((n) => n * repeat), prefix: `${module.id || `module-${index + 1}`}/${repeat}`, zone: module.zone });
  }
  const size = Math.max(plan.bounds.max[0] - plan.bounds.min[0], plan.bounds.max[2] - plan.bounds.min[2]);
  const analysis = clone(plan.analysis || {});
  analysis.provenance ??= clone(plan.provenance || { sources: plan.sources || [], decisions: [] });
  analysis.compilation = {
    version: '2.0.0', geometrySource: 'Explicit semantic primitives and module placements from the authored plan; image palette analysis does not infer geometry.',
    referenceInfluence: referenceAnalysis ? ['palette roles', 'fallback entity colours'] : [],
    coordinateFrame: 'Unity Y-up metres; no implicit source-frame conversion', moduleCount: (plan.modules || []).length,
  };
  if (referenceAnalysis) analysis.referenceAnalysis = clone(referenceAnalysis);
  const map = {
    version: '2.0', mode: 'reconstruction', id: plan.id || `scene-${hash(JSON.stringify([plan.name, plan.seed, plan.bounds, entities]))}`,
    name: plan.name || 'Authored scene', seed: String(plan.seed ?? 'authored'), theme: plan.theme || 'reconstruction', size,
    bounds: clone(plan.bounds), palette, entities,
    spawns: (plan.spawns || []).map((actor, i) => ({ id: `spawn-${i + 1}`, yaw: 0, ...clone(actor) })),
    pickups: (plan.pickups || []).map((actor, i) => ({ id: `pickup-${i + 1}`, ...clone(actor) })),
    sources: clone(plan.sources || []), analysis,
  };
  for (const field of ['zones', 'routes', 'landmarks', 'dynamics']) if (plan[field] !== undefined) map[field] = clone(plan[field]);
  if (assets || plan.assets) map.assets = { ...clone(plan.assets || {}), ...clone(assets || {}) };
  analysis.validation = validateSceneMap(map);
  if (!analysis.validation.schemaValid) throw new TypeError(`Invalid scene plan: ${analysis.validation.errors.join(' ')}`);
  return map;
}
