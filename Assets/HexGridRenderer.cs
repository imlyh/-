using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

/// ============================================================================
/// HexGridRenderer — 将 ECS 六边形地图渲染到 Game 视图
/// ============================================================================
///
/// 【设计思路】
///   为每个 HexCell 创建一个 GameObject，挂载自定义六边形 Mesh。
///   普通格子只画边框（六边形环），城堡和金矿额外叠加实心填充层。
///   所有同类型格子共享同一个 Mesh（sharedMesh）节省内存。
///
/// 【渲染层级】
///   每个 Cell GameObject 上有：
///     - MeshFilter + MeshRenderer → 六边形边框（所有格子）
///     - 额外子对象 Fill → 实心填充（仅城堡/金矿）
///
/// 【关键参数】
///   边框外径 0.52 / 内径 0.42，填充半径 0.41
///   六边形紧密排列，相邻格子中心距 ≈ 1 个单位
/// ============================================================================
namespace ConquestGame
{
    public class HexGridRenderer : MonoBehaviour
    {
        // ===== Inspector 可调颜色 =====
        [Header("边框")]
        [SerializeField] private Color borderColor = Color.white;

        [Header("城堡填充")]
        [SerializeField] private Color castlePlayerColor = new Color(0f, 0.7f, 0.7f);
        [SerializeField] private Color castleEnemyColor = new Color(0.7f, 0f, 0.7f);

        [Header("金矿填充（无主/玩家/敌人）")]
        [SerializeField] private Color mineFreeColor = new Color(0.9f, 0.7f, 0.05f);
        [SerializeField] private Color minePlayerColor = new Color(0.2f, 0.6f, 0.2f);
        [SerializeField] private Color mineEnemyColor = new Color(0.6f, 0.2f, 0.2f);

        [Header("单位")]
        [SerializeField] private Color playerUnitColor = Color.blue;
        [SerializeField] private Color enemyUnitColor = Color.red;

        [Header("背景")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.10f);

        [Header("摄像机")]
        [SerializeField] private float cameraHeight = 15f;
        [SerializeField] private float cameraSize = 11f;

        // ===== 内部状态 =====
        private bool hasBuilt;
        private static Mesh borderMesh;
        private static Mesh fillMesh;
        private Material fillMaterialCastlePlayer, fillMaterialCastleEnemy;
        private Material fillMaterialMineFree, fillMaterialMinePlayer, fillMaterialMineEnemy;
        private Material borderMat;
        private Material playerUnitMat, enemyUnitMat;

        /// <summary>
        /// 创建六边形边框 Mesh：外圈 + 内圈之间的环带
        /// </summary>
        /// <summary>
        /// 创建尖顶六边形边框 Mesh（匹配 R = 0.5 的无缝排列）
        /// </summary>
        private static Mesh CreateBorderMesh()
        {
            const float outer = 0.50f, inner = 0.45f;
            var verts = new Vector3[12];
            var tris = new int[36];
            for (int i = 0; i < 6; i++)
            {
                float a = (90f + 60f * i) * Mathf.Deg2Rad;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                verts[i] = new Vector3(x * outer, 0f, z * outer);
                verts[i + 6] = new Vector3(x * inner, 0f, z * inner);
            }
            for (int i = 0; i < 6; i++)
            {
                int n = (i + 1) % 6;
                int t = i * 6;
                tris[t + 0] = i; tris[t + 1] = n; tris[t + 2] = i + 6;
                tris[t + 3] = i + 6; tris[t + 4] = n; tris[t + 5] = n + 6;
            }
            var m = new Mesh { name = "HexBorder" };
            m.vertices = verts; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// 创建尖顶六边形实心填充 Mesh（半径 0.44，略小于边框内径）
        /// </summary>
        private static Mesh CreateFillMesh()
        {
            const float r = 0.44f;
            var verts = new Vector3[7];
            var tris = new int[18];
            verts[0] = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                float a = (90f + 60f * i) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < 6; i++)
            {
                int n = (i + 1) % 6;
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = n + 1;
            }
            var m = new Mesh { name = "HexFill" };
            m.vertices = verts; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Awake：创建共享 Mesh + 材质缓存
        /// </summary>
        private void Awake()
        {
            if (borderMesh == null) borderMesh = CreateBorderMesh();
            if (fillMesh == null) fillMesh = CreateFillMesh();

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");

            borderMat = MakeMat(shader, borderColor);
            fillMaterialCastlePlayer = MakeMat(shader, castlePlayerColor);
            fillMaterialCastleEnemy = MakeMat(shader, castleEnemyColor);
            fillMaterialMineFree = MakeMat(shader, mineFreeColor);
            fillMaterialMinePlayer = MakeMat(shader, minePlayerColor);
            fillMaterialMineEnemy = MakeMat(shader, mineEnemyColor);
            playerUnitMat = MakeMat(shader, playerUnitColor);
            enemyUnitMat = MakeMat(shader, enemyUnitColor);
        }

        private static Material MakeMat(Shader s, Color c)
        {
            var m = new Material(s);
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            return m;
        }

        /// <summary>
        /// 选取填充材质
        /// </summary>
        private Material PickFillMat(HexCellData cell)
        {
            if (cell.CellType == CellType.Castle)
                return cell.Owner == OwnerType.Player ? fillMaterialCastlePlayer : fillMaterialCastleEnemy;
            if (cell.CellType == CellType.GoldMine)
                return cell.Owner switch
                {
                    OwnerType.Player => fillMaterialMinePlayer,
                    OwnerType.Enemy => fillMaterialMineEnemy,
                    _ => fillMaterialMineFree
                };
            return null;
        }

        /// <summary>
        /// Update：首个有效帧构建所有渲染对象
        /// </summary>
        private void Update()
        {
            if (!Application.isPlaying || hasBuilt) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            SetupCamera();

            var q = em.CreateEntityQuery(ComponentType.ReadOnly<HexCellData>(),
                                          ComponentType.ReadOnly<LocalTransform>());
            var cells = q.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var pos = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (cells.Length == 0) { cells.Dispose(); pos.Dispose(); return; }

            // 缓存世界坐标用于 LateUpdate 的 Debug.DrawLine
            cellCount = cells.Length;
            cellPositions = new Vector3[cellCount];

            for (int i = 0; i < cells.Length; i++)
            {
                Vector3 wp = (Vector3)pos[i].Position;
                cellPositions[i] = wp;
                BuildCell(cells[i], wp);
            }

            // 单位
            var uq = em.CreateEntityQuery(ComponentType.ReadOnly<UnitData>(),
                                           ComponentType.ReadOnly<LocalTransform>());
            var u = uq.ToComponentDataArray<UnitData>(Allocator.Temp);
            var ut = uq.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < u.Length; i++)
                BuildUnit(u[i], (Vector3)ut[i].Position);

            u.Dispose(); ut.Dispose(); cells.Dispose(); pos.Dispose();
            hasBuilt = true;
            Debug.Log($"[HexGrid] 构建完成");
        }

