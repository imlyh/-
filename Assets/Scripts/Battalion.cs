using UnityEngine;
using System.Collections.Generic;

public enum BattalionOwner { Player, Enemy }

public enum BattalionState { Idle, Moving, Gathering, Attacking }

public class Battalion : MonoBehaviour
{
    [Header("Config")]
    public BattalionOwner owner = BattalionOwner.Player;
    public float moveSpeed = 3f;
    public float attackSpeed = 12f;
    public float attackCooldown = 1.5f;
    public float attackRange = 1.5f;
    public float gatherInterval = 2f;
    public float formationSpacing = 0.55f;

    [Header("Prefab")]
    public GameObject soldierPrefab;

    [Header("Runtime")]
    [SerializeField] private BattalionState state = BattalionState.Idle;
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private float gatherAccum;
    [SerializeField] private float attackCooldownRemaining;

    private List<Transform> soldiers = new();
    private List<Rigidbody> soldierRBs = new();
    private Vector3[] formationOffsets;
    private Vector3 attackOrigin;
    private Transform attackEnemy;
    private bool attackForward;
    private float attackT;
    private GameObject selectionRing;

    // ===== Lifecycle =====

    void Start()
    {
        bool usingDefault = soldierPrefab == null;
        if (usingDefault)
            soldierPrefab = CreateDefaultSoldier();

        gameObject.tag = owner == BattalionOwner.Player ? "PlayerUnit" : "EnemyUnit";
        SpawnSoldiers();

        if (usingDefault && soldierPrefab != null)
        {
            Destroy(soldierPrefab);
            soldierPrefab = null;
        }

        // Add a small trigger for enemy detection
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;

        CreateSelectionRing();
    }

