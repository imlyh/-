using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

namespace ConquestGame
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HexGridRenderer : MonoBehaviour
    {
        private Mesh hexMesh;
        private Mesh outlineMesh;
        private Material hexMaterial;
        private Material outlineMaterial;
        private MeshFilter hexFilter;
        private MeshFilter outlineFilter;
        private bool hasBuilt;

        private void Awake()
        {
            // 子对象：填充层
            var go = new GameObject("HexFill");
            go.transform.SetParent(transform, false);
            hexFilter = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            hexMaterial = new Material(Shader.Find("Custom/VertexColorUnlit"));
            mr.material = hexMaterial;

            // 子对象：描边层
            var outlineGo = new GameObject("HexOutline");
            outlineGo.transform.SetParent(transform, false);
            outlineFilter = outlineGo.AddComponent<MeshFilter>();
            var omr = outlineGo.AddComponent<MeshRenderer>();
            outlineMaterial = new Material(Shader.Find("Custom/VertexColorUnlit"));
            omr.material = outlineMaterial;
        }

        private void Update()
        {
            if (!Application.isPlaying || hasBuilt)
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

            BuildHexMesh(cells, transforms);
            BuildOutlineMesh(cells, transforms);

            // 单位 Sphere
            var unitQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var unitTransforms = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < units.Length; i++)
            {
                var unitGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                unitGo.name = units[i].Owner == OwnerType.Player ? "PlayerUnit" : "EnemyUnit";
                unitGo.transform.position = (Vector3)unitTransforms[i].Position;
                unitGo.transform.localScale = Vector3.one * 0.35f;
                unitGo.transform.SetParent(transform);
                var umr = unitGo.GetComponent<MeshRenderer>();
                umr.material = new Material(Shader.Find("Custom/VertexColorUnlit"));
                umr.material.color = units[i].Owner == OwnerType.Player
                    ? Color.blue : Color.red;
            }

            units.Dispose();
            unitTransforms.Dispose();
            cells.Dispose();
            transforms.Dispose();
            hasBuilt = true;
        }

        private void BuildHexMesh(NativeArray<HexCellData> cells, NativeArray<LocalTransform> transforms)
        {
            const float innerR = 0.44f;
            int vph = 7, tph = 18;
            var verts = new Vector3[cells.Length * vph];
            var colors = new Color[cells.Length * vph];
            var tris = new int[cells.Length * tph];

            for (int i = 0; i < cells.Length; i++)
            {
                var center = (Vector3)transforms[i].Position;
                int vi = i * vph, ti = i * tph;
                var c = HexFillColor(cells[i]);

                verts[vi] = center;
                colors[vi] = c;
                for (int j = 0; j < 6; j++)
                {
                    float a = 60f * j * Mathf.Deg2Rad;
                    verts[vi + 1 + j] = center + new Vector3(Mathf.Cos(a) * innerR, 0f, Mathf.Sin(a) * innerR);
                    colors[vi + 1 + j] = c;
                }
                for (int j = 0; j < 6; j++)
                {
                    tris[ti + j * 3] = vi;
                    tris[ti + j * 3 + 1] = vi + 1 + j;
                    tris[ti + j * 3 + 2] = vi + 1 + (j + 1) % 6;
                }
            }

            hexMesh = new Mesh { name = "HexFill" };
            hexMesh.vertices = verts;
            hexMesh.colors = colors;
            hexMesh.triangles = tris;
            hexMesh.RecalculateNormals();
            hexMesh.RecalculateBounds();
            hexFilter.mesh = hexMesh;
        }

        private void BuildOutlineMesh(NativeArray<HexCellData> cells, NativeArray<LocalTransform> transforms)
        {
            const float outerR = 0.50f;
            const float innerR = 0.44f;
            // 每格一个六边形环 = 12 verts (outer 6 + inner 6), 36 indices (6 quads × 2 tris × 3)
            int vph = 12, tph = 36;
            var verts = new Vector3[cells.Length * vph];
            var colors = new Color[cells.Length * vph];
            var tris = new int[cells.Length * tph];

            for (int i = 0; i < cells.Length; i++)
            {
                var center = (Vector3)transforms[i].Position;
                int vi = i * vph, ti = i * tph;
                var oc = HexOutlineColor(cells[i]);

                // 0-5: outer ring, 6-11: inner ring
                for (int j = 0; j < 6; j++)
                {
                    float a = 60f * j * Mathf.Deg2Rad;
                    verts[vi + j] = center + new Vector3(Mathf.Cos(a) * outerR, 0f, Mathf.Sin(a) * outerR);
                    verts[vi + 6 + j] = center + new Vector3(Mathf.Cos(a) * innerR, 0f, Mathf.Sin(a) * innerR);
                    colors[vi + j] = oc;
                    colors[vi + 6 + j] = oc;
                }

                // 6 quads → 12 triangles, each quad: outer[j], outer[j+1], inner[j], inner[j+1]
                for (int j = 0; j < 6; j++)
                {
                    int n = (j + 1) % 6;
                    int t = j * 2;
                    // tri 1: outer[j], outer[n], inner[j]
                    tris[ti + t * 3] = vi + j;
                    tris[ti + t * 3 + 1] = vi + n;
                    tris[ti + t * 3 + 2] = vi + 6 + j;
                    // tri 2: inner[j], outer[n], inner[n]
                    tris[ti + (t + 1) * 3] = vi + 6 + j;
                    tris[ti + (t + 1) * 3 + 1] = vi + n;
                    tris[ti + (t + 1) * 3 + 2] = vi + 6 + n;
                }
            }

            outlineMesh = new Mesh { name = "HexOutline" };
            outlineMesh.vertices = verts;
            outlineMesh.colors = colors;
            outlineMesh.triangles = tris;
            outlineMesh.RecalculateNormals();
            outlineMesh.RecalculateBounds();
            outlineFilter.mesh = outlineMesh;
        }

        private static Color HexFillColor(HexCellData cell)
        {
            switch (cell.CellType)
            {
                case CellType.Plain:
                    return cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.2f, 0.75f, 0.25f),
                        OwnerType.Enemy => new Color(0.75f, 0.2f, 0.2f),
                        _ => new Color(0.28f, 0.28f, 0.30f)
                    };
                case CellType.Castle:
                    return cell.Owner == OwnerType.Player
                        ? new Color(0f, 0.8f, 0.8f)
                        : new Color(0.8f, 0f, 0.8f);
                case CellType.GoldMine:
                    return cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.3f, 0.7f, 0.2f),
                        OwnerType.Enemy => new Color(0.7f, 0.2f, 0.2f),
                        _ => new Color(0.9f, 0.7f, 0.05f)
                    };
                default:
                    return Color.gray;
            }
        }

        private static Color HexOutlineColor(HexCellData cell)
        {
            // 描边颜色更深，形成边框效果
            switch (cell.CellType)
            {
                case CellType.Plain:
                    return new Color(0.1f, 0.1f, 0.12f);
                case CellType.Castle:
                    return Color.white;
                case CellType.GoldMine:
                    return new Color(0.5f, 0.4f, 0f);
                default:
                    return Color.black;
            }
        }

        private void OnDestroy()
        {
            if (hexMesh != null) Destroy(hexMesh);
            if (outlineMesh != null) Destroy(outlineMesh);
            if (hexMaterial != null) Destroy(hexMaterial);
            if (outlineMaterial != null) Destroy(outlineMaterial);
        }
    }
}
