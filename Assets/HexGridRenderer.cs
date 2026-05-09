using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

/// ============================================================================
/// GridRenderer — 将 ECS 方形网格地图渲染到 Game 视图
/// ============================================================================
///
/// 【设计思路】
///   每个格子是 1×1 的紧密排列方形。边框使用方框 Mesh（外框 0.50×0.50，
///   内框 0.46×0.46），填充使用实心方形 Mesh（0.45×0.45）。
///   所有普通格子只渲染边框，城堡和金矿额外渲染彩色填充。
///
/// 【渲染结构】
///   GridRenderer（本脚本）
///     ├── Cell_* × N           → MeshRenderer 渲染边框
///     │   └── Fill（可选）      → 城堡/金矿的实心填充
///     ├── PlayerUnit            → 蓝色 Sphere
///     └── EnemyWarrior          → 红色 Sphere
///
/// 【Inspector 参数】
///   边框、填充、单位、背景、摄像机均可调颜色
/// ============================================================================
namespace ConquestGame
{
    public class GridRenderer : MonoBehaviour
    {
        // ===== Inspector 颜色 =====
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
        [SerializeField] private float cameraSize = 7f;

        // ===== 内部状态 =====
        private bool hasBuilt;
        private static Mesh borderMesh;
        private static Mesh fillMesh;
        private Material borderMat;
        private Material castlePlayerMat, castleEnemyMat;
        private Material mineFreeMat, minePlayerMat, mineEnemyMat;
        private Material playerUnitMat, enemyUnitMat;

        // ===== 共享 Mesh 创建 =====

        /// <summary>
        /// 创建方框 Mesh：外框 0.50 内框 0.46，形成 0.04 宽的边框线
        /// </summary>
        private static Mesh CreateBorderMesh()
        {
            const float o = 0.50f, i = 0.46f;
            // 8 个顶点：外框 4 个 + 内框 4 个
            var verts = new Vector3[] {
                new(-o, 0f, -o), new( o, 0f, -o), new( o, 0f,  o), new(-o, 0f,  o), // outer
                new(-i, 0f, -i), new( i, 0f, -i), new( i, 0f,  i), new(-i, 0f,  i), // inner
            };
            // 4 个 quad → 8 个三角形 = 24 个索引
            var tris = new int[24];
            for (int e = 0; e < 4; e++)
            {
                int n = (e + 1) % 4;
                int t = e * 6;
                tris[t + 0] = e; tris[t + 1] = n; tris[t + 2] = e + 4;
                tris[t + 3] = e + 4; tris[t + 4] = n; tris[t + 5] = n + 4;
            }
            var m = new Mesh { name = "SquareBorder" };
            m.vertices = verts; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// 创建实心方形 Mesh：0.45×0.45
        /// </summary>
        private static Mesh CreateFillMesh()
        {
            const float r = 0.45f;
            var verts = new Vector3[] {
                new(-r, 0f, -r), new( r, 0f, -r), new( r, 0f,  r), new(-r, 0f,  r)
            };
            var tris = new int[] { 0, 1, 2, 0, 2, 3 };
            var m = new Mesh { name = "SquareFill" };
            m.vertices = verts; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ===== Unity 生命周期 =====

        private void Awake()
        {
            if (borderMesh == null) borderMesh = CreateBorderMesh();
            if (fillMesh == null) fillMesh = CreateFillMesh();

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");

            borderMat = MakeMat(shader, borderColor);
            castlePlayerMat = MakeMat(shader, castlePlayerColor);
            castleEnemyMat = MakeMat(shader, castleEnemyColor);
            mineFreeMat = MakeMat(shader, mineFreeColor);
            minePlayerMat = MakeMat(shader, minePlayerColor);
            mineEnemyMat = MakeMat(shader, mineEnemyColor);
            playerUnitMat = MakeMat(shader, playerUnitColor);
            enemyUnitMat = MakeMat(shader, enemyUnitColor);
        }

        private void Update()
        {
            if (!Application.isPlaying || hasBuilt) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            SetupCamera();

            var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<HexCellData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var cells = q.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var pos = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (cells.Length == 0) { cells.Dispose(); pos.Dispose(); return; }

            for (int i = 0; i < cells.Length; i++)
                BuildCell(cells[i], (Vector3)pos[i].Position);

            var uq = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var u = uq.ToComponentDataArray<UnitData>(Allocator.Temp);
            var ut = uq.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < u.Length; i++)
                BuildUnit(u[i], (Vector3)ut[i].Position);

            u.Dispose(); ut.Dispose(); cells.Dispose(); pos.Dispose();
            hasBuilt = true;
            Debug.Log($"[Grid] 构建完成：{cells.Length} 格子 + {u.Length} 单位");
        }

        // ===== 构建逻辑 =====

        /// <summary>
        /// 为一个格子创建 GameObject：边框（必备）+ 填充（仅特殊建筑）
        /// </summary>
        private void BuildCell(HexCellData cell, Vector3 worldPos)
        {
            var go = new GameObject($"Cell_{cell.Coordinates}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;

            // 边框
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = borderMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = borderMat;

            // 填充层
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
        /// 为单位创建球形标记
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
            Destroy(go.GetComponent<Collider>());
        }

        private Material PickFillMat(HexCellData cell)
        {
            if (cell.CellType == CellType.Castle)
                return cell.Owner == OwnerType.Player ? castlePlayerMat : castleEnemyMat;
            if (cell.CellType == CellType.GoldMine)
                return cell.Owner switch
                {
                    OwnerType.Player => minePlayerMat,
                    OwnerType.Enemy => mineEnemyMat,
                    _ => mineFreeMat
                };
            return null;
        }

        // ===== 辅助 =====

        private static Material MakeMat(Shader s, Color c)
        {
            var m = new Material(s);
            m.color = c;
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            return m;
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.transform.position = new Vector3(4.5f, cameraHeight, 3.5f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.backgroundColor = backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
