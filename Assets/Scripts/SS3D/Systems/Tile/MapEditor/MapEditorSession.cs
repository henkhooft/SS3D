using UnityEngine;

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

            if (Input.GetKey(KeyCode.W))
                _focus += _camera.transform.forward * moveSpeed;
            if (Input.GetKey(KeyCode.S))
                _focus -= _camera.transform.forward * moveSpeed;
            if (Input.GetKey(KeyCode.A))
                _focus -= _camera.transform.right * moveSpeed;
            if (Input.GetKey(KeyCode.D))
                _focus += _camera.transform.right * moveSpeed;

            if (Input.GetMouseButton(2))
            {
                _yaw += Input.GetAxis("Mouse X") * rotateSpeed;
                _pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
                _pitch = Mathf.Clamp(_pitch, 10f, 85f);
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _distance = Mathf.Clamp(_distance - scroll * 2f, 5f, 80f);

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
