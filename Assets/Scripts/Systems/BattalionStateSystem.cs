
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 营级状态机：Idle↔Moving↔InCombat
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BattalionInputSystem))]
[UpdateBefore(typeof(BoidTargetSystem))]
public partial class BattalionStateSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (batRef, txRef, entity) in
            SystemAPI.Query<RefRW<BattalionData>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            var bat = batRef.ValueRW;
            float3 flat = txRef.ValueRO.Position; flat.y = 0;

            // Process player command → Moving
            if (bat.state == BattalionState.Idle && bat.commandType != CommandType.Move)
            {
                // Transition decided by CommandMove calls below
            }

            // Idle: auto-detect enemies
            if (bat.state == BattalionState.Idle && HasEnemyInRange(flat, bat.owner, bat.detectionRange, out Entity enemy))
            {
                bat.state = BattalionState.InCombat;
                bat.targetEnemy = enemy;
                bat.targetPosition = GetEntityPosition(enemy);
            }

            // Moving: check if reached detection range of a target
            if (bat.state == BattalionState.Moving)
            {
                float3 toTarget = bat.targetCell - flat;
                if (math.lengthsq(toTarget) < bat.detectionRange * bat.detectionRange)
                {
                    // If the target cell is near an enemy → InCombat
                    if (HasEnemyAt(bat.targetCell, bat.owner))
                    {
                        bat.state = BattalionState.InCombat;
                        bat.targetPosition = bat.targetCell;
                    }
                    else bat.state = BattalionState.Idle;
                }
            }

            // InCombat: check if target still alive
            if (bat.state == BattalionState.InCombat && bat.targetEnemy != Entity.Null)
            {
                if (!EntityManager.Exists(bat.targetEnemy) ||
                    !EntityManager.HasComponent<HealthData>(bat.targetEnemy) ||
                    EntityManager.GetComponentData<HealthData>(bat.targetEnemy).currentHP <= 0)
                {
                    bat.targetEnemy = Entity.Null;
                    bat.state = BattalionState.Idle;
                }
                else bat.targetPosition = GetEntityPosition(bat.targetEnemy);
            }

            batRef.ValueRW = bat;
        }
    }

    bool HasEnemyInRange(float3 pos, BattalionOwner owner, float range, out Entity enemyEntity)
    {
        var hits = Physics.OverlapSphere(new Vector3(pos.x, 0, pos.z), range);
        foreach (var h in hits)
        {
            var e = HasHealth(h.gameObject.GetInstanceID());
            if (e != Entity.Null)
            {
                var hpOwner = GetOwnerFromGO(h);
                if (hpOwner != owner) { enemyEntity = e; return true; }
            }
        }
        enemyEntity = Entity.Null; return false;
    }

    bool HasEnemyAt(float3 cell, BattalionOwner owner)
    {
        var hits = Physics.OverlapSphere(new Vector3(cell.x, 0, cell.z), 0.7f);
        foreach (var h in hits)
            if (GetOwnerFromGO(h) != owner && (h.name.Contains("Soldier") || h.name.Contains("Castle")))
                return true;
        return false;
    }

    Entity HasHealth(int goId)
    {
        foreach (var (link, hp) in SystemAPI.Query<RefRO<EntityLink>, RefRO<HealthData>>())
            if (link.ValueRO.goInstanceID == goId) return link.ValueRO.goInstanceID == goId ? Entity.Null : Entity.Null;
        return Entity.Null;
    }

    BattalionOwner GetOwnerFromGO(UnityEngine.Collider h)
    {
        if (h.name.Contains("Player")) return BattalionOwner.Player;
        if (h.name.Contains("Enemy")) return BattalionOwner.Enemy;
        int id = h.gameObject.GetInstanceID();
        foreach (var (link, sd) in SystemAPI.Query<RefRO<EntityLink>, RefRO<SoldierData>>())
            if (link.ValueRO.goInstanceID == id)
                return SystemAPI.GetComponent<BattalionData>(sd.ValueRO.battalionEntity).owner;
        return BattalionOwner.Player;
    }

    float3 GetEntityPosition(Entity e)
    {
        if (!EntityManager.Exists(e) || !EntityManager.HasComponent<LocalToWorld>(e))
            return float3.zero;
        return EntityManager.GetComponentData<LocalToWorld>(e).Position;
    }
}
