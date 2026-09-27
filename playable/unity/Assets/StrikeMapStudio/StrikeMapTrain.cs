using System;
using UnityEngine;

namespace StrikeMapStudio
{
    // Uses the same seconds/offset equation as src/dynamics.js. Motion is inferred design.
    // Local CharacterControllers receive explicit carry/push/hazard handling. Native bots
    // use a different movement simulation and are not claimed to consume this controller.
    [DefaultExecutionOrder(-100)]
    public sealed class StrikeMapTrain : MonoBehaviour
    {
        public string sourceId, axis = "x";
        public float min, max, speed, pause, phase;
        public bool hazard, nativePlatformsConfigured;
        public Vector3 authoredOrigin;
        public Transform resetSpawn;
        public int CarryCount { get; private set; }
        public int PushCount { get; private set; }
        public int HazardCount { get; private set; }
        public int TrappedCount { get; private set; }
        public bool Paused { get; set; }
        private double elapsed;
        private Collider[] solids;

        public static double Offset(double seconds, double min, double max, double speed, double pause, double phase)
        {
            double length = max - min;
            if (length <= 0 || speed <= 0) return min;
            double travel = length / speed, period = 2 * (travel + pause);
            double t = ((seconds + phase) % period + period) % period;
            if (t < pause) return min;
            if (t < pause + travel) return min + (t - pause) * speed;
            if (t < 2 * pause + travel) return max;
            return max - (t - (2 * pause + travel)) * speed;
        }

        private void Awake() { elapsed = 0; RefreshColliders(); }
        private void FixedUpdate()
        {
            if (Paused) return;
            elapsed += Time.fixedDeltaTime;
            ApplyAtTime(elapsed, true);
        }
        public void Restart() { elapsed = 0; Paused = false; ApplyAtTime(0); }
        public void RefreshColliders() { solids = Array.FindAll(GetComponentsInChildren<Collider>(), c => !c.isTrigger); }

        public void ApplyAtTime(double elapsedSeconds, bool respondToControllers = false)
        {
            if (solids == null) RefreshColliders();
            Vector3 before = transform.position;
            Vector3 direction = axis == "z" ? Vector3.forward : Vector3.right;
            Vector3 after = authoredOrigin + direction * (float)Offset(elapsedSeconds, min, max, speed, pause, phase);
            Vector3 delta = after - before;
            var oldBounds = new Bounds[solids.Length];
            for (int i = 0; i < solids.Length; i++) oldBounds[i] = solids[i].bounds;
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null) body.position = after;
            transform.position = after;
            Physics.SyncTransforms();
            if (!respondToControllers || delta.sqrMagnitude < .00000001f) return;
            foreach (CharacterController actor in UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
            {
                if (!actor.enabled || !actor.CompareTag("Player")) continue;
                bool handled = false;
                for (int i = 0; i < solids.Length && !handled; i++)
                {
                    Collider solid = solids[i];
                    Bounds previous = oldBounds[i], current = solid.bounds;
                    Bounds actorBounds = actor.bounds;
                    bool overTop = actorBounds.max.x > previous.min.x && actorBounds.min.x < previous.max.x && actorBounds.max.z > previous.min.z && actorBounds.min.z < previous.max.z
                        && actorBounds.min.y >= previous.max.y - .12f && actorBounds.min.y <= previous.max.y + .25f;
                    if (overTop)
                    {
                        if (!nativePlatformsConfigured)
                        {
                            Vector3 riderBefore = actor.transform.position;
                            actor.Move(delta);
                            CarryCount++;
                            Vector3 actual = actor.transform.position - riderBefore;
                            // A roof rider cannot pass through the committed-drop
                            // catwalk lip. A blocked carry is a crush/reset, not a
                            // free ascent or an actor left embedded in the train.
                            float requested = axis == "z" ? delta.z : delta.x;
                            float moved = axis == "z" ? actual.z : actual.x;
                            if (Mathf.Abs(requested - moved) > Mathf.Max(.025f, Mathf.Abs(requested) * .3f))
                            { TrappedCount++; ResetActor(actor); }
                        }
                        handled = true; continue;
                    }
                    Bounds swept = previous; swept.Encapsulate(current);
                    if (!swept.Intersects(actorBounds)) continue;
                    float axisDelta = axis == "z" ? delta.z : delta.x;
                    float along = axis == "z" ? actorBounds.center.z - previous.center.z : actorBounds.center.x - previous.center.x;
                    float fraction = Mathf.Abs(axisDelta) > .000001f ? Mathf.Clamp01(along / axisDelta) : 1;
                    Vector3 contactPose = solid.transform.position - delta * (1 - fraction);
                    Vector3 separation; float distance;
                    bool crosses = Physics.ComputePenetration(solid, contactPose, solid.transform.rotation, actor, actor.transform.position, actor.transform.rotation, out separation, out distance);
                    bool overlaps = Physics.ComputePenetration(solid, solid.transform.position, solid.transform.rotation, actor, actor.transform.position, actor.transform.rotation, out separation, out distance);
                    if (!crosses && !overlaps) continue;
                    if (hazard) { HazardCount++; ResetActor(actor); handled = true; continue; }
                    // Moving an already-overlapped CharacterController with Move can eject
                    // it vertically onto a tall car. Sweep its capsule horizontally against
                    // the rest of the world before relocating, preserving the actor's Y.
                    PushController(actor, current, delta);
                    PushCount++;
                    if (Physics.ComputePenetration(actor, actor.transform.position, actor.transform.rotation, solid, solid.transform.position, solid.transform.rotation, out separation, out distance) && distance > .03f)
                    { TrappedCount++; ResetActor(actor); }
                    handled = true;
                }
            }
        }

