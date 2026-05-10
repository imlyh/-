using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(BattalionLogicSystem))]
public partial class BattalionInputSystem : SystemBase
{
    private Camera cam;
    protected override void OnCreate() { RequireForUpdate<PlayerCommandData>(); }

    protected override void OnUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!SystemAPI.TryGetSingletonRW<PlayerCommandData>(out var cmd)) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 500f))
            {
                var e = ResolveEntity(hit.collider.gameObject);
                cmd.ValueRW.selectedBattalion = e != Entity.Null ? EntityManager.GetComponentData<Parent>(e).Value : Entity.Null;
            }
            else cmd.ValueRW.selectedBattalion = Entity.Null;
        }

        if (mouse.rightButton.wasPressedThisFrame && cmd.ValueRW.selectedBattalion != Entity.Null)
        {
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float dist)) return;
            var pt = ray.GetPoint(dist);
            var cell = new float3(math.clamp(math.round(pt.x),0,29), 0, math.clamp(math.round(pt.z),0,19));
            var type = CommandType.Move;
            var hits = Physics.OverlapSphere(new Vector3(cell.x,0,cell.z), 0.7f);
            bool isMine=false, isEnemy=false;
            var batData = EntityManager.GetComponentData<BattalionData>(cmd.ValueRW.selectedBattalion);
            foreach (var h in hits)
            {
                if (h.name.StartsWith("GoldMine")) isMine=true;
                else if (CheckEnemyGO(h.gameObject, batData.owner)) isEnemy=true;
            }
            if (isMine) type=CommandType.Mine;
            else if (isEnemy) type=CommandType.Attack;
            cmd.ValueRW.targetCell=cell; cmd.ValueRW.commandType=type; cmd.ValueRW.pending=true;
        }
    }

    Entity ResolveEntity(GameObject go)
    {
        foreach (var (link, entity) in SystemAPI.Query<RefRO<EntityLink>>().WithEntityAccess())
            if (link.ValueRO.goInstanceID == go.GetInstanceID()) return entity;
        return Entity.Null;
    }

    bool CheckEnemyGO(GameObject go, BattalionOwner owner)
    {
        foreach (var (link, parent) in SystemAPI.Query<RefRO<EntityLink>, RefRO<Parent>>())
            if (link.ValueRO.goInstanceID == go.GetInstanceID())
                return EntityManager.GetComponentData<BattalionData>(parent.ValueRO.Value).owner != owner;
        return false;
    }
}
