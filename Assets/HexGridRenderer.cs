using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

/// ============================================================================
/// GridRenderer — 将 ECS 方形网格渲染到 Game 视图
/// ============================================================================
///
/// 【渲染方式】
///   每个格子一个薄 Cube（0.96×0.04×0.96），边框颜色统一灰色。
///   单位用彩色 Sphere 表示（蓝=玩家，红=敌人）。
///   特殊建筑不额外填充，仅通过边框颜色区分（Inspector 中可调）。
///
/// 【使用方式】
///   场景空 GameObject → 挂载 GridRenderer → Play 自动生成
/// ============================================================================
namespace ConquestGame
{
    public class GridRenderer : MonoBehaviour
    {
        [Header("边框")]
        [SerializeField] private Color borderColor = new Color(0.4f, 0.4f, 0.42f);

        [Header("单位")]
        [SerializeField] private Color playerUnitColor = Color.blue;
        [SerializeField] private Color enemyUnitColor = Color.red;

        [Header("背景")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.10f);

        [Header("摄像机")]
        [SerializeField] private Vector3 cameraPosition = new Vector3(4.5f, 12f, -5f);
        [SerializeField] private Vector3 cameraLookAt = new Vector3(4.5f, 0f, 3.5f);

        private bool hasBuilt;
        private Material borderMat, playerUnitMat, enemyUnitMat;

        private void Awake()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");

            borderMat = MakeMat(shader, borderColor);
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

            // === 格子 ===
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<HexCellData>(),
                                          ComponentType.ReadOnly<LocalTransform>());
            var cells = q.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var pos = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            int cellCount = cells.Length;
            if (cellCount > 0)
            {
                for (int i = 0; i < cellCount; i++)
                    BuildCell(cells[i], (Vector3)pos[i].Position);
            }
            cells.Dispose(); pos.Dispose();

            // === 单位 ===
            var uq = em.CreateEntityQuery(ComponentType.ReadOnly<UnitData>(),
                                           ComponentType.ReadOnly<LocalTransform>());
            var u = uq.ToComponentDataArray<UnitData>(Allocator.Temp);
            var ut = uq.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < u.Length; i++)
                BuildUnit(u[i], (Vector3)ut[i].Position);
            u.Dispose(); ut.Dispose();

            if (cellCount > 0 || u.Length > 0)
            {
                hasBuilt = true;
                Debug.Log($"[Grid] 构建完成：{cellCount} 格子 + {u.Length} 单位");
            }
            else
            {
                Debug.Log("[Grid] 等待 ECS 生成实体...");
            }
        }

        /// <summary>
        /// 为每个格子创建一个灰色薄 Cube 作为边框标记
        /// </summary>
        private void BuildCell(HexCellData cell, Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Cell_{cell.Coordinates}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;
            go.transform.localScale = new Vector3(0.96f, 0.04f, 0.96f);
            Destroy(go.GetComponent<Collider>());

            go.GetComponent<MeshRenderer>().sharedMaterial = borderMat;
        }

        /// <summary>
        /// 为单位创建 Sphere
        /// </summary>
        private void BuildUnit(UnitData unit, Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = unit.Owner == OwnerType.Player ? "PlayerUnit" : "EnemyUnit";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos + Vector3.up * 0.2f;
            go.transform.localScale = Vector3.one * 0.35f;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial =
                unit.Owner == OwnerType.Player ? playerUnitMat : enemyUnitMat;
        }

        private static Material MakeMat(Shader s, Color c)
        {
            var m = new Material(s);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            return m;
        }

        /// <summary>
        /// 设置透视摄像机，视角从上方倾斜俯视地图中心
        /// </summary>
        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = false;
            cam.transform.position = cameraPosition;
            cam.transform.LookAt(cameraLookAt);
            cam.backgroundColor = backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