        private void PushController(CharacterController actor, Bounds trainBounds, Vector3 delta)
        {
            Bounds bounds = actor.bounds;
            float signedDelta = axis == "z" ? delta.z : delta.x;
            Vector3 direction = (axis == "z" ? Vector3.forward : Vector3.right) * Mathf.Sign(signedDelta);
            float front = axis == "z" ? (signedDelta > 0 ? trainBounds.max.z - bounds.min.z : bounds.max.z - trainBounds.min.z)
                : (signedDelta > 0 ? trainBounds.max.x - bounds.min.x : bounds.max.x - trainBounds.min.x);
            float distance = Mathf.Max(Mathf.Abs(signedDelta), front + .03f);
            float radius = actor.radius * Mathf.Max(Mathf.Abs(actor.transform.lossyScale.x), Mathf.Abs(actor.transform.lossyScale.z));
            float half = Mathf.Max(radius, actor.height * Mathf.Abs(actor.transform.lossyScale.y) / 2);
            Vector3 endpoint = Vector3.up * (half - radius);
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bounds.center - endpoint, bounds.center + endpoint, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == actor || Array.IndexOf(solids, hit.collider) >= 0) continue;
                distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .02f));
            }
            actor.enabled = false;
            actor.transform.position += direction * distance;
            actor.enabled = true;
            Physics.SyncTransforms();
        }

        private void ResetActor(CharacterController actor)
        {
            Type stateType = StrikeMapCatalogAdapter.FindType("GameState");
            if (stateType != null)
            {
                object state = stateType.GetField("Current")?.GetValue(null);
                object actions = state == null ? null : stateType.GetField("Actions")?.GetValue(state);
                var kill = actions?.GetType().GetField("KillPlayer")?.GetValue(actions) as Delegate;
                if (kill != null) { kill.DynamicInvoke(); return; }
            }
            if (resetSpawn == null) return;
            actor.enabled = false;
            actor.transform.SetPositionAndRotation(resetSpawn.position, resetSpawn.rotation);
            actor.enabled = true;
        }
    }
}
