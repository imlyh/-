using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum BattalionOwner { Player, Enemy }

public enum BattalionState { Idle, Moving, Gathering }

public class Battalion : MonoBehaviour
{
    [Header("Config")]
    public BattalionOwner owner = BattalionOwner.Player;
    public float moveSpeed = 4f;
    public float bobHeight = 0.2f;
    public float bobFrequency = 8f;
    public float gatherInterval = 2f;
    public float formationSpacing = 0.55f;

    [Header("Prefab")]
    public GameObject soldierPrefab;

    [Header("Runtime")]
    [SerializeField] private BattalionState state = BattalionState.Idle;
    [SerializeField] private Vector3 targetCell;
    [SerializeField] private float gatherAccum;

    private List<Transform> soldiers = new();
    private Vector3[] formationOffsets;

    // Path movement
    private List<Vector3> pathPoints = new();
    private int pathIndex;
    private float bobPhase;

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
        bobPhase = Random.Range(0f, 100f);
    }

    GameObject CreateDefaultSoldier()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        go.AddComponent<Soldier>();
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
        switch (state)
        {
            case BattalionState.Moving:
                MoveTick();
                break;
            case BattalionState.Gathering:
                GatherTick();
                break;
        }
    }

    // ===== Commands =====

    public void CommandMove(Vector3 cellCenter)
    {
        targetCell = new Vector3(cellCenter.x, 0, cellCenter.z);
        BuildPath(transform.position, targetCell);
        if (pathPoints.Count > 0)
        {
            pathIndex = 0;
            state = BattalionState.Moving;
        }
    }

    public void SetSelected(bool sel)
    {
        if (selectionRing != null) selectionRing.SetActive(sel);
    }

    // ===== Pathfinding =====

    void BuildPath(Vector3 from, Vector3 to)
    {
        pathPoints.Clear();
        var navPath = new NavMeshPath();
        if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, navPath) &&
            navPath.status == NavMeshPathStatus.PathComplete)
        {
            for (int i = 0; i < navPath.corners.Length; i++)
            {
                Vector3 pt = navPath.corners[i];
                pt.y = 0;
                pathPoints.Add(pt);
            }
        }
        // Always add exact target
        Vector3 target = to;
        target.y = 0;
        if (pathPoints.Count == 0 || pathPoints[pathPoints.Count - 1] != target)
            pathPoints.Add(target);
    }

    // ===== Continuous Movement with Bob =====

    void MoveTick()
    {
        if (pathIndex >= pathPoints.Count)
        {
            if (CheckGoldMine(transform.position))
            { state = BattalionState.Gathering; gatherAccum = 0; }
            else
                state = BattalionState.Idle;
            return;
        }

        Vector3 target = pathPoints[pathIndex];
        Vector3 flatPos = transform.position;
        flatPos.y = 0;

        Vector3 dir = target - flatPos;
        float dist = dir.magnitude;

        float step = moveSpeed * Time.deltaTime;
        if (step >= dist)
        {
            flatPos = target;
            pathIndex++;
            bobPhase += dist * bobFrequency;
        }
        else
        {
            flatPos += dir / dist * step;
            bobPhase += step * bobFrequency;
        }

        float yBob = Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight;
        transform.position = new Vector3(flatPos.x, yBob, flatPos.z);

        // Soldiers track formation positions
        for (int i = 0; i < soldiers.Count; i++)
        {
            Vector3 tw = transform.TransformPoint(formationOffsets[i]);
            soldiers[i].position = Vector3.Lerp(soldiers[i].position, tw, 15f * Time.deltaTime);
        }
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

    void OnDestroy()
    {
        foreach (var s in soldiers)
            if (s != null) Destroy(s.gameObject);
        if (selectionRing != null) Destroy(selectionRing);
    }
}