    GameObject CreateDefaultSoldier()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        go.name = "DefaultSoldier";
        return go;
    }

    void SpawnSoldiers()
    {
        float s = formationSpacing;
        formationOffsets = new Vector3[]
        {
            new(-s / 2, 0, -s / 2),
            new( s / 2, 0, -s / 2),
            new(-s / 2, 0,  s / 2),
            new( s / 2, 0,  s / 2),
        };

        for (int i = 0; i < 4; i++)
        {
            var go = Instantiate(soldierPrefab, transform);
            go.name = $"Soldier_{i}";
            go.transform.localPosition = formationOffsets[i];
            go.transform.localRotation = Quaternion.identity;

            go.tag = gameObject.tag;
            go.layer = gameObject.layer;

            soldiers.Add(go.transform);
            var rb = go.GetComponent<Rigidbody>();
            soldierRBs.Add(rb);
        }

        // Prevent soldiers within same battalion from colliding with each other
        for (int i = 0; i < soldierRBs.Count; i++)
        {
            for (int j = i + 1; j < soldierRBs.Count; j++)
            {
                var ca = soldiers[i].GetComponent<Collider>();
                var cb = soldiers[j].GetComponent<Collider>();
                if (ca != null && cb != null)
                    Physics.IgnoreCollision(ca, cb);
            }
        }
    }

    void CreateSelectionRing()
    {
        selectionRing = GameObject.CreatePrimitive(PrimitiveType.Quad);
        selectionRing.name = "SelectionRing";
        selectionRing.transform.SetParent(transform);
        selectionRing.transform.localPosition = new Vector3(0, 0.03f, 0);
        selectionRing.transform.localRotation = Quaternion.Euler(90, 0, 0);
        selectionRing.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        var mr = selectionRing.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mr.material.color = new Color(0, 1, 0, 0.35f);
        mr.material.SetFloat("_Surface", 1);
        selectionRing.SetActive(false);
        DestroyImmediate(selectionRing.GetComponent<Collider>());
    }

    // ===== Update =====

    void Update()
    {
        if (attackCooldownRemaining > 0)
            attackCooldownRemaining -= Time.deltaTime;

        switch (state)
        {
            case BattalionState.Idle:
                CheckAutoAttack();
                break;
            case BattalionState.Moving:
                MoveTick();
                CheckAutoAttack();
                break;
            case BattalionState.Gathering:
                GatherTick();
                CheckAutoAttack();
                break;
            case BattalionState.Attacking:
                AttackTick();
                break;
        }
    }

    // ===== Commands =====

    public void CommandMove(Vector3 worldPos)
    {
        targetPosition = new Vector3(worldPos.x, 0, worldPos.z);
        state = BattalionState.Moving;
    }

    public void SetSelected(bool sel)
    {
        if (selectionRing != null)
            selectionRing.SetActive(sel);
    }

    // ===== Movement =====

    void MoveTick()
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0;
        float dist = toTarget.magnitude;

        if (dist < 0.15f)
        {
            // Check for gold mine at arrival
            if (CheckGoldMine(targetPosition))
            {
                state = BattalionState.Gathering;
                gatherAccum = 0;
            }
            else
            {
                state = BattalionState.Idle;
            }
            StopSoldiers();
            return;
        }

        Vector3 dir = toTarget / dist;
        float step = moveSpeed * Time.deltaTime;
        if (step > dist) step = dist;
        transform.position += dir * step;

        // Move soldiers toward formation positions
        for (int i = 0; i < soldiers.Count; i++)
        {
            Vector3 target = transform.TransformPoint(formationOffsets[i]);
            Vector3 delta = target - soldiers[i].position;
            delta.y = 0;
            float d = delta.magnitude;
            if (d > 0.05f)
                soldierRBs[i].linearVelocity = delta / d * moveSpeed * 1.3f;
            else
                soldierRBs[i].linearVelocity = Vector3.zero;
        }
    }

    void StopSoldiers()
    {
        for (int i = 0; i < soldierRBs.Count; i++)
            soldierRBs[i].linearVelocity = Vector3.zero;
    }

    // ===== Gathering =====

    bool CheckGoldMine(Vector3 pos)
    {
        var hits = Physics.OverlapSphere(pos, 0.7f);
        foreach (var h in hits)
            if (h.name == "GoldMine") return true;
        return false;
    }

    void GatherTick()
    {
        gatherAccum += Time.deltaTime;
        if (gatherAccum >= gatherInterval)
        {
            gatherAccum -= gatherInterval;
            Debug.Log($"[{name}] 采集资源 +10 金");
        }
    }

    // ===== Auto Attack =====

    void CheckAutoAttack()
    {
        if (attackCooldownRemaining > 0) return;

        var hits = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var h in hits)
        {
            var other = h.GetComponentInParent<Battalion>();
            if (other != null && other != this && other.owner != owner)
            {
                StartAttack(other.transform);
                return;
            }
        }
    }

    void StartAttack(Transform enemy)
    {
        state = BattalionState.Attacking;
        attackOrigin = transform.position;
        attackEnemy = enemy;
        attackForward = true;
        attackT = 0;
        attackCooldownRemaining = attackCooldown;
    }

    void AttackTick()
    {
        float dashDur = Vector3.Distance(attackOrigin, attackEnemy.position) / attackSpeed;
        if (dashDur < 0.1f) dashDur = 0.1f;

        if (attackForward)
        {
            attackT += Time.deltaTime / dashDur;
            float t = EaseOut(attackT);
            Vector3 target = attackEnemy != null ? attackEnemy.position : attackOrigin;
            transform.position = Vector3.Lerp(attackOrigin, target, Mathf.Clamp01(t));

            if (attackT >= 1f)
            {
                Debug.Log($"[{name}] 撞击敌军!");
                attackForward = false;
                attackT = 0;
            }
        }
        else
        {
            attackT += Time.deltaTime / dashDur;
            float t = EaseOut(attackT);
            transform.position = Vector3.Lerp(attackEnemy != null ? attackEnemy.position : attackOrigin,
                                               attackOrigin, Mathf.Clamp01(t));

            if (attackT >= 1f)
            {
                transform.position = attackOrigin;
                state = BattalionState.Idle;
            }
        }
    }

    float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    // ===== Gizmo =====

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    void OnDestroy()
    {
        foreach (var s in soldiers)
            if (s != null) Destroy(s.gameObject);
        if (selectionRing != null) Destroy(selectionRing);
    }
}
