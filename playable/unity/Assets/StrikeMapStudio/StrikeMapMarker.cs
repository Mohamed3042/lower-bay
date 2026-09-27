using UnityEngine;

namespace StrikeMapStudio
{
    public sealed class StrikeMapMarker : MonoBehaviour
    {
        public string sourceId, kind, team;
        public Color color = Color.white;
        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            if (kind == "spawn")
            {
                Gizmos.DrawWireSphere(transform.position, 0.45f);
                Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
            }
            else Gizmos.DrawWireCube(transform.position, Vector3.one * 0.7f);
        }
    }
}
