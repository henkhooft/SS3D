using SS3D.Core;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Tile.SpawnPoints
{
    /// <summary>
    /// Editor-only pin visuals for authored spawn markers while the map editor is open.
    /// </summary>
    public sealed class SpawnPointEditorView : MonoBehaviour
    {
        private static SpawnPointEditorView _instance;
        private readonly List<GameObject> _pins = new();
        private SpawnPointRegistry _boundRegistry;
        private bool _visible;

        public static SpawnPointEditorView EnsureExists()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject(nameof(SpawnPointEditorView));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SpawnPointEditorView>();
            return _instance;
        }

        public static void RefreshAll()
        {
            if (_instance != null)
                _instance.Rebuild();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible)
            {
                BindRegistry();
                Rebuild();
            }
            else
            {
                ClearPins();
                UnbindRegistry();
            }
        }

        private void OnDestroy()
        {
            UnbindRegistry();
            if (_instance == this)
                _instance = null;
        }

        private void BindRegistry()
        {
            if (!SubSystems.TryGet(out TileSubSystem tile) || tile.SpawnPoints == null)
                return;

            if (_boundRegistry == tile.SpawnPoints)
                return;

            UnbindRegistry();
            _boundRegistry = tile.SpawnPoints;
            _boundRegistry.Changed += Rebuild;
        }

        private void UnbindRegistry()
        {
            if (_boundRegistry == null)
                return;

            _boundRegistry.Changed -= Rebuild;
            _boundRegistry = null;
        }

        private void Rebuild()
        {
            ClearPins();
            if (!_visible)
                return;

            if (_boundRegistry == null)
                BindRegistry();

            if (_boundRegistry == null)
                return;

            foreach (SpawnPointRecord record in _boundRegistry.Records)
                _pins.Add(CreatePin(record));
        }

        private void ClearPins()
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                if (_pins[i] != null)
                    Destroy(_pins[i]);
            }

            _pins.Clear();
        }

        private static GameObject CreatePin(SpawnPointRecord record)
        {
            var root = new GameObject($"SpawnPin_{Describe(record)}");
            root.transform.position = record.Position + Vector3.up * 0.15f;

            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(root.transform, false);
            stem.transform.localScale = new Vector3(0.08f, 0.45f, 0.08f);
            stem.transform.localPosition = Vector3.up * 0.45f;
            Object.Destroy(stem.GetComponent<Collider>());

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localScale = Vector3.one * 0.28f;
            head.transform.localPosition = Vector3.up * 0.95f;
            Object.Destroy(head.GetComponent<Collider>());

            Color color = ColorFor(record);
            ApplyColor(stem, color);
            ApplyColor(head, color);

            float yaw = DirectionToYaw(record.Direction);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            return root;
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            Material material = new(Shader.Find("Universal Render Pipeline/Unlit")
                                    ?? Shader.Find("Unlit/Color")
                                    ?? Shader.Find("Sprites/Default"));
            material.color = color;
            renderer.sharedMaterial = material;
        }

        private static Color ColorFor(SpawnPointRecord record) =>
            record.Kind == SpawnPointKind.Job
                ? new Color(0.25f, 0.75f, 1f, 0.95f)
                : new Color(1f, 0.35f, 0.25f, 0.95f);

        private static string Describe(SpawnPointRecord record) =>
            record.Kind == SpawnPointKind.Job
                ? record.JobName
                : record.AntagonistCategory.ToString();

        private static float DirectionToYaw(Direction direction) =>
            direction switch
            {
                Direction.North => 0f,
                Direction.NorthEast => 45f,
                Direction.East => 90f,
                Direction.SouthEast => 135f,
                Direction.South => 180f,
                Direction.SouthWest => 225f,
                Direction.West => 270f,
                Direction.NorthWest => 315f,
                _ => 0f,
            };
    }
}
