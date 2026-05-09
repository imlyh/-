using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

namespace ConquestGame
{
    [ExecuteAlways]
    public class HexGridDebugger : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var entityManager = world.EntityManager;

            // === 绘制 HexCell ===
            var cellQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<HexCellData>(),
                ComponentType.ReadOnly<LocalTransform>());

            var cells = cellQuery.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var transforms = cellQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // 先画连线（六边形网格结构）
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            for (int i = 0; i < cells.Length; i++)
            {
                var pos = (Vector3)transforms[i].Position;
                foreach (var dir in HexCoordinates.Directions)
                {
                    var neighborCoord = cells[i].Coordinates + dir;
                    var neighborPos = HexUtils.ToWorldPosition(neighborCoord);
                    Gizmos.DrawLine(pos, (Vector3)neighborPos);
                }
            }

            // 再画格子和图标
            for (int i = 0; i < cells.Length; i++)
            {
                var pos = (Vector3)transforms[i].Position;
                var cell = cells[i];

                switch (cell.CellType)
                {
                    case CellType.Plain:
                        Gizmos.color = cell.Owner switch
                        {
                            OwnerType.Player => new Color(0f, 1f, 0f, 0.3f),
                            OwnerType.Enemy => new Color(1f, 0f, 0f, 0.3f),
                            _ => new Color(0.5f, 0.5f, 0.5f, 0.2f)
                        };
                        Gizmos.DrawCube(pos, Vector3.one * 0.85f);
                        break;
                    case CellType.Castle:
                        Gizmos.color = cell.Owner == OwnerType.Player
                            ? new Color(0f, 1f, 1f, 0.6f)
                            : new Color(1f, 0f, 1f, 0.6f);
                        Gizmos.DrawCube(pos, Vector3.one * 1.1f);
                        Gizmos.DrawWireCube(pos, Vector3.one * 1.15f);
                        break;
                    case CellType.GoldMine:
                        Gizmos.color = cell.Owner switch
                        {
                            OwnerType.Player => new Color(0f, 1f, 0f, 0.6f),
                            OwnerType.Enemy => new Color(1f, 0f, 0f, 0.6f),
                            _ => new Color(1f, 0.92f, 0.016f, 0.7f)
                        };
                        Gizmos.DrawSphere(pos, 0.4f);
                        break;
                }
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

                Gizmos.color = unit.Owner == OwnerType.Player ? Color.blue : Color.red;
                Gizmos.DrawSphere(pos, 0.35f);
                Gizmos.DrawWireSphere(pos, 0.40f);

#if UNITY_EDITOR
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(
                    pos + Vector3.up * 0.6f,
                    $"HP:{unit.Health}/{unit.MaxHealth} ATK:{unit.Attack}");
#endif
            }

            units.Dispose();
            unitTransforms.Dispose();
        }
    }
}
