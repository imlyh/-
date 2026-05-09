using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

namespace ConquestGame
{
    [ExecuteAlways]
    public class HexGridDebugger : MonoBehaviour
    {
        private static readonly Vector3[] HexCorners = new Vector3[6];
        private const float HexRadius = 0.55f;
        private const float Sqrt3 = 1.7320508f;

        static HexGridDebugger()
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i * Mathf.Deg2Rad;
                HexCorners[i] = new Vector3(Mathf.Cos(angle) * HexRadius, 0f, Mathf.Sin(angle) * HexRadius);
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var entityManager = world.EntityManager;

            // === 绘制 HexCell 六边形 ===
            var cellQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<HexCellData>(),
                ComponentType.ReadOnly<LocalTransform>());

            var cells = cellQuery.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var transforms = cellQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < cells.Length; i++)
            {
                var center = (Vector3)transforms[i].Position;
                var cell = cells[i];

                Color fillColor = cell.CellType switch
                {
                    CellType.Plain => cell.Owner switch
                    {
                        OwnerType.Player => new Color(0f, 0.8f, 0f, 0.4f),
                        OwnerType.Enemy => new Color(0.8f, 0f, 0f, 0.4f),
                        _ => new Color(0.3f, 0.3f, 0.3f, 0.3f)
                    },
                    CellType.Castle => cell.Owner == OwnerType.Player
                        ? new Color(0f, 1f, 1f, 0.5f)
                        : new Color(1f, 0f, 1f, 0.5f),
                    CellType.GoldMine => cell.Owner switch
                    {
                        OwnerType.Player => new Color(0f, 0.8f, 0f, 0.5f),
                        OwnerType.Enemy => new Color(0.8f, 0f, 0f, 0.5f),
                        _ => new Color(1f, 0.8f, 0f, 0.5f)
                    },
                    _ => Color.gray
                };

                // 用六个三角形拼成实心六边形
                DrawHexFill(center, fillColor);

                // 六边形边线
                Gizmos.color = cell.CellType switch
                {
                    CellType.Plain => new Color(0.6f, 0.6f, 0.6f, 0.5f),
                    CellType.Castle => Color.white,
                    CellType.GoldMine => new Color(1f, 0.8f, 0f, 0.8f),
                    _ => Color.white
                };
                DrawHexWire(center);
            }

            cells.Dispose();
            transforms.Dispose();

            // === 绘制 Unit ===
            var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());

            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var unitTransforms = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < units.Length; i++)
            {
                var pos = (Vector3)unitTransforms[i].Position;
                var unit = units[i];

                Gizmos.color = unit.Owner == OwnerType.Player
                    ? new Color(0.2f, 0.5f, 1f, 0.9f)
                    : new Color(1f, 0.2f, 0.2f, 0.9f);
                Gizmos.DrawSphere(pos, 0.3f);
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(pos, 0.33f);

#if UNITY_EDITOR
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(
                    pos + Vector3.up * 0.5f,
                    $"{(unit.Owner == OwnerType.Player ? "玩家" : "敌人")} HP:{unit.Health}/{unit.MaxHealth} ATK:{unit.Attack}");
#endif
            }

            units.Dispose();
            unitTransforms.Dispose();
        }

        private static void DrawHexFill(Vector3 center, Color color)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            var verts = new Vector3[7];
            for (int i = 0; i < 6; i++)
                verts[i] = center + HexCorners[i];
            verts[6] = center + HexCorners[0];
            // 用三角形扇形填充
            for (int i = 0; i < 6; i++)
            {
                UnityEditor.Handles.DrawAAConvexPolygon(
                    new[] { center, center + HexCorners[i], center + HexCorners[(i + 1) % 6] });
            }
#endif
        }

        private static void DrawHexWire(Vector3 center)
        {
            for (int i = 0; i < 6; i++)
            {
                var a = center + HexCorners[i];
                var b = center + HexCorners[(i + 1) % 6];
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
