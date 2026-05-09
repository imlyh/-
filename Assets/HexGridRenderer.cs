using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

/// ============================================================================
/// HexGridRenderer — 将 ECS 中的六边形地图渲染到 Game 视图
/// ============================================================================
///
/// 【设计思路】
///   地图由六边形格子紧密排列而成。每个格子使用两个共享 Mesh 渲染：
///     1. 边框层（HexBorderMesh）—— 六边形环，所有格子都有
///     2. 填充层（HexFillMesh）  —— 实心六边形，仅特殊建筑（城堡/金矿）使用
///
///   这样普通平原只有白色细边框，城堡和金矿有带颜色的实心填充。
///
/// 【渲染架构】
///   GridRenderer（本脚本）
///     └── Hex_(q,r) × 93          → 只挂边框子对象
///     └── Hex_(q,r) × 3（特殊）   → 边框 + 填充子对象
///     └── PlayerUnit / EnemyWarrior → Sphere
///
/// 【Mesh 说明】
///   边框 Mesh 是半径差 0.04 的六边形环（外 0.50 / 内 0.46）
///   填充 Mesh 是半径 0.46 的六边形扇形（中心 + 6 三角）
///   所有同类型 GameObject 共享同一个 Mesh（sharedMesh），节省内存
///
/// 【使用方式】
///   场景创建空 GameObject → 挂载本脚本 → Play → 自动生成
/// ============================================================================
namespace ConquestGame
{
    public class HexGridRenderer : MonoBehaviour
    {
        private bool hasBuilt;

        // 共享 Mesh（所有格子复用）
        private static Mesh borderMesh;
        private static Mesh fillMesh;

        // 共享 Material（按颜色缓存）
        private Material borderMaterial;

        /// <summary>
        /// 生成六边形边框 Mesh：外环与内环之间的三角形环
        /// 顶点布局：0-5=外圈，6-11=内圈
        /// </summary>
        private static Mesh CreateBorderMesh()
        {
            const float outerR = 0.50f;
            const float innerR = 0.46f;

            var verts = new Vector3[12];
            var tris = new int[36];

            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                verts[i] = new Vector3(x * outerR, 0f, z * outerR);
                verts[i + 6] = new Vector3(x * innerR, 0f, z * innerR);
            }

            for (int i = 0; i < 6; i++)
            {
                int n = (i + 1) % 6;
                int t = i * 6;
                tris[t + 0] = i;
                tris[t + 1] = n;
                tris[t + 2] = i + 6;
                tris[t + 3] = i + 6;
                tris[t + 4] = n;
                tris[t + 5] = n + 6;
            }

            var m = new Mesh { name = "HexBorder" };
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// 生成六边形实心填充 Mesh：中心点 + 6 个角点组成 6 个三角形
        /// </summary>
        private static Mesh CreateFillMesh()
        {
            const float r = 0.45f;

            var verts = new Vector3[7];
            var tris = new int[18];

            verts[0] = Vector3.zero; // 中心

            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % 6 + 1;
            }

            var m = new Mesh { name = "HexFill" };
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Awake：创建共享 Mesh 和边框 Material（所有格子复用）
        /// </summary>
        private void Awake()
        {
            if (borderMesh == null)
                borderMesh = CreateBorderMesh();
            if (fillMesh == null)
                fillMesh = CreateFillMesh();

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");

            borderMaterial = new Material(shader);
            borderMaterial.color = Color.black;
            borderMaterial.SetColor("_BaseColor", Color.black);
            borderMaterial.SetColor("_Color", Color.black);
        }

        /// <summary>
        /// Update：首个有效帧读取 ECS 数据，为每个格子创建 GameObject
        /// </summary>
        private void Update()
        {
            if (!Application.isPlaying || hasBuilt)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            SetupCamera();

            var cellQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<HexCellData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var cells = cellQuery.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var transforms = cellQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            if (cells.Length == 0)
            {
                cells.Dispose();
                transforms.Dispose();
                return;
            }

            // 为每个格子创建边框 GameObject
            for (int i = 0; i < cells.Length; i++)
            {
                CreateCellObject(cells[i], (Vector3)transforms[i].Position);
            }

            // 为单位创建球形标记
            var unitQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var ut = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < units.Length; i++)
            {
                CreateUnitObject(units[i], (Vector3)ut[i].Position);
            }

            units.Dispose();
            ut.Dispose();
            cells.Dispose();
            transforms.Dispose();
            hasBuilt = true;
            Debug.Log($"[HexGrid] 构建完成：{cells.Length} 格子 + {units.Length} 单位");
        }

        /// <summary>
        /// 为一个格子创建 GameObject：
        ///   - 所有格子都有黑色边框子对象
        ///   - 城堡/金矿额外有带颜色填充子对象
        /// </summary>
        private void CreateCellObject(HexCellData cell, Vector3 worldPos)
        {
            var go = new GameObject($"Hex_{cell.Coordinates}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;

            // --- 边框层（所有格子都有）---
            var border = new GameObject("Border");
            border.transform.SetParent(go.transform, false);
            border.transform.localPosition = Vector3.zero;
            var bmf = border.AddComponent<MeshFilter>();
            bmf.sharedMesh = borderMesh;
            var bmr = border.AddComponent<MeshRenderer>();
            bmr.sharedMaterial = borderMaterial;

            // --- 填充层（仅特殊建筑）---
            if (cell.CellType == CellType.Castle || cell.CellType == CellType.GoldMine)
            {
                Color fillColor = cell.CellType switch
                {
                    CellType.Castle => cell.Owner == OwnerType.Player
                        ? new Color(0f, 0.7f, 0.7f)   // 玩家城堡 = 青色
                        : new Color(0.7f, 0f, 0.7f),  // 敌方城堡 = 品红
                    CellType.GoldMine => cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.2f, 0.6f, 0.2f),
                        OwnerType.Enemy => new Color(0.6f, 0.2f, 0.2f),
                        _ => new Color(0.9f, 0.7f, 0.05f) // 金黄色
                    },
                    _ => Color.gray
                };

                var fill = new GameObject("Fill");
                fill.transform.SetParent(go.transform, false);
                fill.transform.localPosition = Vector3.zero;
                var fmf = fill.AddComponent<MeshFilter>();
                fmf.sharedMesh = fillMesh;
                var fmr = fill.AddComponent<MeshRenderer>();

                var mat = new Material(borderMaterial.shader);
                mat.color = fillColor;
                mat.SetColor("_BaseColor", fillColor);
                mat.SetColor("_Color", fillColor);
                fmr.sharedMaterial = mat;  // 每种颜色各自独立 Material
            }
        }

        /// <summary>
        /// 为单位创建球形标记：蓝色=玩家，红色=敌人
        /// </summary>
        private void CreateUnitObject(UnitData unit, Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = unit.Owner == OwnerType.Player ? "PlayerUnit" : "EnemyUnit";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;
            go.transform.localScale = Vector3.one * 0.35f;

            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(borderMaterial.shader);
            var color = unit.Owner == OwnerType.Player ? Color.blue : Color.red;
            mat.color = color;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mr.sharedMaterial = mat;
        }

        /// <summary>
        /// 自动配置主摄像机为正交俯视模式（XZ 平面）
        /// </summary>
        private static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null || cam.orthographic)
                return;
            cam.orthographic = true;
            cam.orthographicSize = 11f;
            cam.transform.position = new Vector3(0f, 15f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
