using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(BattalionLogicSystem))]
public partial struct BattalionInputSystem : ISystem
{
    private Camera cam;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCommandData>();
        cam = Camera.main;
    }

    public void OnUpdate(ref SystemState state)
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        var cmd = SystemAPI.GetSingletonRW<PlayerCommandData>();

        // Left click: select battalion
        if (mouse.leftButton.wasPressedThisFrame)
        {
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 500f))
            {
                var link = hit.collider != null ? ResolveEntity(hit.collider.gameObject, ref state) : Entity.Null;
                if (link != Entity.Null)
                {
                    var parent = state.EntityManager.GetComponentData<Parent>(link).Value;
                    cmd.ValueRW.selectedBattalion = parent;
                }
                else
                {
                    cmd.ValueRW.selectedBattalion = Entity.Null;
                }
            }
            else cmd.ValueRW.selectedBattalion = Entity.Null;
        }

        // Right click: issue command
        if (mouse.rightButton.wasPressedThisFrame && cmd.ValueRW.selectedBattalion != Entity.Null)
        {
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
            {
                var hit = ray.GetPoint(dist);
                var cell = new float3(
                    math.clamp(math.round(hit.x), 0, 29), 0,
                    math.clamp(math.round(hit.z), 0, 19));

                // Determine command type
                CommandType type = CommandType.Move;
                var hits = Physics.OverlapSphere(new Vector3(cell.x, 0, cell.z), 0.7f);
                bool isMine = false, isEnemy = false;
                foreach (var h in hits)
                {
                    if (h.name.StartsWith("GoldMine")) isMine = true;
                    var el = h.GetComponentInParent<GameObject>();
                }
                // Check for enemy at cell
                var batData = SystemAPI.GetComponent<BattalionData>(cmd.ValueRW.selectedBattalion);
                foreach (var h in hits)
                {
                    if (h.name.StartsWith("GoldMine")) isMine = true;
                    else isEnemy = CheckEnemyGO(h.gameObject, batData.owner, ref state);
                }

                if (isMine) type = CommandType.Mine;
                else if (isEnemy) type = CommandType.Attack;

                cmd.ValueRW.targetCell = cell;
                cmd.ValueRW.commandType = type;
                cmd.ValueRW.pending = true;
            }
        }
    }

    Entity ResolveEntity(GameObject go, ref SystemState state)
    {
        foreach (var (link, entity) in SystemAPI.Query<RefRO<EntityLink>>().WithEntityAccess())
            if (link.ValueRO.goInstanceID == go.GetInstanceID())
                return entity;
        return Entity.Null;
    }

    bool CheckEnemyGO(GameObject go, BattalionOwner owner, ref SystemState state)
    {
        foreach (var (link, parent) in SystemAPI.Query<RefRO<EntityLink>, RefRO<Parent>>())
        {
            if (link.ValueRO.goInstanceID == go.GetInstanceID())
            {
                var bat = SystemAPI.GetComponent<BattalionData>(parent.ValueRO.Value);
                return bat.owner != owner;
            }
        }
        return false;
    }
}
