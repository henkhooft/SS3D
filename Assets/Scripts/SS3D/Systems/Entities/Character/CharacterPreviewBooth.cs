using Coimbra;
using FishNet.Object;
using FishNet.Observing;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

namespace SS3D.Systems.Entities.Character
{
    /// <summary>
    /// Spawns a local-only humanoid for the customizer RenderTexture preview.
    /// Uses a dedicated preview layer and world-space booth (same approach as RuntimePreviewGenerator).
    /// </summary>
    public class CharacterPreviewBooth : MonoBehaviour
    {
        private const int PreviewLayer = 22;
        private static readonly Vector3 BoothOrigin = new(-250f, -250f, -250f);

        [SerializeField] private GameObject _humanPrefab;
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private RawImage _previewImage;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Vector3 _cameraOffset = new(0f, 1.2f, 2.6f);
        [SerializeField] private Vector3 _lookAtOffset = new(0f, 1.1f, 0f);
        [SerializeField] private int _textureWidth = 512;
        [SerializeField] private int _textureHeight = 512;

        private GameObject _dummy;
        private RenderTexture _renderTexture;
        private AppearanceCatalog _catalog;

        public GameObject Dummy => _dummy;

        public void Initialize(GameObject humanPrefab, AppearanceCatalog catalog, RawImage previewImage)
        {
            if (humanPrefab != null)
            {
                _humanPrefab = humanPrefab;
            }

            _catalog = catalog;
            if (previewImage != null)
            {
                _previewImage = previewImage;
            }

            MoveToWorldSpace();
            EnsureCamera();
            EnsureRenderTexture();
            EnsureDummy();
            FrameCamera();
            SetActive(true);
        }

        public void ApplySheet(CharacterSheet sheet)
        {
            if (_dummy == null)
            {
                EnsureDummy();
                FrameCamera();
            }

            if (_dummy == null)
            {
                return;
            }

            HumanoidAppearanceApplier.Apply(_dummy, sheet, _catalog);
        }

        public void SetActive(bool active)
        {
            if (_previewCamera != null)
            {
                _previewCamera.enabled = active;
            }

            if (_dummy != null)
            {
                _dummy.SetActive(active);
            }

            if (_previewImage != null)
            {
                _previewImage.enabled = active;
                if (active && _renderTexture != null)
                {
                    _previewImage.texture = _renderTexture;
                    _previewImage.color = Color.white;
                }
            }
        }

        private void OnDestroy()
        {
            if (_dummy != null)
            {
                _dummy.Dispose(true);
            }

            if (_renderTexture != null)
            {
                if (_previewCamera != null)
                {
                    _previewCamera.targetTexture = null;
                }

                _renderTexture.Release();
                Destroy(_renderTexture);
            }
        }

        private void MoveToWorldSpace()
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, false);
            }

            transform.position = BoothOrigin;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private void EnsureCamera()
        {
            if (_previewCamera != null)
            {
                return;
            }

            GameObject cameraObject = new("CharacterPreviewCamera");
            cameraObject.transform.SetParent(transform, false);
            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            _previewCamera.fieldOfView = 28f;
            _previewCamera.nearClipPlane = 0.05f;
            _previewCamera.farClipPlane = 40f;
            _previewCamera.depth = -100f;
            _previewCamera.cullingMask = 1 << PreviewLayer;
            _previewCamera.allowHDR = false;
            _previewCamera.allowMSAA = false;
            _previewCamera.enabled = false;

            ConfigureUrpCamera(_previewCamera);

            AudioListener listener = cameraObject.GetComponent<AudioListener>();
            if (listener != null)
            {
                DestroyImmediate(listener);
            }
        }

        private static void ConfigureUrpCamera(Camera camera)
        {
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.renderShadows = false;
            cameraData.dithering = false;
#endif
        }

        private void EnsureRenderTexture()
        {
            if (_renderTexture != null)
            {
                return;
            }

            _renderTexture = new RenderTexture(_textureWidth, _textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "CharacterPreviewRT",
                antiAliasing = 1,
            };
            _renderTexture.Create();
            _previewCamera.targetTexture = _renderTexture;

            if (_previewImage != null)
            {
                _previewImage.texture = _renderTexture;
                _previewImage.color = Color.white;
            }
        }

        private void EnsureDummy()
        {
            if (_dummy != null || _humanPrefab == null)
            {
                return;
            }

            Transform parent = _spawnPoint != null ? _spawnPoint : transform;
            _dummy = Instantiate(_humanPrefab, parent);
            _dummy.name = "CharacterPreviewDummy";
            _dummy.transform.localPosition = Vector3.zero;
            _dummy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _dummy.transform.localScale = Vector3.one;

            StripNetworking(_dummy);
            DisableGameplay(_dummy);
            SetLayerRecursively(_dummy.transform, PreviewLayer);
        }

        private void FrameCamera()
        {
            if (_previewCamera == null || _dummy == null)
            {
                return;
            }

            Bounds bounds = CalculateBounds(_dummy);
            Vector3 lookAt = bounds.center;
            lookAt.y = Mathf.Lerp(bounds.min.y, bounds.max.y, 0.55f);

            float radius = Mathf.Max(bounds.extents.magnitude, 0.75f);
            float distance = radius / Mathf.Tan(_previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            distance = Mathf.Clamp(distance * 1.15f, 1.5f, 8f);

            Vector3 forward = Quaternion.Euler(8f, 180f, 0f) * Vector3.forward;
            _previewCamera.transform.position = lookAt - forward * distance + Vector3.up * (radius * 0.05f);
            _previewCamera.transform.LookAt(lookAt);
            _previewCamera.enabled = true;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new(root.transform.position, Vector3.one * 0.1f);
            bool initialized = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        private static void StripNetworking(GameObject root)
        {
            DestroyAll<RagdollPart>(root);
            DestroyAll<NetworkObserver>(root);
            DestroyAll<NetworkBehaviour>(root);
            DestroyAll<NetworkObject>(root);
        }

        private static void DestroyAll<T>(GameObject root) where T : Object
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                {
                    continue;
                }

                Object.DestroyImmediate(component);
            }
        }

        private static void DisableGameplay(GameObject root)
        {
            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                // Keep Animator so skinned meshes stay in a valid bind/pose; freeze playback.
                if (behaviour is Animator animator)
                {
                    animator.enabled = true;
                    animator.speed = 0f;
                    continue;
                }

                if (behaviour is CharacterController || behaviour is AudioListener)
                {
                    behaviour.enabled = false;
                }

                if (behaviour is NetworkBehaviour || behaviour is NetworkObject)
                {
                    behaviour.enabled = false;
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                collider.enabled = false;
            }

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            foreach (Rigidbody body in bodies)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }
    }
}
