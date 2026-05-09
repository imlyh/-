using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

namespace ConquestGame
{
    /// <summary>
    /// 调试用：在 Scene View 中绘制六边形网格和单位位置
    /// 挂载到场景中的任意 GameObject 上即可
    /// </summary>
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

            // 绘制 HexCell
            var cellQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<HexCellData>(),
                ComponentType.ReadOnly<LocalTransform>());

            var cells = cellQuery.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var transforms = cellQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < cells.Length; i++)
            {
                var pos = transforms[i].Position;
                var cell = cells[i];

                switch (cell.CellType)
                {
                    case CellType.Plain:
                        Gizmos.color = cell.Owner switch
                        {
                            OwnerType.Player => Color.green,
                            OwnerType.Enemy => Color.red,
                            _ => new Color(0.3f, 0.3f, 0.3f, 0.5f)
                        };
                        Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
                        break;
                    case CellType.Castle:
                        Gizmos.color = cell.Owner == OwnerType.Player
                            ? Color.cyan : Color.magenta;
                        Gizmos.DrawCube(pos, Vector3.one * 1.2f);
                        break;
                    case CellType.GoldMine:
                        Gizmos.color = cell.Owner switch
                        {
                            OwnerType.Player => Color.green,
                            OwnerType.Enemy => Color.red,
                            _ => Color.yellow
                        };
                        Gizmos.DrawWireSphere(pos, 0.5f);
                        break;
                }
            }

            cells.Dispose();
            transforms.Dispose();

            // 绘制 Unit
            var unitQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());

            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var unitTransforms = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < units.Length; i++)
            {
                var pos = unitTransforms[i].Position;
                var unit = units[i];

                Gizmos.color = unit.Owner == OwnerType.Player ? Color.blue : Color.red;
                Gizmos.DrawSphere(pos, 0.3f);

                // 画血条
#if UNITY_EDITOR
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(
                    (Vector3)pos + Vector3.up * 0.5f,
                    $"HP:{unit.Health}/{unit.MaxHealth} ATK:{unit.Attack}");
#endif
            }

            units.Dispose();
            unitTransforms.Dispose();
        }
    }
}