        /// <summary>
        /// 创建材质并设置颜色（UPR Unlit / 内置管线兼容）
        /// </summary>

        /// <summary>
        /// 为一个格子创建 GameObject，直接挂 MeshRenderer（边框）+ 可选子 Fill
        /// </summary>
        private void BuildCell(HexCellData cell, Vector3 worldPos)
        {
            var go = new GameObject($"Hex_{cell.Coordinates}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;

            // 边框 Mesh（直接在根节点上）
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = borderMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = borderMat;

            // 填充层（仅特殊建筑）
            var fillMat = PickFillMat(cell);
            if (fillMat != null)
            {
                var fill = new GameObject("Fill");
                fill.transform.SetParent(go.transform, false);
                fill.transform.localPosition = Vector3.zero;
                var fmf = fill.AddComponent<MeshFilter>();
                fmf.sharedMesh = fillMesh;
                var fmr = fill.AddComponent<MeshRenderer>();
                fmr.sharedMaterial = fillMat;
            }
        }

        /// <summary>
        /// 为单位创建 Sphere
        /// </summary>
        private void BuildUnit(UnitData unit, Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = unit.Owner == OwnerType.Player ? "PlayerUnit" : "EnemyUnit";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;
            go.transform.localScale = Vector3.one * 0.35f;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = unit.Owner == OwnerType.Player ? playerUnitMat : enemyUnitMat;
        }

        // 缓存格子的世界坐标用于 Debug 绘制
        private Vector3[] cellPositions;
        private int cellCount;

        /// <summary>
        /// 每帧用 Debug.DrawLine 绘制六边形边框（Game/Scene 视图均可见）
        /// </summary>
        private void LateUpdate()
        {
            if (!hasBuilt || cellPositions == null)
                return;

            const float r = 0.50f;
            var corners = new Vector3[6];
            Color borderC = borderColor;

            for (int i = 0; i < cellCount; i++)
            {
                Vector3 center = cellPositions[i];

                // 计算六边形 6 个角
                for (int j = 0; j < 6; j++)
                {
                    float a = (90f + 60f * j) * Mathf.Deg2Rad;
                    corners[j] = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }

                // 画出 6 条边
                for (int j = 0; j < 6; j++)
                {
                    Debug.DrawLine(corners[j], corners[(j + 1) % 6], borderC, 0f, false);
                }
            }
        }

        /// <summary>
        /// 自动设置摄像机
        /// </summary>
        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.transform.position = new Vector3(0f, cameraHeight, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.backgroundColor = backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
