using UnityEngine;

namespace StrikeMapStudio
{
    public sealed class StrikeMapEntity : MonoBehaviour
    {
        public string sourceId, kind, zone, label;
        public bool walkable, roof;
        // Cutaways affect visibility only. Collider state is deliberately untouched.
        public void SetRoofVisible(bool visible)
        {
            if (!roof) return;
            foreach (Renderer item in GetComponentsInChildren<Renderer>()) item.enabled = visible;
        }
    }
}
