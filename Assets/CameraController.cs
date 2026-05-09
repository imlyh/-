using UnityEngine;

/// ============================================================================
/// CameraController — 透视摄像机平移 + 缩放 + 初始定位
/// ============================================================================
///
/// 【操作方式】
///   鼠标右键拖拽 → 平移摄像机
///   滚轮 → 拉近/拉远
///
/// 【初始视角】
///   自动定位到玩家城堡上方（World 坐标 (0, 0, 4)）
/// ============================================================================
namespace ConquestGame
{
    public class CameraController : MonoBehaviour
    {
        [Header("初始目标")]
        [SerializeField] private Vector3 startLookTarget = new Vector3(0f, 0f, 4f);

        [Header("视角参数")]
        [SerializeField] private float initialDistance = 12f;
        [SerializeField] private float pitchAngle = 55f;     // 俯角
        [SerializeField] private float panSpeed = 0.3f;
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
        private Vector3 lastMouse;

        private void Start()
        {
            lookTarget = startLookTarget;
            currentZoom = initialDistance;
            UpdateCameraPosition();

            // 确保纯色背景（非天空盒）
            Camera cam = GetComponent<Camera>();
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
        /// 鼠标右键拖拽平移
        /// </summary>
        private void HandleMousePan()
        {
            if (Input.GetMouseButtonDown(1))
                lastMouse = Input.mousePosition;

            if (Input.GetMouseButton(1))
            {
                Vector3 delta = Input.mousePosition - lastMouse;
                lastMouse = Input.mousePosition;
                if (delta.magnitude < 0.5f) return;

                Vector3 right = transform.right;
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

                float factor = panSpeed * 0.01f * (currentZoom / initialDistance);
                lookTarget -= right * delta.x * factor;
                lookTarget -= forward * delta.y * factor;

                ClampAndApply();
            }
        }

        /// <summary>
        /// WASD 键盘备选平移
        /// </summary>
        private void HandleKeyboardPan()
        {
            float speed = panSpeed * 0.05f * (currentZoom / initialDistance);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = transform.right;

            bool moved = false;
            if (Input.GetKey(KeyCode.W)) { lookTarget += forward * speed; moved = true; }
            if (Input.GetKey(KeyCode.S)) { lookTarget -= forward * speed; moved = true; }
            if (Input.GetKey(KeyCode.A)) { lookTarget -= right * speed; moved = true; }
            if (Input.GetKey(KeyCode.D)) { lookTarget += right * speed; moved = true; }

            if (moved) ClampAndApply();
        }

        private void ClampAndApply()
        {
            lookTarget.x = Mathf.Clamp(lookTarget.x, minX, maxX);
            lookTarget.z = Mathf.Clamp(lookTarget.z, minZ, maxZ);
            UpdateCameraPosition();
        }

        /// <summary>
        /// 滚轮缩放：拉近/拉远
        /// </summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                currentZoom -= scroll * zoomSpeed;
                currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
                UpdateCameraPosition();
            }
        }

        /// <summary>
        /// 根据注视点和距离计算摄像机位置（保持固定俯角）
        /// </summary>
        private void UpdateCameraPosition()
        {
            float rad = pitchAngle * Mathf.Deg2Rad;
            float y = Mathf.Sin(rad) * currentZoom;
            float hDist = Mathf.Cos(rad) * currentZoom;

            // 从 lookTarget 向后方（-Z 方向）偏移
            transform.position = new Vector3(
                lookTarget.x,
                y,
                lookTarget.z - hDist
            );
            transform.LookAt(lookTarget);
        }

        /// <summary>
        /// 在 Inspector 中可视化初始注视点
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(startLookTarget, 0.3f);
        }
    }
}
