using UnityEngine;
using UnityEngine.InputSystem;

/// ============================================================================
/// CameraController — 透视摄像机控制：WASD平移、右键拖拽、滚轮缩放、边界限制
/// ============================================================================
public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float panSpeed = 15f;
    public float dragSpeed = 0.3f;
    public float zoomSpeed = 8f;

    [Header("Zoom")]
    public float minHeight = 5f;
    public float maxHeight = 12f;

    [Header("Pitch")]
    public float pitchAngle = 45f;

    [Header("Bounds (world XZ)")]
    public float boundMinX = -10f;
    public float boundMaxX = 39f;
    public float boundMinZ = -10f;
    public float boundMaxZ = 29f;

    private Camera cam;
    private Vector3 lookTarget;   // 摄像机注视的地面点
    private float currentHeight;

    void Awake()
    {
        cam = GetComponent<Camera>();
        // 从当前Transform反算初始lookTarget和height
        currentHeight = transform.position.y;
        float backDist = currentHeight / Mathf.Tan(pitchAngle * Mathf.Deg2Rad);
        lookTarget = transform.position + Vector3.forward * backDist;
        lookTarget.y = 0f;
    }

    void Update()
    {
        HandleKeyboardPan();
        HandleMouseDrag();
        HandleZoom();
        ClampLookTarget();
        ApplyPosition();
    }

    void HandleKeyboardPan()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move.z += 1f;
        if (kb.sKey.isPressed) move.z -= 1f;
        if (kb.aKey.isPressed) move.x -= 1f;
        if (kb.dKey.isPressed) move.x += 1f;

        if (move != Vector3.zero)
            lookTarget += move.normalized * (panSpeed * Time.deltaTime);
    }

    void HandleMouseDrag()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            // 鼠标向右拖 → 地图向右 → lookTarget.x 增加
            // 鼠标向上拖 → 地图向上（远处）→ lookTarget.z 增加
            lookTarget.x += delta.x * dragSpeed * Time.deltaTime;
            lookTarget.z += delta.y * dragSpeed * Time.deltaTime;
        }
    }

    void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y / 120f; // 一格=1
        if (Mathf.Abs(scroll) > 0.001f)
        {
            currentHeight -= scroll * zoomSpeed;
            currentHeight = Mathf.Clamp(currentHeight, minHeight, maxHeight);
        }
    }

    void ClampLookTarget()
    {
        // 根据当前高度计算可见区域边距
        float farMargin = currentHeight * 2.73f;  // tan(75°)-1  at 45° pitch
        float nearMargin = currentHeight * 0.732f; // 1-tan(15°) at 45° pitch
        float sideMargin = currentHeight * 1.45f;  // half horizontal extent at center

        float minX = Mathf.Max(boundMinX, boundMinX + sideMargin);
        float maxX = Mathf.Min(boundMaxX, boundMaxX - sideMargin);
        float minZ = Mathf.Max(boundMinZ, boundMinZ + nearMargin);
        float maxZ = Mathf.Min(boundMaxZ, boundMaxZ - farMargin);

        // 如果边距太大导致区间反转，固定在中心
        if (minX > maxX) minX = maxX = (boundMinX + boundMaxX) * 0.5f;
        if (minZ > maxZ) minZ = maxZ = (boundMinZ + boundMaxZ) * 0.5f;

        lookTarget.x = Mathf.Clamp(lookTarget.x, minX, maxX);
        lookTarget.z = Mathf.Clamp(lookTarget.z, minZ, maxZ);
    }

    void ApplyPosition()
    {
        float backDist = currentHeight / Mathf.Tan(pitchAngle * Mathf.Deg2Rad);
        Vector3 camPos = lookTarget;
        camPos.y = currentHeight;
        camPos.z -= backDist;
        transform.position = camPos;
        transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
    }
}
