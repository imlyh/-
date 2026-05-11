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
            if (Physics.SphereCast(ray, 0.4f, out var hit, 500f))
            {
                // Castle click → open shop
                if (hit.collider.gameObject.name == "PlayerCastle")
                {
                    cmd.ValueRW.selectedBattalion = Entity.Null;
                    ShopUI shop = Object.FindObjectOfType<ShopUI>();
                    if (shop != null) shop.SetOpen(true);
                    return;
                }

                var e = ResolveEntity(hit.collider.gameObject);
                if (e != Entity.Null && EntityManager.HasComponent<SoldierData>(e))
                {
                    var batEntity = EntityManager.GetComponentData<SoldierData>(e).battalionEntity;
                    if (EntityManager.HasComponent<BattalionData>(batEntity) &&
                        EntityManager.GetComponentData<BattalionData>(batEntity).owner == BattalionOwner.Player)
                        cmd.ValueRW.selectedBattalion = batEntity;
                    else cmd.ValueRW.selectedBattalion = Entity.Null;
                }
                else cmd.ValueRW.selectedBattalion = Entity.Null;
            }
            else { Debug.Log("[INPUT] Ray missed"); cmd.ValueRW.selectedBattalion = Entity.Null; }
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
                else if (h.name.Contains("Castle") && IsEnemyCastle(h.gameObject, batData.owner)) isEnemy=true;
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

    bool IsEnemyCastle(GameObject go, BattalionOwner myOwner)
    {
        if (go.name.Contains("Player") && myOwner == BattalionOwner.Enemy) return true;
        if (go.name.Contains("Enemy") && myOwner == BattalionOwner.Player) return true;
        return false;
    }

    bool CheckEnemyGO(GameObject go, BattalionOwner owner)
    {
        foreach (var (link, sd) in SystemAPI.Query<RefRO<EntityLink>, RefRO<SoldierData>>())
            if (link.ValueRO.goInstanceID == go.GetInstanceID()
                && EntityManager.HasComponent<BattalionData>(sd.ValueRO.battalionEntity))
                return EntityManager.GetComponentData<BattalionData>(sd.ValueRO.battalionEntity).owner != owner;
        return false;
    }
}
