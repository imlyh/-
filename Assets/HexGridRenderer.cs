using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

namespace ConquestGame
{
    public class HexGridRenderer : MonoBehaviour
    {
        private Mesh hexMesh;
        private Material hexMaterial;
        private bool hasBuilt;

        // 每帧单元状态
        private int[] unitEntityIds;
        private Vector3[] unitPositions;
        private bool[] unitIsPlayer;
        private int unitCount;

        private void Awake()
        {
            var shader = Shader.Find("Custom/VertexColorUnlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
            }
            hexMaterial = new Material(shader);
            hexMaterial.color = Color.white;
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            var em = world.EntityManager;

            if (!hasBuilt)
            {
                var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<HexCellData>(),
                    ComponentType.ReadOnly<LocalTransform>());

                var cells = query.ToComponentDataArray<HexCellData>(Allocator.Temp);
                var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                if (cells.Length > 0)
                {
                    BuildHexMesh(cells, transforms);
                    hasBuilt = true;
                }

                cells.Dispose();
                transforms.Dispose();
            }

            if (hasBuilt && hexMesh != null)
            {
                // 每帧手动绘制 hex mesh（无相机裁剪优化，直接画）
                Graphics.DrawMesh(hexMesh, Vector3.zero, Quaternion.identity, hexMaterial, 0);
            }

            // 手动绘制单位
            var unitQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitData>(),
                ComponentType.ReadOnly<LocalTransform>());
            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var ut = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            // Fallback: create a small quad or use primitive
            if (sphereMesh == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereMesh = go.GetComponent<MeshFilter>().sharedMesh;
                Destroy(go);
            }

            var matBlue = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            matBlue.color = Color.blue;
            var matRed = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            matRed.color = Color.red;

            for (int i = 0; i < units.Length; i++)
            {
                var pos = (Vector3)ut[i].Position;
                var mat = units[i].Owner == OwnerType.Player ? matBlue : matRed;
                var matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.35f);
                Graphics.DrawMesh(sphereMesh, matrix, mat, 0);
            }

            units.Dispose();
            ut.Dispose();
        }

        private void BuildHexMesh(NativeArray<HexCellData> cells, NativeArray<LocalTransform> transforms)
        {
            const float innerR = 0.44f;
            const float outerR = 0.50f;

            // 每个格子：填充 7 verts + 18 indices，描边环 12 verts + 36 indices
            int vpf = 7, tpf = 18; // fill: per hex
            int vpo = 12, tpo = 36; // outline: per hex
            int n = cells.Length;
            int totalVerts = n * (vpf + vpo);
            int totalTris = n * (tpf + tpo);

            var verts = new Vector3[totalVerts];
            var colors = new Color[totalVerts];
            var tris = new int[totalTris];

            for (int i = 0; i < n; i++)
            {
                var center = (Vector3)transforms[i].Position;
                Color fillC = HexFillColor(cells[i]);
                Color outlineC = HexOutlineColor(cells[i]);

                int vBase = i * (vpf + vpo);
                int tBase = i * (tpf + tpo);

                // --- 填充层 ---
                verts[vBase] = center;
                colors[vBase] = fillC;
                for (int j = 0; j < 6; j++)
                {
                    float a = 60f * j * Mathf.Deg2Rad;
                    verts[vBase + 1 + j] = center + new Vector3(Mathf.Cos(a) * innerR, 0f, Mathf.Sin(a) * innerR);
                    colors[vBase + 1 + j] = fillC;
                }
                for (int j = 0; j < 6; j++)
                {
                    tris[tBase + j * 3] = vBase;
                    tris[tBase + j * 3 + 1] = vBase + 1 + j;
                    tris[tBase + j * 3 + 2] = vBase + 1 + (j + 1) % 6;
                }

                // --- 描边层（环）---
                int vo = vBase + vpf;
                int to = tBase + tpf;
                for (int j = 0; j < 6; j++)
                {
                    float a = 60f * j * Mathf.Deg2Rad;
                    verts[vo + j] = center + new Vector3(Mathf.Cos(a) * outerR, 0f, Mathf.Sin(a) * outerR);
                    verts[vo + 6 + j] = center + new Vector3(Mathf.Cos(a) * innerR, 0f, Mathf.Sin(a) * innerR);
                    colors[vo + j] = outlineC;
                    colors[vo + 6 + j] = outlineC;
                }
                for (int j = 0; j < 6; j++)
                {
                    int nj = (j + 1) % 6;
                    int t = j * 6; // 2 triangles per quad × 3 indices
                    tris[to + t] = vo + j;
                    tris[to + t + 1] = vo + nj;
                    tris[to + t + 2] = vo + 6 + j;
                    tris[to + t + 3] = vo + 6 + j;
                    tris[to + t + 4] = vo + nj;
                    tris[to + t + 5] = vo + 6 + nj;
                }
            }

            hexMesh = new Mesh { name = "HexGrid" };
            hexMesh.vertices = verts;
            hexMesh.colors = colors;
            hexMesh.triangles = tris;
            hexMesh.RecalculateNormals();
            hexMesh.RecalculateBounds();
            Debug.Log($"[HexGrid] Mesh built: {totalVerts} verts, {totalTris} indices, bounds={hexMesh.bounds}");
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
                        _ => new Color(0.3f, 0.3f, 0.32f)
                    };
                case CellType.Castle:
                    return cell.Owner == OwnerType.Player
                        ? new Color(0f, 0.85f, 0.85f)
                        : new Color(0.85f, 0f, 0.85f);
                case CellType.GoldMine:
                    return cell.Owner switch
                    {
                        OwnerType.Player => new Color(0.3f, 0.7f, 0.2f),
                        OwnerType.Enemy => new Color(0.7f, 0.2f, 0.2f),
                        _ => new Color(0.95f, 0.75f, 0.05f)
                    };
                default:
                    return Color.gray;
            }
        }

        private static Color HexOutlineColor(HexCellData cell)
        {
            return cell.CellType switch
            {
                CellType.Plain => new Color(0.05f, 0.05f, 0.07f),
                CellType.Castle => Color.white,
                CellType.GoldMine => new Color(0.5f, 0.4f, 0f),
                _ => Color.black
            };
        }

        private void OnDestroy()
        {
            if (hexMesh != null) Destroy(hexMesh);
            if (hexMaterial != null) Destroy(hexMaterial);
        }
    }
}
