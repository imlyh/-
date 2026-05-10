using UnityEngine;

public class Soldier : MonoBehaviour
{
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;
    public float dashSpeed = 10f;
    public float dashHeight = 0.25f;

    private Battalion battalion;
    private float cooldownRemaining;

    // Attack state
    private enum SoldierState { Idle, AttackingForward, AttackingBack }
    private SoldierState state = SoldierState.Idle;
    private Vector3 attackOrigin;
    private Vector3 attackTarget;
    private float attackT;
    private float dashTotalTime;

    void Awake()
    {
        battalion = GetComponentInParent<Battalion>();
    }

    void Update()
    {
        if (cooldownRemaining > 0)
            cooldownRemaining -= Time.deltaTime;

        switch (state)
        {
            case SoldierState.Idle:
                CheckForEnemy();
                break;
            case SoldierState.AttackingForward:
                AttackForwardTick();
                break;
            case SoldierState.AttackingBack:
                AttackBackTick();
                break;
        }
    }

    void CheckForEnemy()
    {
        if (cooldownRemaining > 0) return;
        if (battalion == null) return;

        var hits = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var h in hits)
        {
            var otherBattalion = h.GetComponentInParent<Battalion>();
            if (otherBattalion != null && otherBattalion != battalion && otherBattalion.owner != battalion.owner)
            {
                StartAttack(h.transform.position);
                return;
            }
        }
    }

    void StartAttack(Vector3 enemyPos)
    {
        state = SoldierState.AttackingForward;
        attackOrigin = transform.position;
        attackOrigin.y = 0;
        attackTarget = enemyPos;
        attackTarget.y = 0;
        attackT = 0;
        cooldownRemaining = attackCooldown;
        float dist = Vector3.Distance(attackOrigin, attackTarget);
        dashTotalTime = Mathf.Max(0.08f, dist / dashSpeed);
    }

    void AttackForwardTick()
    {
        attackT += Time.deltaTime / dashTotalTime;
        float t = Mathf.Clamp01(attackT);
        float xz = EaseOut(t);
        Vector3 pos = Vector3.Lerp(attackOrigin, attackTarget, xz);
        pos.y = dashHeight * Mathf.Sin(t * Mathf.PI);
        transform.position = pos;

        if (attackT >= 1f)
        {
            Debug.Log($"[{name}] 冲击!");
            state = SoldierState.AttackingBack;
            attackT = 0;
        }
    }

    void AttackBackTick()
    {
        attackT += Time.deltaTime / dashTotalTime;
        float t = Mathf.Clamp01(attackT);
        float xz = EaseOut(t);
        Vector3 pos = Vector3.Lerp(attackTarget, attackOrigin, xz);
        pos.y = dashHeight * Mathf.Sin(t * Mathf.PI);
        transform.position = pos;

        if (attackT >= 1f)
        {
            transform.position = attackOrigin;
            state = SoldierState.Idle;
        }
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
