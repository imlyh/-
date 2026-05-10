using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(BattalionLogicSystem))]
public partial class EnemyAISystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (aiRef, batRef, txRef, entity) in
            SystemAPI.Query<RefRW<EnemyAIData>, RefRW<BattalionData>, RefRO<LocalTransform>>()
            .WithEntityAccess())
        {
            var ai = aiRef.ValueRW;
            var bat = batRef.ValueRW;
            var pos = txRef.ValueRO.Position;
            ai.phaseTimer += dt;

            switch (ai.phase)
            {
                case EnemyAIPhase.GoMine:
                    if (bat.state == BattalionState.Idle)
                    {
                        bat.targetCell = ai.mineTarget;
                        bat.commandType = CommandType.Mine;
                        bat.state = BattalionState.Moving;
                    }
                    if (bat.state == BattalionState.Mining)
                    { ai.phase = EnemyAIPhase.Mining; ai.phaseTimer = 0; }
                    if (bat.state == BattalionState.Idle && !MineInRange(pos, bat.detectionRange))
                    { bat.targetCell = ai.mineTarget; bat.commandType = CommandType.Mine; bat.state = BattalionState.Moving; }
                    break;

                case EnemyAIPhase.Mining:
                    if (!MineInRange(pos, bat.detectionRange))
                    { bat.targetCell = ai.mineTarget; bat.commandType = CommandType.Mine; bat.state = BattalionState.Moving; ai.phase = EnemyAIPhase.GoMine; }
                    else if (ai.phaseTimer >= ai.miningDuration)
                    { bat.targetCell = ai.castlePos; bat.commandType = CommandType.Move; bat.state = BattalionState.Moving; ai.phase = EnemyAIPhase.ReturnCastle; ai.phaseTimer = 0; }
                    break;

                case EnemyAIPhase.ReturnCastle:
                    if (bat.state == BattalionState.Idle)
                    { bat.targetCell = ai.enemyCastlePos; bat.commandType = CommandType.Attack; bat.state = BattalionState.Moving; ai.phase = EnemyAIPhase.AttackCastle; ai.phaseTimer = 0; }
                    break;

                case EnemyAIPhase.AttackCastle:
                    if (ai.phaseTimer > 15f)
                    { bat.targetCell = ai.mineTarget; bat.commandType = CommandType.Mine; bat.state = BattalionState.Moving; ai.phase = EnemyAIPhase.GoMine; ai.phaseTimer = 0; }
                    break;
            }

            aiRef.ValueRW = ai;
            batRef.ValueRW = bat;
        }
    }

    bool MineInRange(float3 pos, float range)
    {
        foreach (var h in Physics.OverlapSphere(new Vector3(pos.x, 0, pos.z), range))
            if (h.name.StartsWith("GoldMine")) return true;
        return false;
    }
}
