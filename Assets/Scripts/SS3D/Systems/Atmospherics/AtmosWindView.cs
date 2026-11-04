using Coimbra;
using UnityEngine;
using UnityEngine.Rendering;

namespace SS3D.Systems.Atmospherics
{
    /// <summary>Bounded reusable wind curls. Updates stop and fade when no field arrives.</summary>
    public sealed class AtmosWindView : MonoBehaviour
    {
        private readonly LineRenderer[] _lines = new LineRenderer[96];
        private readonly Vector3[] _centres = new Vector3[96];
        private readonly Vector3[] _wind = new Vector3[96];
        private Material _material;
        private int _count;
        private float _expires;

        public void Show(Vector3[] positions, Vector3[] gradients, int count)
        {
            _count = Mathf.Min(count, _lines.Length);
            _expires = Time.time + AtmosConstants.TickInterval * 2.5f;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (i >= _count)
                {
                    if (_lines[i] != null) _lines[i].enabled = false;
                    continue;
                }
                if (_lines[i] == null)
                {
                    if (_material == null)
                    {
                        Shader shader = Resources.Load<Shader>("Atmospherics/Wind");
                        if (shader == null) return;
                        _material = new Material(shader);
                    }
                    var go = new GameObject("Wind curl");
                    go.transform.SetParent(transform, false);
                    var line = go.AddComponent<LineRenderer>();
                    line.sharedMaterial = _material;
                    line.useWorldSpace = true;
                    line.positionCount = 18;
                    line.widthMultiplier = 0.03f;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    _lines[i] = line;
                }
                _lines[i].enabled = true;
                _centres[i] = positions[i];
                _wind[i] = gradients[i];
            }
        }

        private void Update()
        {
            float fade = Mathf.Clamp01((_expires - Time.time) / AtmosConstants.TickInterval);
            for (int i = 0; i < _count; i++)
            {
                LineRenderer line = _lines[i];
                if (line == null) continue;
                line.enabled = fade > 0f;
                if (!line.enabled) continue;
                Vector3 forward = _wind[i].normalized;
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                float phase = Mathf.Repeat(Time.time * 1.8f + i * 0.37f, 1f);
                float alpha = Mathf.Sin(phase * Mathf.PI) * fade * Mathf.Clamp01(_wind[i].magnitude / 15f) * 0.65f;
                line.startColor = new Color(0.7f, 0.9f, 1f, 0f);
                line.endColor = new Color(0.7f, 0.9f, 1f, alpha);
                for (int p = 0; p < 18; p++)
                {
                    float t = p / 17f;
                    float angle = t * Mathf.PI * 2.7f;
                    float radius = 0.4f * (1f - t * 0.85f);
                    line.SetPosition(p, _centres[i] + forward * ((phase - 0.5f) * 0.8f + Mathf.Sin(angle) * radius)
                        + side * (Mathf.Cos(angle) * radius));
                }
            }
        }

        private void OnDestroy()
        {
            foreach (LineRenderer line in _lines)
                if (line != null) line.gameObject.Dispose(true);
            if (_material != null) Destroy(_material);
        }
    }
}
