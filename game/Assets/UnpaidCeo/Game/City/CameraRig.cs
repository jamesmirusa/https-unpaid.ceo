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
        [SerializeField] float distance = 70f, minDistance = 24f, maxDistance = 120f;
        [SerializeField] float pitch = 38f, yaw = 45f;
        [SerializeField] Vector2 panLimit = new Vector2(45f, 30f);
        [SerializeField] float panGrab = 1.2f, zoomSpeed = 0.05f;

        Vector2 _lastPointer;
        float _lastPinch;

        public Vector3 Focus => focus;
        public float Distance => distance;

        /// <summary>A popup or dialogue is open: drags and the wheel belong to it, not to the city.</summary>
        public static bool InputBlocked { get; set; }
        /// <summary>A scene's dialogue is on screen: taps continue it.</summary>
        public static bool SceneOpen { get; set; }

        /// <summary>Looks at a point from a distance (both kept inside the district's limits), e.g. at the hero in front of home.</summary>
        /// <param name="raise">Shows the point above the middle of the screen by this share of its height (the bottom of the screen is under the UI).</param>
        public void Frame(Vector3 point, float fromDistance, float raise = 0f)
        {
            distance = Mathf.Clamp(fromDistance, minDistance, maxDistance);
            if (raise != 0f)
            {
                // Looking at a ground point nearer the camera lifts everything behind it on the screen.
                float screenHeight = 2f * distance * Mathf.Tan(GetComponent<Camera>().fieldOfView * 0.5f * Mathf.Deg2Rad);
                point -= Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (raise * screenHeight / Mathf.Sin(pitch * Mathf.Deg2Rad));
            }
            focus = new Vector3(Mathf.Clamp(point.x, -panLimit.x, panLimit.x), point.y, Mathf.Clamp(point.z, -panLimit.y, panLimit.y));
            Apply();
        }

        void OnEnable() => Apply();

        /// <summary>Places the camera from focus/distance/angles (also usable in edit mode, e.g. by the district builder).</summary>
        public void Snap() => Apply();

        public void Pan(Vector2 screenDelta)
        {
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            // "Grab the ground": a drag across the whole screen moves the view by about what the screen shows, whatever the
            // pixel density (the old per-pixel speed flew across the block on a retina canvas).
            float k = distance * panGrab / Mathf.Max(1, Screen.height);
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
            if (InputBlocked || SceneOpen)
            {
                if (Mouse.current != null) _lastPointer = Mouse.current.position.ReadValue();
                _lastPinch = 0;
                return;
            }
            // Touchscreen.touches is a fixed array of controls (its Count is never 0): only a touch in progress means the
            // finger is driving the camera; otherwise the mouse still does, also on a laptop or browser that reports a touchscreen.
            var touch = Touchscreen.current;
            if (touch != null && touch.touches[0].isInProgress)
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
            if (mouse.leftButton.wasPressedThisFrame) _lastPointer = mouse.position.ReadValue();   // a drag starts here, not at (0,0)
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
