using Unity.Entities;
using Unity.Mathematics;

public enum BattalionOwner { Player, Enemy }
public enum BattalionState { Idle, Moving, InCombat }
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
    public int pathIndex;
    public float3 targetPosition;
    public Entity targetEnemy;
}
public struct BattalionPathPoint : IBufferElementData { public float3 position; }

// ---- Soldier (Boids + Combat) ----
public struct SoldierData : IComponentData
{
    public Entity battalionEntity;
    public Entity currentTarget;
    public float3 velocity;
    public float maxSpeed;
    public float maxForce;
    public float neighborRadius;
    public float separationRadius;
    public float3 targetPosition;
    public float attackRange;
    public float attackCooldown;
    public float cooldownRemaining;
    public float dashSpeed;
    public float dashHeight;
    public float attackT;
    public float dashTotalTime;
    public float3 attackOrigin;
    public float3 attackTarget;
    public byte attackState;
    public int currentHP;
    public int maxHP;
}

// ---- Player Command ----
public struct PlayerCommandData : IComponentData
{
    public Entity selectedBattalion;
    public float3 targetCell;
    public CommandType commandType;
    public bool pending;
}

// ---- Player Gold ----
public struct PlayerGoldData : IComponentData { public int gold; }

// ---- Health ----
public struct HealthData : IComponentData { public int currentHP; public int maxHP; }

// ---- Entity-GameObject link ----
public struct EntityLink : IComponentData { public int goInstanceID; }

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
