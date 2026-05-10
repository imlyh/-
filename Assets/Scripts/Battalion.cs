using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum BattalionOwner { Player, Enemy }

public enum BattalionState { Idle, HopMoving, Gathering, Attacking }

public class Battalion : MonoBehaviour
{
    [Header("Config")]
    public BattalionOwner owner = BattalionOwner.Player;
    public float hopDuration = 0.18f;
    public float hopHeight = 0.3f;
    public float attackCooldown = 1.5f;
    public float attackRange = 1.5f;
    public float gatherInterval = 2f;
    public float formationSpacing = 0.55f;

    [Header("Prefab")]
    public GameObject soldierPrefab;

    [Header("Runtime")]
    [SerializeField] private BattalionState state = BattalionState.Idle;
    [SerializeField] private Vector3 targetCell;
    [SerializeField] private float gatherAccum;
    [SerializeField] private float attackCooldownRemaining;

    private List<Transform> soldiers = new();
    private Vector3[] formationOffsets;

    // Hop
    private List<Vector3> pathCells = new();
    private int pathIndex;
    private Vector3 hopFrom, hopTo;
    private float hopT, hopTotalTime;

    // Attack
    private Vector3 attackOrigin, attackTarget;
    private bool attackForward;
    private float attackT;

    private GameObject selectionRing;

    // ===== Lifecycle =====

    void Start()
    {
        bool usingDefault = soldierPrefab == null;
        if (usingDefault) soldierPrefab = CreateDefaultSoldier();

        gameObject.tag = owner == BattalionOwner.Player ? "PlayerUnit" : "EnemyUnit";
        SpawnSoldiers();

        if (usingDefault && soldierPrefab != null)
        {
            Destroy(soldierPrefab);
            soldierPrefab = null;
        }

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
        rb.isKinematic = true;
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
        }

        for (int i = 0; i < soldiers.Count; i++)
        {
            for (int j = i + 1; j < soldiers.Count; j++)
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
            case BattalionState.HopMoving:
                HopTick();
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

    public void CommandMove(Vector3 cellCenter)
    {
        targetCell = new Vector3(cellCenter.x, 0, cellCenter.z);
        BuildPath(transform.position, targetCell);
        if (pathCells.Count > 0)
        {
            pathIndex = 0;
            StartHop(pathCells[0]);
        }
    }

    public void SetSelected(bool sel)
    {
        if (selectionRing != null) selectionRing.SetActive(sel);
    }

    static Vector3 WorldToGrid(Vector3 pos) => new(Mathf.Round(pos.x), 0, Mathf.Round(pos.z));

    // ===== Pathfinding =====

    void BuildPath(Vector3 from, Vector3 to)
    {
        pathCells.Clear();
        var navPath = new NavMeshPath();
        if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, navPath) &&
            navPath.status == NavMeshPathStatus.PathComplete)
        {
            Vector3 lastCell = WorldToGrid(from);
            pathCells.Add(lastCell);
            for (int i = 0; i < navPath.corners.Length; i++)
            {
                Vector3 cell = WorldToGrid(navPath.corners[i]);
                if (cell != lastCell && IsValidCell(cell))
                {
                    pathCells.Add(cell);
                    lastCell = cell;
                }
            }
            Vector3 final = WorldToGrid(to);
            if (final != lastCell && IsValidCell(final))
                pathCells.Add(final);
        }
        else
        {
            // Fallback: straight line
            pathCells.Add(WorldToGrid(from));
            Vector3 t = WorldToGrid(to);
            if (t != pathCells[0]) pathCells.Add(t);
        }
    }

    static bool IsValidCell(Vector3 cell) =>
        cell.x >= 0 && cell.x <= 29 && cell.z >= 0 && cell.z <= 19;

    // ===== Hop Movement =====

    void StartHop(Vector3 toCell)
    {
        state = BattalionState.HopMoving;
        hopFrom = transform.position;
        hopFrom.y = 0;
        hopTo = toCell;
        hopT = 0;
        float dist = Vector3.Distance(hopFrom, hopTo);
        hopTotalTime = hopDuration * Mathf.Max(1f, dist);
    }

    void HopTick()
    {
        hopT += Time.deltaTime / hopTotalTime;

        if (hopT >= 1f)
        {
            transform.position = hopTo;
            pathIndex++;
            if (pathIndex < pathCells.Count)
                StartHop(pathCells[pathIndex]);
            else if (CheckGoldMine(transform.position))
            { state = BattalionState.Gathering; gatherAccum = 0; }
            else
                state = BattalionState.Idle;
        }
        else
        {
            float xz = EaseInOut(hopT);
            Vector3 pos = Vector3.Lerp(hopFrom, hopTo, xz);
            pos.y = hopHeight * Mathf.Sin(hopT * Mathf.PI);
            transform.position = pos;
        }

        // Soldiers follow with slight lag
        for (int i = 0; i < soldiers.Count; i++)
        {
            Vector3 tw = transform.TransformPoint(formationOffsets[i]);
            soldiers[i].position = Vector3.Lerp(soldiers[i].position, tw, 15f * Time.deltaTime);
            soldiers[i].position = new Vector3(soldiers[i].position.x, transform.position.y, soldiers[i].position.z);
        }
    }

    static float EaseInOut(float t) =>
        t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

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
            { StartAttack(other.transform.position); return; }
        }
    }

    void StartAttack(Vector3 enemyPos)
    {
        state = BattalionState.Attacking;
        attackOrigin = transform.position;
        attackOrigin.y = 0;
        attackTarget = enemyPos;
        attackTarget.y = 0;
        attackForward = true;
        attackT = 0;
        attackCooldownRemaining = attackCooldown;
    }

    void AttackTick()
    {
        float dashDur = 0.15f;
        float dist = Vector3.Distance(attackOrigin, attackTarget);
        if (dist > 0.01f) dashDur = dist / 10f;

        if (attackForward)
        {
            attackT += Time.deltaTime / dashDur;
            float t = Mathf.Clamp01(attackT);
            Vector3 pos = Vector3.Lerp(attackOrigin, attackTarget, EaseOut(t));
            pos.y = hopHeight * 0.7f * Mathf.Sin(t * Mathf.PI);
            transform.position = pos;
            if (attackT >= 1f) { Debug.Log($"[{name}] 撞击敌军!"); attackForward = false; attackT = 0; }
        }
        else
        {
            attackT += Time.deltaTime / dashDur;
            float t = Mathf.Clamp01(attackT);
            Vector3 pos = Vector3.Lerp(attackTarget, attackOrigin, EaseOut(t));
            pos.y = hopHeight * 0.7f * Mathf.Sin(t * Mathf.PI);
            transform.position = pos;
            if (attackT >= 1f) { transform.position = attackOrigin; state = BattalionState.Idle; }
        }

        for (int i = 0; i < soldiers.Count; i++)
        {
            Vector3 tw = transform.TransformPoint(formationOffsets[i]);
            soldiers[i].position = Vector3.Lerp(soldiers[i].position, tw, 20f * Time.deltaTime);
            soldiers[i].position = new Vector3(soldiers[i].position.x, transform.position.y, soldiers[i].position.z);
        }
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

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
