using UnityEngine;
using UnityEngine.Events;

//place one encounter trigger with its own boss prefab and starting position
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossEncounter2D : MonoBehaviour
{
    [SerializeField] private BossGrapple2D bossPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private PlayerVitals2D player;
    [SerializeField] private bool wakeOnEntry = true;
    [SerializeField] private vineController wakeVines;
    [SerializeField, Min(0f)] private float respawnPause = 2f;
    private PlayerRespawn2D playerRespawn;
    private bool hasStarted;

    [System.Serializable]
    private class FightPoint
    {
        public Transform point;
        public GameObject gate;
        [Tooltip("Optional points to pass on the way here from the previous fight point.")]
        public Transform[] via = new Transform[0];
    }

    [Header("Final fight (leave empty for a normal chase)")]
    [SerializeField] private FightPoint[] fightPoints = new FightPoint[0];
    [SerializeField, Min(0f)] private float fightPause = 2f;
    [SerializeField, Min(1f)] private float fightRange = 90f;
    [SerializeField, Min(1f)] private float fightSpeed = 32f;
    [SerializeField, Min(0.1f)] private float fightShotInterval = 0.5f;
    [SerializeField] private UnityEvent onFinished = new UnityEvent();
    private int fightIndex;
    private bool fightFinished;
    public bool FightFinished => fightFinished;
    public event System.Action Finished;
    private bool IsFight => fightPoints.Length > 0;

    public void Configure(BossGrapple2D prefab, Transform spawn, vineController camouflage)
    {
        bossPrefab = prefab;
        spawnPoint = spawn;
        wakeVines = camouflage;
        wakeOnEntry = false;
    }

    private BossGrapple2D boss;
    private bool triggered;
    private bool waitingForRespawn;
    private bool started;
    public BossGrapple2D ActiveBoss => boss;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        if (player == null) player = FindAnyObjectByType<PlayerVitals2D>();
        if (player != null) playerRespawn = player.GetComponent<PlayerRespawn2D>();
    }

    private void OnEnable()
    {
        if (player != null) player.Died += PlayerDied;
        if (playerRespawn != null) playerRespawn.Respawned += PlayerRespawned;
        if (started) SpawnBoss();
    }

    private void Start()
    {
        started = true;
        if (IsFight)
        {
            foreach (FightPoint entry in fightPoints)
                if (entry == null || entry.point == null)
                {
                    Debug.LogWarning("Assign every MABEL fight point.", this);
                    return;
                }
        }
        if (bossPrefab == null || (!IsFight && spawnPoint == null))
        {
            Debug.LogWarning("Boss Encounter needs a boss prefab and spawn point.", this);
            return;
        }
        SpawnBoss();
    }

    private void Update()
    {
        if (boss != null && boss.CurrentState != BossGrapple2D.State.Idle)
            hasStarted = true;
        if (!waitingForRespawn || player == null || player.IsDead) return;

        //the normal player respawn has finished, now prepare a fresh encounter
        waitingForRespawn = false;
        SpawnBoss();
    }

    private void SpawnBoss()
    {
        if (boss != null || fightFinished || bossPrefab == null || (!IsFight && spawnPoint == null)) return;
        if (IsFight && (fightIndex >= fightPoints.Length || fightPoints[fightIndex] == null ||
            fightPoints[fightIndex].point == null)) return;
        if (player != null && player.IsDead)
        {
            waitingForRespawn = true;
            return;
        }

        //finished rounds stay finished after death; respawn at the current fight point
        Transform spawn = IsFight ? fightPoints[fightIndex].point :
            hasStarted && playerRespawn != null && playerRespawn.BossSpawnPoint != null
                ? playerRespawn.BossSpawnPoint : spawnPoint;
        boss = Instantiate(bossPrefab, spawn.position, spawn.rotation);
        //activate first so Awake has prepared the body and arms
        boss.gameObject.SetActive(true);
        boss.PrepareEncounter(player != null ? player.transform : null,
            wakeVines, hasStarted, hasStarted ? respawnPause : 0f);
        if (IsFight)
        {
            boss.SetFightSpeed(fightSpeed);
            boss.SetFightPoint(fightPoints[fightIndex].point, fightPause);
            boss.Hurt += FightBurned;
            BossSpit2D spit = boss.GetComponent<BossSpit2D>();
            if (spit != null) spit.EnableFightAttack(fightRange, fightShotInterval);
        }
        triggered = hasStarted;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        StartEncounter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        StartEncounter(other);
    }

    private void StartEncounter(Collider2D other)
    {
        if (!wakeOnEntry || triggered || boss == null || player == null || player.IsDead ||
            other.GetComponentInParent<PlayerVitals2D>() != player) return;

        Wake();
    }

    public void Wake()
    {
        if (triggered || boss == null || player == null || player.IsDead) return;
        triggered = true;
        hasStarted = true;
        boss.SetAggro(true);
    }

    private void FightBurned()
    {
        if (!hasStarted || fightFinished || boss == null) return;

        GameObject gate = fightPoints[fightIndex].gate;
        if (gate != null) gate.SetActive(false);
        fightIndex++;

        if (fightIndex < fightPoints.Length)
            boss.SetFightPoint(fightPoints[fightIndex].point, fightPause, fightPoints[fightIndex].via);
        else
        {
            fightFinished = true;
            boss.FinishFight();
            onFinished.Invoke();
            Finished?.Invoke();
        }
    }

    private void PlayerDied()
    {
        if (boss != null && boss.CurrentState != BossGrapple2D.State.Idle) hasStarted = true;
        ClearBoss();
        waitingForRespawn = true;
    }

    private void PlayerRespawned()
    {
        if (!waitingForRespawn) return;
        waitingForRespawn = false;
        SpawnBoss();
    }

    private void ClearBoss()
    {
        if (boss != null)
        {
            boss.Hurt -= FightBurned;
            //stop attacks and release the grapples immediately
            boss.gameObject.SetActive(false);
            Destroy(boss.gameObject);
        }
        boss = null;
        triggered = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Transform previous = null;
        foreach (FightPoint entry in fightPoints)
        {
            if (entry == null || entry.point == null) continue;
            //the small yellow points are travel guides, not extra fight rounds
            if (previous != null && entry.via != null)
                foreach (Transform point in entry.via)
                {
                    if (point == null) continue;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                    Gizmos.DrawLine(previous.position, point.position);
                    previous = point;
                }
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(entry.point.position, 1.5f);
            if (previous != null) Gizmos.DrawLine(previous.position, entry.point.position);
            previous = entry.point;
        }
    }

    private void OnDisable()
    {
        if (player != null) player.Died -= PlayerDied;
        if (playerRespawn != null) playerRespawn.Respawned -= PlayerRespawned;
        ClearBoss();
        waitingForRespawn = false;
    }
}
