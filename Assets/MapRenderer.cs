using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using System.Collections.Generic;

/// ============================================================================
/// MapRenderer — 程序化生成地图：白模地形 + 半透明网格线 + 方块标记
/// ============================================================================
///
/// 【生成内容】
///   1. 白色大平面（地形基底）
///   2. 灰色半透明网格线（Cell 边界）
///   3. 青色/品红 Cube → 我方/敌方城堡
///   4. 黄色 Cube → 金矿
///   5. 蓝色/红色小 Cube → 玩家/敌方单位（每帧同步位置）
///
/// 【使用方式】
///   场景空 GameObject → 挂载 MapRenderer → Play
/// ============================================================================
namespace ConquestGame
{
    public class MapRenderer : MonoBehaviour
    {
        [Header("地图尺寸（需与 MapGenerationSystem 一致）")]
        [SerializeField] private int gridCols = 10;
        [SerializeField] private int gridRows = 8;

        [Header("颜色")]
        [SerializeField] private Color terrainColor = new Color(0.85f, 0.85f, 0.82f);
        [SerializeField] private Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        [SerializeField] private Color castlePlayerColor = new Color(0f, 0.7f, 0.7f);
        [SerializeField] private Color castleEnemyColor = new Color(0.7f, 0f, 0.7f);
        [SerializeField] private Color goldMineColor = new Color(0.9f, 0.7f, 0.05f);
        [SerializeField] private Color playerUnitColor = Color.blue;
        [SerializeField] private Color enemyUnitColor = Color.red;

        private bool hasBuilt;
        private Material gridLineMat;
        private Dictionary<int, GameObject> unitObjects = new Dictionary<int, GameObject>();
        private List<GameObject> staticObjects = new List<GameObject>();

        private void Awake()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");

            // 网格线使用透明材质
            gridLineMat = new Material(shader);
            gridLineMat.SetColor("_BaseColor", gridLineColor);
            gridLineMat.SetColor("_Color", gridLineColor);
            // 半透明需要设置渲染模式
            gridLineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            gridLineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            gridLineMat.SetInt("_ZWrite", 0);
            gridLineMat.renderQueue = 3000; // Transparent
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            if (!hasBuilt)
            {
                BuildTerrain();
                BuildGridLines();
                BuildStaticObjects(world);
                hasBuilt = true;
            }

            SyncUnitPositions(world);
        }

        // ===== 地形 =====

        /// <summary>
        /// 创建白色平面覆盖整个网格范围
        /// </summary>
        private void BuildTerrain()
        {
            var terrain = GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrain.name = "Terrain";
            terrain.transform.SetParent(transform);
            terrain.transform.position = new Vector3((gridCols - 1) * 0.5f, 0f, (gridRows - 1) * 0.5f);

            // Plane 默认 10×10 units → 缩放到覆盖网格
            float scaleX = gridCols / 10f;
            float scaleZ = gridRows / 10f;
            terrain.transform.localScale = new Vector3(scaleX, 1f, scaleZ);

            Destroy(terrain.GetComponent<Collider>());
            var mr = terrain.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MakeOpaqueMat(terrainColor);
            staticObjects.Add(terrain);
        }

        // ===== 网格线 =====

        /// <summary>
        /// 用细长 Cube 拼出半透明网格线
        /// 竖线在 x = -0.5, 0.5, 1.5, ..., 9.5
        /// 横线在 z = -0.5, 0.5, 1.5, ..., 7.5
        /// </summary>
        private void BuildGridLines()
        {
            var parent = new GameObject("GridLines");
            parent.transform.SetParent(transform);

            float zStart = -0.5f;
            float zEnd = gridRows - 0.5f;
            float zMid = (zStart + zEnd) * 0.5f;
            float zLen = zEnd - zStart;

            // 竖线
            for (int col = 0; col <= gridCols; col++)
            {
                float x = col - 0.5f;
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"VLine_{col}";
                line.transform.SetParent(parent.transform);
                line.transform.position = new Vector3(x, 0.01f, zMid);
                line.transform.localScale = new Vector3(0.02f, 0.01f, zLen);
                Destroy(line.GetComponent<Collider>());
                line.GetComponent<MeshRenderer>().sharedMaterial = gridLineMat;
            }

            float xStart = -0.5f;
            float xEnd = gridCols - 0.5f;
            float xMid = (xStart + xEnd) * 0.5f;
            float xLen = xEnd - xStart;

            // 横线
            for (int row = 0; row <= gridRows; row++)
            {
                float z = row - 0.5f;
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"HLine_{row}";
                line.transform.SetParent(parent.transform);
                line.transform.position = new Vector3(xMid, 0.01f, z);
                line.transform.localScale = new Vector3(xLen, 0.01f, 0.02f);
                Destroy(line.GetComponent<Collider>());
                line.GetComponent<MeshRenderer>().sharedMaterial = gridLineMat;
            }

            staticObjects.Add(parent);
        }

