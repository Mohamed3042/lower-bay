using UnityEngine;

namespace StrikeMapStudio
{
    [RequireComponent(typeof(Camera))]
    public sealed class LowerBayFilm : MonoBehaviour
    {
        public Shader shader;
        [Range(.1f,4)] public float exposure = 1.15f;
        private Material material;
        private void OnEnable() { GetComponent<Camera>().allowHDR = true; }
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (shader == null || !shader.isSupported) { Graphics.Blit(source, destination); return; }
            if (material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_Exposure", exposure);
            Graphics.Blit(source, destination, material);
        }
        private void OnDisable() { if (material != null) Destroy(material); }
    }
}
