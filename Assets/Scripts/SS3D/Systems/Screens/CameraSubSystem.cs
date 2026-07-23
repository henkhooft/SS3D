using SS3D.Core.Behaviours;
using UnityEngine;

namespace SS3D.Systems.Screens
{
    public class CameraSubSystem : SubSystem
    {
        [SerializeField] private Actor _playerCamera;

        public Actor PlayerCamera => _playerCamera;

        protected override void OnAwake()
        {
            base.OnAwake();
            if (_playerCamera == null)
            {
                // Phase 3h: camera stays scene-placed (TestCamera); resolve at runtime.
                Camera cam = Camera.main;
                if (cam != null && cam.TryGetComponent(out Actor actor))
                {
                    _playerCamera = actor;
                }
            }

#if UNITY_SERVER
            if (_playerCamera == null)
            {
                return;
            }

            GameObject cameraObject = _playerCamera.GameObject;

            if (cameraObject.TryGetComponent(out Camera camera))
            {
                camera.enabled = false;
            }

            if (cameraObject.TryGetComponent(out AudioListener audioListener))
            {
                audioListener.enabled = false;
            }
#endif
        }
    }
}