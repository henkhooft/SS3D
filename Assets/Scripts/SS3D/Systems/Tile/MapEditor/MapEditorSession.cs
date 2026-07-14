using UnityEngine;
using UnityEngine.InputSystem;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Client-only free camera used while the map editor is open.
    /// </summary>
    public sealed class MapEditorSession
    {
        private Camera _camera;
        private Transform _previousParent;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private Vector3 _entryPosition;
        private bool _active;

        private float _yaw;
        private float _pitch = 45f;
        private float _distance = 20f;
        private Vector3 _focus;

        public bool IsActive => _active;

        public void Enter(Camera camera, Vector3 entryPosition)
        {
            if (_active || camera == null)
                return;

            _camera = camera;
            _entryPosition = entryPosition;
            _savedPosition = camera.transform.position;
            _savedRotation = camera.transform.rotation;
            _previousParent = camera.transform.parent;

            _focus = entryPosition;
            _yaw = camera.transform.eulerAngles.y;
            _pitch = 45f;
            _distance = 20f;

            camera.transform.SetParent(null);
            ApplyCamera();
            _active = true;
        }

        public void Exit()
        {
            if (!_active || _camera == null)
                return;

            _camera.transform.SetParent(_previousParent);
            _camera.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            _active = false;
            _camera = null;
        }

        public void ResetPosition()
        {
            _focus = _entryPosition;
            _distance = 20f;
            _pitch = 45f;
            ApplyCamera();
        }

        public void Update(float deltaTime)
        {
            if (!_active || _camera == null)
                return;

            float moveSpeed = 15f * deltaTime;
            float rotateSpeed = 120f * deltaTime;
            Keyboard keyboard = Keyboard.current;

            Vector3 forward = _camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                forward.Normalize();

            Vector3 right = _camera.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude > 0.0001f)
                right.Normalize();

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed)
                    _focus += forward * moveSpeed;
                if (keyboard.sKey.isPressed)
                    _focus -= forward * moveSpeed;
                if (keyboard.aKey.isPressed)
                    _focus -= right * moveSpeed;
                if (keyboard.dKey.isPressed)
                    _focus += right * moveSpeed;
            }

            if (Mouse.current != null && Mouse.current.middleButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _yaw += delta.x * rotateSpeed * 0.05f;
                _pitch -= delta.y * rotateSpeed * 0.05f;
                _pitch = Mathf.Clamp(_pitch, 10f, 85f);
            }

            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            if (Mathf.Abs(scroll) > 0.01f)
                _distance = Mathf.Clamp(_distance - scroll * 0.25f, 5f, 80f);

            ApplyCamera();
        }

        private void ApplyCamera()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -_distance);
            _camera.transform.position = _focus + offset;
            _camera.transform.rotation = rotation;
        }
    }
}
