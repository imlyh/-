
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
        // Disabled — Behavior Designer takes over
        Enabled = false;
    }
}
