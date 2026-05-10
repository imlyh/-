using UnityEngine;

public class Soldier : MonoBehaviour
{
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;
    public float dashSpeed = 10f;
    public float dashHeight = 0.25f;

    private Battalion battalion;
    private float cooldownRemaining;
    private int soldierIndex = -1;

    private enum SoldierState { Idle, AttackingForward, AttackingBack }
    private SoldierState state = SoldierState.Idle;
    private Vector3 attackOrigin;
    private Vector3 attackTarget;
    private float attackT;
    private float dashTotalTime;

    void Awake()
    {
        battalion = GetComponentInParent<Battalion>();
        // Extract index from name "Soldier_N"
        var parts = name.Split('_');
        if (parts.Length > 1) int.TryParse(parts[1], out soldierIndex);
    }

    void Update()
    {
        if (cooldownRemaining > 0)
            cooldownRemaining -= Time.deltaTime;

        switch (state)
        {
            case SoldierState.Idle:
                CheckForTarget();
                break;
            case SoldierState.AttackingForward:
                AttackForwardTick();
                break;
            case SoldierState.AttackingBack:
                AttackBackTick();
                break;
        }
    }

    void CheckForTarget()
    {
        if (cooldownRemaining > 0) return;
        if (battalion == null) return;

        var bs = battalion.CurrentState;
        if (bs != BattalionState.Mining && bs != BattalionState.Attacking)
            return;

        var hits = Physics.OverlapSphere(transform.position, attackRange);

        if (bs == BattalionState.Attacking)
        {
            // Find closest enemy soldier
            Transform closest = null;
            float closestDist = float.MaxValue;
            foreach (var h in hits)
            {
                var otherBat = h.GetComponentInParent<Battalion>();
                if (otherBat == null || otherBat == battalion || otherBat.owner == battalion.owner)
                    continue;
                float d = Vector3.Distance(transform.position, h.transform.position);
                if (d < closestDist) { closestDist = d; closest = h.transform; }
            }
            if (closest != null)
            { StartAttack(closest.position); return; }
        }

        if (bs == BattalionState.Mining)
        {
            foreach (var h in hits)
            {
                if (!h.name.StartsWith("GoldMine")) continue;
                // Offset target by soldier index to spread out
                Vector3 offset = Vector3.zero;
                if (soldierIndex >= 0)
                {
                    float angle = soldierIndex * Mathf.PI * 0.5f;
                    offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.4f;
                }
                StartAttack(h.transform.position + offset);
                return;
            }
        }
    }

    void StartAttack(Vector3 targetPos)
    {
        state = SoldierState.AttackingForward;
        attackOrigin = transform.position;
        attackOrigin.y = 0;
        attackTarget = targetPos;
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
        Vector3 pos = Vector3.Lerp(attackOrigin, attackTarget, EaseOut(t));
        pos.y = dashHeight * Mathf.Sin(t * Mathf.PI);
        transform.position = pos;

        if (attackT >= 1f)
        {
            state = SoldierState.AttackingBack;
            attackT = 0;
        }
    }

    void AttackBackTick()
    {
        attackT += Time.deltaTime / dashTotalTime;
        float t = Mathf.Clamp01(attackT);
        Vector3 pos = Vector3.Lerp(attackTarget, attackOrigin, EaseOut(t));
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
