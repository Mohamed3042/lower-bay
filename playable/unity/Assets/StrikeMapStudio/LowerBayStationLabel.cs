using UnityEngine;

namespace StrikeMapStudio
{
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class LowerBayStationLabel : MonoBehaviour
    {
        public Shader shader;
        private Material material;
        private void OnEnable() { Font.textureRebuilt += FontChanged; Refresh(); }
        private void FontChanged(Font font) { if (font == GetComponent<TextMesh>().font) Refresh(); }
        public void Refresh()
        {
            var text = GetComponent<TextMesh>();
            if (shader == null || text.font == null) return;
            if (material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.mainTexture = text.font.material.mainTexture;
            GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private void OnDisable()
        {
            Font.textureRebuilt -= FontChanged;
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
