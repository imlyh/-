using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

namespace ConquestGame
{
    /// <summary>
    /// 在 Game 视图中渲染六边形地图网格（组合Mesh + 顶点着色）
    /// 挂载到带 MeshFilter + MeshRenderer 的 GameObject 上
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HexGridRenderer : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private MeshFilter meshFilter;
        private bool hasBuilt;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            var mr = GetComponent<MeshRenderer>();

            // 使用 URP 的 Unlit shader（带顶点颜色）
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = Color.white;
            mr.material = material;
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;
            if (hasBuilt)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<HexCellData>(),
                ComponentType.ReadOnly<LocalTransform>());

            var cells = query.ToComponentDataArray<HexCellData>(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            if (cells.Length == 0)
            {
                cells.Dispose();
                transforms.Dispose();
                return;
            }

            BuildMesh(cells, transforms);

            // 绘制单位
            var unitQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var unitTransforms = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // 为单位创建子 GameObject 标记
            for (int i = 0; i < units.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = units[i].Owner == OwnerType.Player ? "PlayerUnit" : "EnemyUnit";
                go.transform.position = (Vector3)unitTransforms[i].Position;
                go.transform.localScale = Vector3.one * 0.3f;
                go.transform.SetParent(transform);
                var mr = go.GetComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mr.material.color = units[i].Owner == OwnerType.Player
                    ? Color.blue : Color.red;
            }

            units.Dispose();
            unitTransforms.Dispose();

            cells.Dispose();
            transforms.Dispose();
            hasBuilt = true;
        }

        private void BuildMesh(NativeArray<HexCellData> cells, NativeArray<LocalTransform> transforms)
        {
            const float r = 0.48f;
            int vertsPerHex = 7; // center + 6 corners
            int trisPerHex = 18; // 6 triangles × 3 indices

            var verts = new Vector3[cells.Length * vertsPerHex];
            var colors = new Color[cells.Length * vertsPerHex];
            var tris = new int[cells.Length * trisPerHex];

            for (int i = 0; i < cells.Length; i++)
            {
                var center = (Vector3)transforms[i].Position;
                int vi = i * vertsPerHex;
                int ti = i * trisPerHex;

                // center
                verts[vi] = center;
                colors[vi] = HexColor(cells[i]);

                // 6 corners
                for (int c = 0; c < 6; c++)
                {
                    float angle = 60f * c * Mathf.Deg2Rad;
                    verts[vi + 1 + c] = center + new Vector3(
                        Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                    colors[vi + 1 + c] = colors[vi]; // same as center
                }

                // 6 triangles: center + corner[c] + corner[c+1]
                for (int c = 0; c < 6; c++)
                {
                    tris[ti + c * 3] = vi;
                    tris[ti + c * 3 + 1] = vi + 1 + c;
                    tris[ti + c * 3 + 2] = vi + 1 + (c + 1) % 6;
                }
            }

            mesh = new Mesh { name = "HexGrid" };
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
        }

        private static Color HexColor(HexCellData cell)
        {
            switch (cell.CellType)
            {
                case CellType.Plain:
                    return cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.15f, 0.6f, 0.15f),
                        OwnerType.Enemy => new Color(0.6f, 0.15f, 0.15f),
                        _ => new Color(0.2f, 0.2f, 0.22f)
                    };
                case CellType.Castle:
                    return cell.Owner == OwnerType.Player
                        ? new Color(0f, 0.7f, 0.7f)
                        : new Color(0.7f, 0f, 0.7f);
                case CellType.GoldMine:
                    return cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.15f, 0.6f, 0.15f),
                        OwnerType.Enemy => new Color(0.6f, 0.15f, 0.15f),
                        _ => new Color(0.8f, 0.6f, 0f)
                    };
                default:
                    return Color.gray;
            }
        }

        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
            if (material != null)
                Destroy(material);
        }
    }
}
