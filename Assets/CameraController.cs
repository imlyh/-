using UnityEngine;
using UnityEngine.InputSystem;

/// ============================================================================
/// CameraController — 透视摄像机平移 + 缩放（支持 Input System）
/// ============================================================================
///
/// 【操作】
///   鼠标右键拖拽 → 平移 | WASD → 平移 | 滚轮 → 缩放
///   启动时自动注视玩家城堡 (0, 0, 4)
/// ============================================================================
namespace ConquestGame
{
    public class CameraController : MonoBehaviour
    {
        [Header("初始目标")]
        [SerializeField] private Vector3 startLookTarget = new Vector3(0f, 0f, 4f);

        [Header("视角")]
        [SerializeField] private float initialDistance = 12f;
        [SerializeField] private float pitchAngle = 55f;
        [SerializeField] private float panSpeed = 10f;
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 30f;

        [Header("边界")]
        [SerializeField] private float minX = -3f;
        [SerializeField] private float maxX = 12f;
        [SerializeField] private float minZ = -3f;
        [SerializeField] private float maxZ = 10f;

        private Vector3 lookTarget;
        private float currentZoom;
        private bool isDragging;
        private Vector2 lastMouse;

        private void Start()
        {
            lookTarget = startLookTarget;
            currentZoom = initialDistance;
            UpdateCameraPosition();

            var cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
            Debug.Log($"[Camera] 初始化完成，注视 {lookTarget}，距离 {currentZoom}");
        }

        private void Update()
        {
            HandleMousePan();
            HandleKeyboardPan();
            HandleZoom();
        }

        /// <summary>
        /// 鼠标右键拖拽平移（Input System API）
        /// </summary>
        private void HandleMousePan()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastMouse = mouse.position.value;
            }

            if (mouse.rightButton.wasReleasedThisFrame)
                isDragging = false;

            if (isDragging)
            {
                Vector2 delta = mouse.position.value - lastMouse;
                lastMouse = mouse.position.value;
                if (delta.magnitude < 0.5f) return;

                Vector3 right = transform.right;
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

                float factor = panSpeed * 0.001f * (currentZoom / initialDistance);
                lookTarget -= right * delta.x * factor;
                lookTarget -= forward * delta.y * factor;

                ClampAndApply();
            }
        }

        /// <summary>
        /// WASD 键盘平移（Input System API）
        /// </summary>
        private void HandleKeyboardPan()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float speed = panSpeed * 0.005f * (currentZoom / initialDistance);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = transform.right;

            bool moved = false;
            if (kb.wKey.isPressed)  { lookTarget += forward * speed; moved = true; }
            if (kb.sKey.isPressed)  { lookTarget -= forward * speed; moved = true; }
            if (kb.aKey.isPressed)  { lookTarget -= right * speed;   moved = true; }
            if (kb.dKey.isPressed)  { lookTarget += right * speed;   moved = true; }

            if (moved) ClampAndApply();
        }

        /// <summary>
        /// 滚轮缩放
        /// </summary>
        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.y.value / 120f; // 一格 = 120
            if (scroll == 0f) return;

            currentZoom -= scroll * zoomSpeed;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            UpdateCameraPosition();
        }

        private void ClampAndApply()
        {
            lookTarget.x = Mathf.Clamp(lookTarget.x, minX, maxX);
            lookTarget.z = Mathf.Clamp(lookTarget.z, minZ, maxZ);
            UpdateCameraPosition();
        }

        /// <summary>
        /// 根据注视点和距离计算摄像机位置（固定俯角）
        /// </summary>
        private void UpdateCameraPosition()
        {
            float rad = pitchAngle * Mathf.Deg2Rad;
            float y = Mathf.Sin(rad) * currentZoom;
            float hDist = Mathf.Cos(rad) * currentZoom;

            transform.position = new Vector3(lookTarget.x, y, lookTarget.z - hDist);
            transform.LookAt(lookTarget);
        }
    }
}
