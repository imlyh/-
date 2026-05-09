using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

/// ============================================================================
/// HexGridRenderer — 将 ECS 中的六边形地图实体渲染为 Game 视图可见的 GameObject
/// ============================================================================
///
/// 【设计思路】
///   ECS 的 Entity 默认不在 Game 视图中渲染。本脚本作为 ECS 与 Unity 渲染之间的桥接层，
///   在运行时从 ECS World 中读取 HexCellData/UnitData，为每个实体创建一个对应的
///   GameObject（Cube/Sphere），挂载到本对象下作为子节点。
///
///   这样做的原因是：
///   1. Entities Graphics 渲染管线配置复杂，原型阶段不适合投入大量时间
///   2. 直接用 GameObject 渲染可以立刻验证 ECS 逻辑是否正确
///   3. 后续可以替换为高效的 Entities Graphics 渲染
///
/// 【渲染架构】
///   - HexGridRenderer（本脚本）→ 挂载在空 GameObject 上
///     ├── Hex_*(-5,0)   Cube  灰色平原
///     ├── Hex_*(-4,0)   Cube  青色玩家城堡
///     ├── ...           ...
///     ├── Hex_*(1,0)    Sphere 黄色金矿
///     ├── Hex_*(4,0)    Cube  品红敌方城堡
///     ├── PlayerUnit    Sphere 蓝色
///     └── EnemyWarrior  Sphere 红色
///
/// 【材质与 Shader】
///   优先使用 URP Unlit → 失败则用内置 Unlit/Color → 最后使用 Custom/VertexColorUnlit
///   颜色通过 material.color 和 material.SetColor("_BaseColor") 双重设置兼容不同 Shader
///
/// 【使用方式】
///   1. 场景中创建空 GameObject 命名为 GridRenderer
///   2. 挂载本脚本
///   3. Play → 自动生成子对象
/// ============================================================================
namespace ConquestGame
{
    public class HexGridRenderer : MonoBehaviour
    {
        private bool hasBuilt;

        /// <summary>
        /// Update：仅在 Play 模式且尚未构建时执行一次
        /// 从 ECS World 读取所有 HexCell 和 Unit 实体，创建对应的 GameObject
        /// </summary>
        private void Update()
        {
            if (!Application.isPlaying || hasBuilt)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;

            // === 自动配置主摄像机 ===
            // 地图在 XZ 平面（Y=0），相机从上方俯视
            SetupCamera();

            // === 查询所有 HexCell 实体 ===
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

            // --- 确定可用的 Shader ---
            string shaderName = null;
            foreach (var name in new[] {
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                "Sprites/Default",
                "Custom/VertexColorUnlit"
            })
            {
                if (Shader.Find(name) != null)
                {
                    shaderName = name;
                    break;
                }
            }
            Debug.Log($"[HexGrid] 使用 Shader: {shaderName ?? "NULL"}");

            // --- 为每个 HexCell 创建 GameObject ---
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var pos = (Vector3)transforms[i].Position;

                CreateHexGameObject(cell, pos, shaderName);
            }

            // --- 为每个 Unit 创建 GameObject ---
            var unitQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var ut = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                var pos = (Vector3)ut[i].Position;
                CreateUnitGameObject(unit, pos, shaderName);
            }

            units.Dispose();
            ut.Dispose();
            cells.Dispose();
            transforms.Dispose();
            hasBuilt = true;
        }

        /// <summary>
        /// 根据 CellType 和 Owner 创建一个 Cube（平原/城堡）或 Sphere（金矿）子对象
        /// </summary>
        private void CreateHexGameObject(HexCellData cell, Vector3 worldPos, string shaderName)
        {
            GameObject go;
            Color color;

            switch (cell.CellType)
            {
                case CellType.Castle:
                    // 城堡用大号 Cube 表示
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = Vector3.one * 1.05f;
                    color = cell.Owner == OwnerType.Player
                        ? new Color(0f, 0.85f, 0.85f)   // 玩家城堡 = 青色
                        : new Color(0.85f, 0f, 0.85f);   // 敌方城堡 = 品红
                    break;

                case CellType.GoldMine:
                    // 金矿用 Sphere 表示，区别于平原的 Cube
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.localScale = Vector3.one * 0.85f;
                    color = cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.2f, 0.7f, 0.2f),
                        OwnerType.Enemy => new Color(0.7f, 0.2f, 0.2f),
                        _ => new Color(0.95f, 0.75f, 0.05f)  // 无主金矿 = 金黄色
                    };
                    break;

                default: // CellType.Plain
                    // 平原用 Cube，按归属着色
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = Vector3.one * 0.88f;
                    color = cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.2f, 0.7f, 0.2f),   // 玩家领地 = 绿色
                        OwnerType.Enemy => new Color(0.7f, 0.2f, 0.2f),    // 敌方领地 = 红色
                        _ => new Color(0.28f, 0.28f, 0.30f)                // 无主平原 = 深灰
                    };
                    break;
            }

            go.name = $"Hex_{cell.Coordinates}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;

            var mr = go.GetComponent<MeshRenderer>();
            if (shaderName != null)
            {
                var mat = new Material(Shader.Find(shaderName));
                mat.color = color;
                mat.SetColor("_BaseColor", color);  // URP Shader 用 _BaseColor
                mat.SetColor("_Color", color);      // 内置 Shader 用 _Color
                mr.material = mat;
            }
        }

        /// <summary>
        /// 为单位创建一个彩色 Sphere，蓝色=玩家，红色=敌人
        /// </summary>
        private void CreateUnitGameObject(UnitData unit, Vector3 worldPos, string shaderName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = unit.Owner == OwnerType.Player ? "PlayerUnit" : "EnemyUnit";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;
            go.transform.localScale = Vector3.one * 0.35f;

            var color = unit.Owner == OwnerType.Player ? Color.blue : Color.red;

            var mr = go.GetComponent<MeshRenderer>();
            if (shaderName != null)
            {
                var mat = new Material(Shader.Find(shaderName));
                mat.color = color;
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_Color", color);
                mr.material = mat;
            }
        }

        /// <summary>
        /// 自动把主摄像机设置为俯视 XZ 平面的正交模式
        /// 地图中心约在原点，范围约 ±9（X）× ±8（Z）
        /// </summary>
        private static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            // 仅在第一次调用时配置，避免每帧重置
            if (cam.orthographic && Mathf.Approximately(cam.orthographicSize, 11f))
                return;

            cam.orthographic = true;
            cam.orthographicSize = 11f;           // 视野覆盖 ~22 单位
            cam.transform.position = new Vector3(0f, 15f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.backgroundColor = Color.black;    // 黑色背景，突出网格
            cam.clearFlags = CameraClearFlags.SolidColor;

            Debug.Log("[HexGrid] 已自动配置摄像机：正交俯视，Size=11，黑背景");
        }
    }
}
