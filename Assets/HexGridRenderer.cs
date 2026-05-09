using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

namespace ConquestGame
{
    public class HexGridRenderer : MonoBehaviour
    {
        private bool hasBuilt;

        private void Update()
        {
            if (!Application.isPlaying || hasBuilt)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
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

            // 调试：打印前5个格子的位置
            for (int i = 0; i < Mathf.Min(5, cells.Length); i++)
            {
                Debug.Log($"[HexGrid] Cell {i}: coord={cells[i].Coordinates} pos={(Vector3)transforms[i].Position} type={cells[i].CellType}");
            }

            // 测试 Shader
            var testShader = Shader.Find("Universal Render Pipeline/Unlit");
            var testShader2 = Shader.Find("Unlit/Color");
            Debug.Log($"[HexGrid] URP/Unlit={testShader!=null} Unlit/Color={testShader2!=null}");

            // 为每个格子创建一个子 GameObject（Cube/Sphere 调整形状）
            string shaderName = testShader != null ? "Universal Render Pipeline/Unlit" :
                                testShader2 != null ? "Unlit/Color" : "Sprites/Default";

            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var pos = (Vector3)transforms[i].Position;

                GameObject go;
                Color color;

                switch (cell.CellType)
                {
                    case CellType.Castle:
                        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.transform.localScale = Vector3.one * 1.0f;
                        color = cell.Owner == OwnerType.Player
                            ? new Color(0f, 0.85f, 0.85f)
                            : new Color(0.85f, 0f, 0.85f);
                        break;
                    case CellType.GoldMine:
                        go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        go.transform.localScale = Vector3.one * 0.8f;
                        color = cell.Owner switch
                        {
                            OwnerType.Player => new Color(0.2f, 0.7f, 0.2f),
                            OwnerType.Enemy => new Color(0.7f, 0.2f, 0.2f),
                            _ => new Color(0.95f, 0.75f, 0.05f)
                        };
                        break;
                    default: // Plain
                        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.transform.localScale = Vector3.one * 0.85f;
                        color = cell.Owner switch
                        {
                            OwnerType.Player => new Color(0.2f, 0.7f, 0.2f),
                            OwnerType.Enemy => new Color(0.7f, 0.2f, 0.2f),
                            _ => new Color(0.28f, 0.28f, 0.30f)
                        };
                        break;
                }

                go.name = $"Hex_{cell.Coordinates}";
                go.transform.position = pos;
                go.transform.SetParent(transform);

                var mr = go.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find(shaderName));
                mat.SetColor("_BaseColor", color);
                mat.color = color;
                mr.material = mat;
            }

            // 单位
            var unitQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var ut = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < units.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = units[i].Owner == OwnerType.Player ? "PlayerUnit" : "EnemyUnit";
                go.transform.position = (Vector3)ut[i].Position;
                go.transform.localScale = Vector3.one * 0.35f;
                go.transform.SetParent(transform);
                var mr = go.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find(shaderName));
                var uc = units[i].Owner == OwnerType.Player ? Color.blue : Color.red;
                mat.SetColor("_BaseColor", uc);
                mat.color = uc;
                mr.material = mat;
            }

            units.Dispose();
            ut.Dispose();
            cells.Dispose();
            transforms.Dispose();
            hasBuilt = true;
        }

        private void OnDestroy()
        {
            // 子对象随父对象自动清理
        }
    }
}
