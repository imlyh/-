using Unity.Entities;
using Unity.Mathematics;

public enum BattalionOwner { Player, Enemy }
public enum BattalionState { Idle, Moving, Mining, Attacking }
public enum CommandType { Move, Mine, Attack }

// ---- Battalion ----

public struct BattalionData : IComponentData
{
    public BattalionOwner owner;
    public BattalionState state;
    public CommandType commandType;
    public float3 targetCell;
    public float moveSpeed;
    public float detectionRange;
    public float bobHeight;
    public float bobFrequency;
    public float bobPhase;
    public int pathIndex;
}

public struct BattalionPathPoint : IBufferElementData
{
    public float3 position;
}

// ---- Soldier ----

public enum SoldierActionState : byte { Idle, AttackingForward, AttackingBack }

public struct SoldierData : IComponentData
{
    public Entity battalionEntity;
    public float attackRange;
    public float attackCooldown;
    public float dashSpeed;
    public float dashHeight;
    public float cooldownRemaining;
    public SoldierActionState actionState;
    public float attackT;
    public float dashTotalTime;
    public float3 attackOrigin;
    public float3 attackTarget;
    public float3 formationOffset;
}

// ---- Player Command ----

public struct PlayerCommandData : IComponentData
{
    public Entity selectedBattalion;
    public float3 targetCell;
    public CommandType commandType;
    public bool pending;
}

// ---- Entity-GameObject link ----

public struct EntityLink : IComponentData
{
    public int goInstanceID;
}

// ---- Enemy AI ----

public enum EnemyAIPhase : byte { GoMine, Mining, ReturnCastle, AttackCastle }

public struct EnemyAIData : IComponentData
{
    public EnemyAIPhase phase;
    public float phaseTimer;
    public float3 mineTarget;
    public float3 castlePos;
    public float3 enemyCastlePos;
    public float miningDuration;
}
