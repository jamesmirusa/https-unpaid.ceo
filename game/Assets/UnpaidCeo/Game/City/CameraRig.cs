using UnityEngine;
using UnityEngine.InputSystem;

namespace UnpaidCeo.Game.City
{
    /// <summary>
    /// Isometric diorama camera: drag (mouse or one finger) to pan, wheel or pinch to zoom, within the district.
    /// Tilt-shift depth of field lives in the post-processing volumes, which Eco turns off with post-processing.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Vector3 focus = Vector3.zero;
        [SerializeField] float distance = 70f, minDistance = 30f, maxDistance = 120f;
        [SerializeField] float pitch = 38f, yaw = 45f;
        [SerializeField] Vector2 panLimit = new Vector2(45f, 30f);
        [SerializeField] float panSpeed = 0.08f, zoomSpeed = 0.05f;

        Vector2 _lastPointer;
        float _lastPinch;

        public Vector3 Focus => focus;
        public float Distance => distance;

        void OnEnable() => Apply();

        /// <summary>Places the camera from focus/distance/angles (also usable in edit mode, e.g. by the district builder).</summary>
        public void Snap() => Apply();

        public void Pan(Vector2 screenDelta)
        {
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            float k = panSpeed * distance / 70f;
            focus -= (right * screenDelta.x + forward * screenDelta.y) * k;
            focus.x = Mathf.Clamp(focus.x, -panLimit.x, panLimit.x);
            focus.z = Mathf.Clamp(focus.z, -panLimit.y, panLimit.y);
            Apply();
        }

        public void Zoom(float amount)
        {
            distance = Mathf.Clamp(distance * (1f - amount * zoomSpeed), minDistance, maxDistance);
            Apply();
        }

        void Update()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.touches.Count > 0)
            {
                var a = touch.touches[0];
                if (touch.touches.Count > 1 && touch.touches[1].isInProgress && a.isInProgress)
                {
                    float d = Vector2.Distance(a.position.ReadValue(), touch.touches[1].position.ReadValue());
                    if (_lastPinch > 0) Zoom((d - _lastPinch) * 0.05f);
                    _lastPinch = d;
                    return;
                }
                _lastPinch = 0;
                if (a.isInProgress) Pan(a.delta.ReadValue());
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.leftButton.isPressed) Pan(mouse.position.ReadValue() - _lastPointer);
            _lastPointer = mouse.position.ReadValue();
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) Zoom(Mathf.Sign(scroll));
        }

        void Apply()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focus - rotation * Vector3.forward * distance;
            transform.rotation = rotation;
        }
    }
}
