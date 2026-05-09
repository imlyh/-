using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

/// ============================================================================
/// UnitController — 鼠标选中单位 + 点击格子下达移动命令
/// ============================================================================
///
/// 【操作方式】
///   左键点击己方单位 → 选中（高亮显示）
///   左键点击空格子 → 选中单位移动过去
///   右键拖拽 → 摄像机平移（由 CameraController 处理）
///
/// 【实现原理】
///   用 Plane.Raycast 计算鼠标在 Y=0 平面的世界坐标，
///   就近匹配到网格格子，再查找该格子上的单位或下达移动命令。
/// ============================================================================
namespace ConquestGame
{
    public class UnitController : MonoBehaviour
    {
        [Header("操作")]
        [SerializeField] private float clickThreshold = 0.5f; // 点击移动阈值（秒）

        private Entity selectedEntity = Entity.Null;
        private GameObject selectionIndicator;
        private float clickTime;
        private Vector2 clickStartPos;

        private void Start()
        {
            // 绿色半透明方块挂在选中单位上方
            selectionIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            selectionIndicator.name = "SelectionIndicator";
            selectionIndicator.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            selectionIndicator.SetActive(false);
            Destroy(selectionIndicator.GetComponent<Collider>());
            var mr = selectionIndicator.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Unlit/Color")
                                 ?? Shader.Find("Sprites/Default"));
            mat.SetColor("_BaseColor", new Color(0f, 1f, 0f, 0.6f));
            mat.SetColor("_Color", new Color(0f, 1f, 0f, 0.6f));
            mr.sharedMaterial = mat;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // 左键按下记录起始位置
            if (mouse.leftButton.wasPressedThisFrame)
            {
                clickStartPos = mouse.position.value;
                clickTime = Time.time;
            }

            // 左键松开：判断是点击还是拖拽
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                float dragDist = Vector2.Distance(mouse.position.value, clickStartPos);
                float dragTime = Time.time - clickTime;

                // 拖拽太远或太久 = 摄像机操作，不处理
                if (dragDist > 10f || dragTime > 0.5f)
                    return;

                HandleClick(mouse.position.value);
            }
        }

        /// <summary>
        /// 处理点击：射线 → 平面 y=0 → 匹配格子 → 选单位 or 移动
        /// </summary>
        private void HandleClick(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;

            // 计算鼠标在 Y=0 平面上的世界坐标
            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float dist)) return;

            Vector3 hitPoint = ray.GetPoint(dist);

            // 找最近的格子（四舍五入到整数坐标）
            int col = Mathf.RoundToInt(hitPoint.x);
            int row = Mathf.RoundToInt(hitPoint.z);
            var targetCoord = new HexCoordinates(col, row);

            // 检查点击位置是否够近（防止点到地图外）
            float dx = hitPoint.x - col;
            float dz = hitPoint.z - row;
            if (Mathf.Abs(dx) > 0.5f || Mathf.Abs(dz) > 0.5f)
                return;

            // 查找该格子上是否有己方单位
            Entity playerUnitHere = Entity.Null;
            var unitQuery = em.CreateEntityQuery(ComponentType.ReadOnly<UnitData>());
            var units = unitQuery.ToComponentDataArray<UnitData>(Allocator.Temp);
            var entities = unitQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].CurrentPosition == targetCoord
                    && units[i].Owner == OwnerType.Player
                    && units[i].Health > 0)
                {
                    playerUnitHere = entities[i];
                    break;
                }
            }

            if (playerUnitHere != Entity.Null)
            {
                // 点击己方单位 → 选中
                selectedEntity = playerUnitHere;
                Debug.Log($"[UnitController] 选中单位 {(selectedEntity != Entity.Null ? selectedEntity.ToString() : "null")}");
            }
            else if (selectedEntity != Entity.Null && em.Exists(selectedEntity))
            {
                // 已有选中单位 + 点击空位 → 下达移动命令
                var unitData = em.GetComponentData<UnitData>(selectedEntity);
                if (unitData.Owner == OwnerType.Player)
                {
                    em.SetComponentData(selectedEntity, new UnitData
                    {
                        Attack = unitData.Attack,
                        Defense = unitData.Defense,
                        Health = unitData.Health,
                        MaxHealth = unitData.MaxHealth,
                        MoveSpeed = unitData.MoveSpeed,
                        Owner = unitData.Owner,
                        State = UnitState.Moving,
                        CurrentPosition = unitData.CurrentPosition,
                        TargetPosition = targetCoord,
                        MoveTimer = 0f,
                        CombatTimer = unitData.CombatTimer
                    });
                    Debug.Log($"[UnitController] 下达移动命令 → {targetCoord}");
                }
            }

            units.Dispose();
            entities.Dispose();
        }

        private void LateUpdate()
        {
            // 更新选中指示器位置
            if (selectedEntity == Entity.Null) { selectionIndicator.SetActive(false); return; }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { selectionIndicator.SetActive(false); return; }
            var em = world.EntityManager;

            if (!em.Exists(selectedEntity))
            {
                selectedEntity = Entity.Null;
                selectionIndicator.SetActive(false);
                return;
            }

            var lt = em.GetComponentData<LocalTransform>(selectedEntity);
            selectionIndicator.SetActive(true);
            selectionIndicator.transform.position = (Vector3)lt.Position + Vector3.up * 0.5f;
        }
    }
}