        // ===== 建筑和单位 =====

        /// <summary>
        /// 一次性创建城堡和金矿的静态方块
        /// </summary>
        private void BuildStaticObjects(World world)
        {
            var em = world.EntityManager;
            var parent = new GameObject("Structures");
            parent.transform.SetParent(transform);

            var q = em.CreateEntityQuery(ComponentType.ReadOnly<HexCellData>(),
                                          ComponentType.ReadOnly<LocalTransform>());
            var cells = q.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var pos = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < cells.Length; i++)
            {
                Vector3 wp = (Vector3)pos[i].Position;
                var cell = cells[i];
                Color color;
                if (cell.CellType == CellType.Castle)
                    color = cell.Owner == OwnerType.Player ? castlePlayerColor : castleEnemyColor;
                else if (cell.CellType == CellType.GoldMine)
                    color = goldMineColor;
                else
                    continue; // 平原不建方块

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = MapObjectName(cells[i]);
                go.transform.SetParent(parent.transform);
                float h = cells[i].CellType == CellType.Castle ? 0.5f : 0.3f;
                go.transform.position = wp + Vector3.up * (h * 0.5f);
                // 边长与格子一致（1×1），高度按建筑类型区分
                go.transform.localScale = cells[i].CellType == CellType.Castle
                    ? new Vector3(1f, 0.5f, 1f)
                    : new Vector3(1f, 0.3f, 1f);
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<MeshRenderer>().sharedMaterial = MakeOpaqueMat(color);
                staticObjects.Add(go);
            }

            cells.Dispose(); pos.Dispose();
        }

        /// <summary>
        /// 每帧同步单位位置
        /// </summary>
        private void SyncUnitPositions(World world)
        {
            var em = world.EntityManager;
            var uq = em.CreateEntityQuery(ComponentType.ReadOnly<UnitData>(),
                                           ComponentType.ReadOnly<LocalTransform>());
            var units = uq.ToComponentDataArray<UnitData>(Allocator.Temp);
            var ut = uq.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var seen = new HashSet<int>();

            for (int i = 0; i < units.Length; i++)
            {
                // 用 Entity Index 作为简单的追踪 key
                // 更准确的方法是用 Entity 的 Index+Version，这里简化处理
                int key = i;
                seen.Add(key);
                Vector3 wp = (Vector3)ut[i].Position + Vector3.up * 0.3f;

                if (unitObjects.TryGetValue(key, out var go))
                {
                    go.transform.position = wp;
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = units[i].Owner == OwnerType.Player ? "PlayerUnit" : "EnemyUnit";
                    go.transform.SetParent(transform);
                    go.transform.position = wp;
                    go.transform.localScale = Vector3.one * 0.3f;
                    Destroy(go.GetComponent<Collider>());
                    go.GetComponent<MeshRenderer>().sharedMaterial =
                        MakeOpaqueMat(units[i].Owner == OwnerType.Player ? playerUnitColor : enemyUnitColor);
                    unitObjects[key] = go;
                }
            }

            // 清理已销毁的单位
            var toRemove = new List<int>();
            foreach (var kv in unitObjects)
                if (!seen.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            foreach (var k in toRemove)
            {
                if (unitObjects[k] != null) Destroy(unitObjects[k]);
                unitObjects.Remove(k);
            }

            units.Dispose(); ut.Dispose();
        }

        // ===== 辅助方法 =====

        private static string MapObjectName(HexCellData cell)
        {
            if (cell.CellType == CellType.Castle)
                return cell.Owner == OwnerType.Player ? "PlayerCastle" : "EnemyCastle";
            if (cell.CellType == CellType.GoldMine)
                return $"GoldMine_{cell.Coordinates}";
            return "";
        }

        private Material MakeOpaqueMat(Color c)
        {
            var s = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Sprites/Default");
            var m = new Material(s);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            return m;
        }

        private void OnDestroy()
        {
            foreach (var go in staticObjects)
                if (go != null) Destroy(go);
            foreach (var kv in unitObjects)
                if (kv.Value != null) Destroy(kv.Value);
        }
    }
}
