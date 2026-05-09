using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

/// ============================================================================
/// GridRenderer — 用 Cube 在 Game 视图中渲染方形网格地图
/// ============================================================================
///
/// 【渲染方式】
///   每个格子一个 Cube Primitive，缩成薄片（0.04 厚=边框，0.02 厚=填充）。
///   Cube 是 Unity 内置 Primitive，保证在任何渲染管线都能正常显示。
///
/// 【Inspector 参数】
///   所有颜色、摄像机参数均在 Inspector 可调。
/// ============================================================================
namespace ConquestGame
{
    public class GridRenderer : MonoBehaviour
    {
        [Header("边框")]
        [SerializeField] private Color borderColor = new Color(0.3f, 0.3f, 0.35f);

        [Header("城堡")]
        [SerializeField] private Color castlePlayerColor = new Color(0f, 0.7f, 0.7f);
        [SerializeField] private Color castleEnemyColor = new Color(0.7f, 0f, 0.7f);

        [Header("金矿")]
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

        private bool hasBuilt;
        private Material borderMat, castlePlayerMat, castleEnemyMat;
        private Material mineFreeMat, minePlayerMat, mineEnemyMat;
        private Material playerUnitMat, enemyUnitMat;

        private void Awake()
        {
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

            // === 格子 ===
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<HexCellData>(),
                                          ComponentType.ReadOnly<LocalTransform>());
            var cells = q.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var pos = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (cells.Length > 0)
            {
                for (int i = 0; i < cells.Length; i++)
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

            hasBuilt = true;
            Debug.Log("[Grid] 构建完成");

            // 测试：在原点放一个明显的红色大 Cube
            var test = GameObject.CreatePrimitive(PrimitiveType.Cube);
            test.name = "TEST_CUBE";
            test.transform.position = Vector3.zero;
            test.transform.localScale = Vector3.one * 2f;
            var testMR = test.GetComponent<MeshRenderer>();
            var testMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                                    ?? Shader.Find("Unlit/Color")
                                    ?? Shader.Find("Sprites/Default"));
            testMat.SetColor("_BaseColor", Color.red);
            testMat.SetColor("_Color", Color.red);
            testMR.sharedMaterial = testMat;
        }

        private void BuildCell(HexCellData cell, Vector3 worldPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Cell_{cell.Coordinates}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldPos;
            go.transform.localScale = new Vector3(0.96f, 0.04f, 0.96f);
            Destroy(go.GetComponent<Collider>());

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = borderMat;

            // 特殊建筑：在上面叠一层填充
            var fillMat = PickFillMat(cell);
            if (fillMat != null)
            {
                var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fill.name = "Fill";
                fill.transform.SetParent(go.transform, false);
                fill.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                fill.transform.localScale = new Vector3(0.88f, 0.02f, 0.88f);
                Destroy(fill.GetComponent<Collider>());
                fill.GetComponent<MeshRenderer>().sharedMaterial = fillMat;
            }
        }

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

        private static Material MakeMat(Shader s, Color c)
        {
            var m = new Material(s);
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
            cam.transform.position = new Vector3(5f, cameraHeight, 4f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.backgroundColor = backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
