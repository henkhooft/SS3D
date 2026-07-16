using System.Collections.Generic;
using Coimbra;
using FishNet.Object;
using UnityEngine;
using UnityEngine.UI;

namespace SS3D.Systems.Entities.Character
{
    /// <summary>
    /// Spawns a local-only humanoid for the customizer RenderTexture preview.
    /// </summary>
    public class CharacterPreviewBooth : MonoBehaviour
    {
        [SerializeField] private GameObject _humanPrefab;
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private RawImage _previewImage;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Vector3 _cameraOffset = new(0f, 1.5f, 2.2f);
        [SerializeField] private Vector3 _lookAtOffset = new(0f, 1.4f, 0f);
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

            EnsureCamera();
            EnsureRenderTexture();
            EnsureDummy();
            FrameCamera();
        }

        public void ApplySheet(CharacterSheet sheet)
        {
            if (_dummy == null)
            {
                EnsureDummy();
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
            _previewCamera.fieldOfView = 35f;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 50f;
            _previewCamera.enabled = false;
        }

        private void EnsureRenderTexture()
        {
            if (_renderTexture != null)
            {
                return;
            }

            _renderTexture = new RenderTexture(_textureWidth, _textureHeight, 16)
            {
                name = "CharacterPreviewRT",
                antiAliasing = 1,
            };
            _renderTexture.Create();
            _previewCamera.targetTexture = _renderTexture;

            if (_previewImage != null)
            {
                _previewImage.texture = _renderTexture;
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
            _dummy.transform.localRotation = Quaternion.identity;

            StripNetworking(_dummy);
            DisableGameplay(_dummy);
        }

        private void FrameCamera()
        {
            if (_previewCamera == null || _dummy == null)
            {
                return;
            }

            Transform t = _dummy.transform;
            _previewCamera.transform.position = t.position + t.rotation * _cameraOffset;
            _previewCamera.transform.LookAt(t.position + _lookAtOffset);
            _previewCamera.enabled = true;
        }

        private static void StripNetworking(GameObject root)
        {
            NetworkObject[] networkObjects = root.GetComponentsInChildren<NetworkObject>(true);
            foreach (NetworkObject networkObject in networkObjects)
            {
                Object.DestroyImmediate(networkObject);
            }

            NetworkBehaviour[] behaviours = root.GetComponentsInChildren<NetworkBehaviour>(true);
            foreach (NetworkBehaviour behaviour in behaviours)
            {
                if (behaviour != null)
                {
                    Object.DestroyImmediate(behaviour);
                }
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

                // Keep renderers' hosts and transforms; disable controllers / audio listeners.
                if (behaviour is Animator || behaviour is CharacterController || behaviour is AudioListener)
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
