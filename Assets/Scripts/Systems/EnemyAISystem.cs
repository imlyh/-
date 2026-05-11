using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 敌方AI自主决策系统
/// 每个敌方营独立决策：选择最近/最弱的玩家单位，执行攻击移动
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(BattalionInputSystem))]
public partial class EnemyAISystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var em = EntityManager;

        // Collect all player battalion positions and soldier counts
        var playerBattalionPositions = new Unity.Collections.NativeList<float3>(8, Unity.Collections.Allocator.Temp);
        var playerBattalionEntities = new Unity.Collections.NativeList<Entity>(8, Unity.Collections.Allocator.Temp);
        var playerBattalionStrength = new Unity.Collections.NativeList<int>(8, Unity.Collections.Allocator.Temp);

        foreach (var (bat, ltw, entity) in
            SystemAPI.Query<RefRO<BattalionData>, RefRO<LocalToWorld>>().WithEntityAccess())
        {
            if (bat.ValueRO.owner != BattalionOwner.Player) continue;
            if (bat.ValueRO.soldierCount <= 0) continue; // dead battalion, skip
            playerBattalionEntities.Add(entity);
            playerBattalionPositions.Add(ltw.ValueRO.Position);
            playerBattalionStrength.Add(bat.ValueRO.soldierCount);
        }

        // Find player castle
        float3 playerCastlePos = float3.zero;
        bool hasPlayerCastle = false;
        foreach (var (castle, ltw) in SystemAPI.Query<RefRO<CastleTag>, RefRO<LocalToWorld>>())
        {
            if (castle.ValueRO.owner == BattalionOwner.Player)
            {
                playerCastlePos = ltw.ValueRO.Position;
                hasPlayerCastle = true;
                break;
            }
        }

        // Process each enemy battalion's AI
        foreach (var (batRef, aiRef, ltw, entity) in
            SystemAPI.Query<RefRW<BattalionData>, RefRW<EnemyAIData>, RefRO<LocalToWorld>>().WithEntityAccess())
        {
            var bat = batRef.ValueRW;
            var ai = aiRef.ValueRW;

            // Countdown decision timer
            ai.decisionTimer -= dt;
            if (ai.decisionTimer > 0) continue;

            // Reset timer with 2-5s random interval
            ai.decisionTimer = ai.decisionCooldown + UnityEngine.Random.Range(-1f, 1.5f);
            ai.decisionTimer = math.max(1.5f, ai.decisionTimer);

            float3 myPos = ltw.ValueRO.Position;

            // Decision: target a player battalion or the castle?
            bool targetCastle = false;

            // Evaluate each player battalion for scoring
            Entity bestTarget = Entity.Null;
            float3 bestTargetPos = float3.zero;
            float bestScore = float.MaxValue;

            for (int i = 0; i < playerBattalionEntities.Length; i++)
            {
                float dist = math.distance(myPos, playerBattalionPositions[i]);
                float score = dist;

                // Prefer weak targets (fewer soldiers)
                score += playerBattalionStrength[i] * 2f;

                // Random factor to spread attacks
                score += UnityEngine.Random.Range(0f, 5f);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = playerBattalionEntities[i];
                    bestTargetPos = playerBattalionPositions[i];
                }
            }

            // Consider attacking castle directly
            if (hasPlayerCastle && UnityEngine.Random.value < ai.aggressiveness * 0.3f)
            {
                float castleDist = math.distance(myPos, playerCastlePos);
                if (castleDist < 30f)
                {
                    targetCastle = true;
                    bestTarget = Entity.Null;
                    bestTargetPos = playerCastlePos;
                }
            }

            // If we have a valid target, issue attack-move command
            if (bestTarget != Entity.Null || targetCastle)
            {
                ai.currentTargetEntity = targetCastle ? Entity.Null : bestTarget;

                // Add some offset so battalions approach from different directions
                float3 offset = new float3(
                    UnityEngine.Random.Range(-3f, 3f),
                    0,
                    UnityEngine.Random.Range(-3f, 3f)
                );
                float3 targetPos = bestTargetPos + offset;
                targetPos.y = 0;

                bat.targetCell = targetPos;
                bat.commandType = CommandType.Attack;
                bat.state = BattalionState.Moving;
                bat.targetEnemy = Entity.Null;
            }
            else
            {
                // No target found - return to home position
                bat.targetCell = ai.homePosition;
                bat.commandType = CommandType.Move;
                bat.state = BattalionState.Moving;
            }
        }

        playerBattalionPositions.Dispose();
        playerBattalionEntities.Dispose();
        playerBattalionStrength.Dispose();
    }
}
