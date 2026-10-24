using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

namespace CityRise.CameraRig
{
    /// <summary>
    /// Şehir kamerası: tek parmakla kaydırma, iki parmakla yakınlaştırma.
    /// Editörde fareyle sürükleme ve tekerlek de çalışır.
    /// Bu bileşen kameranın üstündeki boş "rig" nesnesine takılır; kamera onun çocuğudur.
    /// </summary>
    public class CityCameraController : MonoBehaviour
    {
        public Camera cam;

        [Header("Kaydırma")]
        public float panSpeed = 1f;
        public Vector2 boundsMin = new Vector2(-60, -60);
        public Vector2 boundsMax = new Vector2(60, 60);

        [Header("Yakınlaştırma")]
        public float minDistance = 15f;
        public float maxDistance = 90f;
        public float zoomSpeed = 0.05f;
        public float distance = 45f;
        [Range(20, 80)] public float pitch = 45f;
        public float yaw = 45f;

        [Header("Eğim")]
        [Tooltip("Açıksa yakınlaştıkça kamera yatar ve ufuk/gökyüzü görünür; uzaklaştıkça tepeden bakar.")]
        public bool tiltWithZoom = false;
        [Range(10, 80)] public float pitchNear = 22f;
        [Range(10, 80)] public float pitchFar = 52f;

        [Header("Yörünge (iç görünüm)")]
        [Tooltip("Açıkken sürüklemek haritayı kaydırmaz, kamerayı merkez etrafında (sınırlı açıyla) döndürür.")]
        public bool orbitMode;
        public float orbitPitch = 38f;
        public float orbitYawMin = 0f;
        public float orbitYawMax = 90f;

        Vector3 lastGroundPoint;
        Vector2 lastScreen;
        bool dragging;
        bool pressStartedOnUI;
        Plane ground = new Plane(Vector3.up, Vector3.zero);

        void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Enable();
#endif
        }

        void Start()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            ApplyTransform();
        }

        void Update()
        {
            HandleInput();
            ApplyTransform();
        }

        /// <summary>Basış arayüzün (düğme, pencere) üstünde başladıysa harita kaydırılmaz.</summary>
        static bool PointerOverUI() =>
            UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var anyPointer = Pointer.current;
            if (anyPointer != null)
            {
                if (anyPointer.press.wasPressedThisFrame) pressStartedOnUI = PointerOverUI();
                if (!anyPointer.press.isPressed && !anyPointer.press.wasReleasedThisFrame) pressStartedOnUI = false;
            }
            if (pressStartedOnUI)
            {
                dragging = false;
                return;
            }
            var touches = Touch.activeTouches;
            if (touches.Count == 2)
            {
                var a = touches[0];
                var b = touches[1];
                float now = Vector2.Distance(a.screenPosition, b.screenPosition);
                float before = Vector2.Distance(a.screenPosition - a.delta, b.screenPosition - b.delta);
                Zoom((before - now) * zoomSpeed);
                dragging = false;
                return;
            }
            if (touches.Count == 1)
            {
                Drag(touches[0].screenPosition, touches[0].phase == UnityEngine.InputSystem.TouchPhase.Began);
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom(-Mathf.Sign(scroll) * 3f);
                if (mouse.leftButton.isPressed)
                {
                    Drag(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame);
                    return;
                }
            }
#else
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                float now = Vector2.Distance(a.position, b.position);
                float before = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition);
                Zoom((before - now) * zoomSpeed);
                dragging = false;
                return;
            }
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                Drag(t.position, t.phase == UnityEngine.TouchPhase.Began);
                return;
            }
            if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) Zoom(-Mathf.Sign(Input.mouseScrollDelta.y) * 3f);
            if (Input.GetMouseButton(0))
            {
                Drag(Input.mousePosition, Input.GetMouseButtonDown(0));
                return;
            }
#endif
            dragging = false;
        }

        void Drag(Vector2 screenPos, bool began)
        {
            if (orbitMode)
            {
                if (!began && dragging) yaw = Mathf.Clamp(yaw + (screenPos.x - lastScreen.x) * 0.25f, orbitYawMin, orbitYawMax);
                lastScreen = screenPos;
                dragging = true;
                return;
            }
            if (!TryGroundPoint(screenPos, out var point)) return;
            if (began || !dragging)
            {
                lastGroundPoint = point;
                dragging = true;
                return;
            }
            // Parmağın altındaki zemin noktası parmakla birlikte hareket eder.
            Vector3 delta = lastGroundPoint - point;
            transform.position += new Vector3(delta.x, 0, delta.z) * panSpeed;
            ClampPosition();
            ApplyTransform();
            TryGroundPoint(screenPos, out lastGroundPoint);
        }

        void Zoom(float amount)
        {
            distance = Mathf.Clamp(distance + amount, minDistance, maxDistance);
        }

        bool TryGroundPoint(Vector2 screenPos, out Vector3 point)
        {
            point = Vector3.zero;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(screenPos);
            if (!ground.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        void ClampPosition()
        {
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, boundsMin.x, boundsMax.x);
            p.z = Mathf.Clamp(p.z, boundsMin.y, boundsMax.y);
            transform.position = p;
        }

        void ApplyTransform()
        {
            if (cam == null) return;
            float p = orbitMode ? orbitPitch
                : tiltWithZoom ? Mathf.Lerp(pitchNear, pitchFar, Mathf.InverseLerp(minDistance, maxDistance, distance)) : pitch;
            var rot = Quaternion.Euler(p, yaw, 0);
            cam.transform.position = transform.position - rot * Vector3.forward * distance;
            cam.transform.rotation = rot;
        }
    }
}
