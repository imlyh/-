using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

namespace ConquestGame
{
    [ExecuteAlways]
    public class HexGridDebugger : MonoBehaviour
    {
        private Mesh hexMesh;

        private void Awake()
        {
            hexMesh = CreateHexMesh(0.5f);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var entityManager = world.EntityManager;

            // === HexCell ===
            var cellQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<HexCellData>(),
                ComponentType.ReadOnly<LocalTransform>());

            var cells = cellQuery.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var transforms = cellQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < cells.Length; i++)
            {
                var pos = transforms[i].Position;
                var cell = cells[i];

                Color color = cell.CellType switch
                {
                    CellType.Plain => cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.1f, 0.7f, 0.1f),
                        OwnerType.Enemy => new Color(0.7f, 0.1f, 0.1f),
                        _ => new Color(0.25f, 0.25f, 0.25f)
                    },
                    CellType.Castle => cell.Owner == OwnerType.Player
                        ? new Color(0f, 0.8f, 0.8f)
                        : new Color(0.8f, 0f, 0.8f),
                    CellType.GoldMine => cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.1f, 0.7f, 0.1f),
                        OwnerType.Enemy => new Color(0.7f, 0.1f, 0.1f),
                        _ => new Color(0.9f, 0.7f, 0f)
                    },
                    _ => Color.gray
                };

                Gizmos.color = color;
                Gizmos.DrawMesh(hexMesh, pos, Quaternion.identity, Vector3.one);
            }

            cells.Dispose();
            transforms.Dispose();

            // === Unit ===
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
                    ? Color.blue : Color.red;
                Gizmos.DrawSphere(pos, 0.25f);
            }

            units.Dispose();
            unitTransforms.Dispose();
        }

        private static Mesh CreateHexMesh(float radius)
        {
            var mesh = new Mesh();
            var verts = new Vector3[7];
            for (int i = 0; i < 6; i++)
            {
                float angle = 60f * i * Mathf.Deg2Rad;
                verts[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            verts[6] = Vector3.zero;

            var tris = new int[18]; // 6 triangles × 3
            for (int i = 0; i < 6; i++)
            {
                tris[i * 3] = 6;
                tris[i * 3 + 1] = i;
                tris[i * 3 + 2] = (i + 1) % 6;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
