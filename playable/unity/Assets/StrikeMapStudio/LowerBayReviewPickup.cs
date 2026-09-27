using UnityEngine;

namespace StrikeMapStudio
{
    public sealed class LowerBayReviewPickup : MonoBehaviour
    {
        private float availableAt;
        private Renderer[] visuals;
        public int Collections { get; private set; }
        private void Awake() { visuals = GetComponentsInChildren<Renderer>(); }
        private void Update()
        {
            bool visible = Time.time >= availableAt;
            foreach (var item in visuals) item.enabled = visible;
        }
        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<LowerBayReviewPlayer>();
            if (player == null || Time.time < availableAt) return;
            player.collected++;
            Collections++;
            availableAt = Time.time + 20;
        }
    }
}
