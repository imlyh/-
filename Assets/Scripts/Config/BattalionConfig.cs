using UnityEngine;

[CreateAssetMenu(menuName = "Conquest/Battalion Config")]
public class BattalionConfig : ScriptableObject
{
    [Header("Soldier")]
    [Range(0.5f, 5f)] public float attackRange = 1.5f;
    [Range(0.1f, 5f)] public float attackCooldown = 1.5f;
    [Range(1f, 30f)]  public float dashSpeed = 10f;
    [Range(0.05f, 1f)] public float dashHeight = 0.25f;

    [Header("Battalion")]
    [Range(1f, 10f)]  public float moveSpeed = 4f;
    [Range(0.5f, 3f)] public float detectionRange = 1.3f;
    [Range(0.05f, 1f)] public float bobHeight = 0.2f;
    [Range(1f, 20f)]  public float bobFrequency = 8f;
    [Range(0.2f, 1f)] public float formationSpacing = 0.55f;

    [Header("Enemy AI")]
    [Range(1f, 30f)]  public float miningDuration = 8f;
    [Range(1f, 30f)]  public float attackDuration = 15f;

    [Header("NavMeshAgent")]
    [Range(0.5f, 5f)]  public float agentRadius = 0.2f;
    [Range(0.5f, 5f)]  public float agentHeight = 0.5f;
}
