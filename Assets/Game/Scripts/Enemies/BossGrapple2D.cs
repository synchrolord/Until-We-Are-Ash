using System.Collections;
using UnityEngine;

//one moving body that follows the player by pulling on walls
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class BossGrapple2D : MonoBehaviour
{
    public enum State { Idle = 0, Aggro = 1, Retreat = 2, Hunt = 3, Recover = 4 }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private LayerMask wallMask;

    [Header("Idle")]
    [SerializeField] private bool aggro = true;
    [SerializeField] private vineController wakeVines;
    [SerializeField, Min(0f)] private float breathSize = 0.05f;
    [SerializeField, Min(0f)] private float breathSpeed = 1.5f;

    [Header("Retreat")]
    [SerializeField] private BossBody2D vineBody;
    [SerializeField, Min(0.1f)] private float retreatTime = 2f;
    [SerializeField, Min(0f)] private float fireProtectionTime = 6f;
    [SerializeField] private State state;
    [SerializeField, Min(1f)] private float retreatDistance = 18f;
    [SerializeField, Min(0.1f)] private float recoverTime = 4f;

    [Header("Hunt")]
    [SerializeField, Min(1f)] private float huntDistance = 10f;
    [SerializeField, Range(0.05f, 1f)] private float huntSpeed = 0.3f;
    [SerializeField] private Vector2 attackWait = new Vector2(4f, 8f);
    [SerializeField, Min(0.1f)] private float attackTime = 2.5f;
    [SerializeField, Min(0.1f)] private float idleTime = 2f;
    private float stateTime;
    private float huntLeft;
    private bool awakeOnce;
    private bool wakeAfterPause;
    private float wakeAt;
    private PlayerVitals2D player;
    private float nextBurnCheck;
    private float fireProtectedUntil;

    public State CurrentState => state;
    public event System.Action Attacked;
    public event System.Action Hurt;
    public void NotifyAttack() => Attacked?.Invoke();
    public Transform Target => target;
    public LayerMask WallMask => wallMask;

    public void PrepareEncounter(Transform playerTarget, vineController camouflage, bool chase, float pauseSeconds = 0f)
    {
        target = playerTarget;
        wakeVines = camouflage;
        wakeAt = Time.time + Mathf.Max(0f, pauseSeconds);
        wakeAfterPause = chase && pauseSeconds > 0f;
        nextContactHit = wakeAt + 3f;
        fireProtectedUntil = wakeAt;
        SetAggro(chase && !wakeAfterPause);
    }

    [Header("Vine")]
    [SerializeField] private vineController vines;

    [Header("Anchor")]
    [SerializeField, Range(1, 12)] private int anchorCount = 2;
    [SerializeField, Min(0.05f)] private float shotDelay = 0.3f;
    [SerializeField, Min(0f)] private float anchorSpacing = 4f;
    [SerializeField, Min(0.1f)] private float regenTime = 6f;
    [SerializeField, Min(1f)] private float reach = 12f;
    [SerializeField, Range(0f, 180f)] private float spread = 65f;
    [SerializeField, Range(1, 100)] private int moveSteps = 12;
    [SerializeField, Min(0f)] private float embedDelay = 0.05f;
    [SerializeField, Min(0.05f)] private float pause = 0.08f;

    [Header("Pull")]
    [SerializeField, Min(0.1f)] private float pullSpeed = 14f;
    [SerializeField, Min(0.1f)] private float pullTime = 0.8f;
    [SerializeField, Min(0.1f)] private float closeDistance = 1f;
    [SerializeField, Min(0.1f)] private float maxSpeed = 16f;

    [Header("Contact")]
    [SerializeField, Min(0f)] private float contactDamage = 15f;
    [SerializeField, Min(0.1f)] private float contactInterval = 0.75f;
    private float nextContactHit;

    [Header("Test Rope")]
    [SerializeField] private Color ropeColor = new Color(0.3f, 0.5f, 0.2f);
    [SerializeField, Min(0.01f)] private float ropeWidth = 0.08f;
    [SerializeField] private int sortingOrder = 110;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Material ropeMaterial;
    private Transform bodyPoint;
    private Arm[] arms;
    private Arm pullingArm;

    public void SetAggro(bool value)
    {
        aggro = value;
        if (value && !awakeOnce) Wake();
    }

    private void Wake()
    {
        awakeOnce = true;
        SetState(State.Hunt);
    }

    private void SetState(State next)
    {
        //a rush can hook the player, but it must let go before stalking or retreating
        if (next != State.Aggro && arms != null)
            foreach (Arm arm in arms)
                if (arm.wall != null && arm.wall.GetComponentInParent<PlayerVitals2D>() != null) Release(arm);
        state = next;
        stateTime = 0f;
        if (next == State.Hunt)
            huntLeft = Random.Range(Mathf.Max(0.1f, Mathf.Min(attackWait.x, attackWait.y)),
                Mathf.Max(0.1f, Mathf.Max(attackWait.x, attackWait.y)));
        if (next == State.Recover) Extinguish();
    }

    public void Ignite()
    {
        if (Time.time < fireProtectedUntil || state == State.Retreat || state == State.Recover) return;
        awakeOnce = aggro = true;
        SetState(State.Retreat);
        Hurt?.Invoke();
    }

    private void Extinguish()
    {
        float protection = recoverTime + fireProtectionTime;
        fireProtectedUntil = Time.time + protection;
        if (vineBody != null) vineBody.Extinguish(protection);
        foreach (Arm arm in arms)
            if (arm.vine != null) arm.vine.Extinguish(protection);
    }

    private void Update()
    {
        if (target == null)
        {
            player = FindAnyObjectByType<PlayerVitals2D>();
            if (player != null) target = player.transform;
        }
        else if (player == null) player = target.GetComponentInParent<PlayerVitals2D>();
        if (target == null || (player != null && player.IsDead)) return;
        //hang at the checkpoint briefly before starting the chase again
        if (wakeAfterPause)
        {
            if (Time.time < wakeAt) return;
            wakeAfterPause = false;
            SetAggro(true);
        }
        stateTime += Time.deltaTime;

        //fire wakes the encounter, but can't restart retreat during recovery protection
        if (Time.time >= nextBurnCheck)
        {
            nextBurnCheck = Time.time + 0.1f;
            if (!awakeOnce && wakeVines != null && wakeVines.IsBurning) Wake();
            if (vineBody != null && vineBody.IsBurning) Ignite();
            foreach (Arm arm in arms)
                if (arm.vine != null && arm.vine.IsBurning) { Ignite(); break; }
        }

        float distance = Vector2.Distance(BodyCenter, TargetCenter);
        switch (state)
        {
            case State.Idle:
                if (aggro && stateTime >= idleTime) Wake();
                break;
            case State.Hunt:
                //the attack timer only counts while she is close enough to stalk you
                if (distance <= huntDistance + 3f) huntLeft -= Time.deltaTime;
                if (huntLeft <= 0f) SetState(State.Aggro);
                break;
            case State.Aggro:
                if (stateTime >= attackTime) SetState(State.Hunt);
                break;
            case State.Retreat:
                //the timeout prevents a wall from trapping her in retreat forever
                if (distance >= retreatDistance || stateTime >= retreatTime) SetState(State.Recover);
                break;
            case State.Recover:
                if (stateTime >= recoverTime) SetState(State.Hunt);
                break;
        }
    }

    private Vector2 TargetCenter => player != null ? player.BodyCenter : (Vector2)target.position;

    private bool ShouldPull()
    {
        if (target == null || (player != null && player.IsDead)) return false;
        if (state == State.Aggro || state == State.Retreat) return true;
        return state == State.Hunt && Mathf.Abs(Vector2.Distance(BodyCenter, TargetCenter) - huntDistance) > 2f;
    }

    private Vector2 AimDirection()
    {
        if (state == State.Idle || target == null) return Vector2.up;
        Vector2 offset = TargetCenter - BodyCenter;
        bool away = state == State.Retreat ||
            (state == State.Hunt && offset.magnitude < huntDistance);
        return away ? -offset.normalized : offset.normalized;
    }

    //each anchor keeps its own joint, flight and burning vine
    private sealed class Arm
    {
        public DistanceJoint2D joint;
        public Transform anchor;
        public LineRenderer rope;
        public Coroutine routine;
        public VineRope2D vine;
        public Transform vineAnchor;
        public Transform wall;
        public Vector3 wallPoint;
        public Transform aimedWall;
        public Vector3 aimedPoint;
        public bool restMade;
        public float regrowAt;
        public float idleLength;
    }

    private Vector2 BodyCenter => bodyCollider.bounds.center;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        if (vineBody == null) vineBody = GetComponentInChildren<BossBody2D>(true);
        state = State.Idle;
        bodyPoint = new GameObject("Grapple Point").transform;
        bodyPoint.SetParent(transform, false);
        bodyPoint.position = BodyCenter;

        ropeMaterial = new Material(Shader.Find("Sprites/Default"));
        arms = new Arm[anchorCount];
        for (int i = 0; i < arms.Length; i++)
        {
            Arm arm = new Arm();
            arms[i] = arm;
            arm.joint = gameObject.AddComponent<DistanceJoint2D>();
            //world-attached joints must keep contact with static floors and walls
            arm.joint.enableCollision = true;
            arm.joint.enabled = false;
            arm.joint.autoConfigureDistance = false;
            arm.joint.autoConfigureConnectedAnchor = false;
            arm.joint.maxDistanceOnly = true;
            arm.anchor = new GameObject("Grapple Anchor " + (i + 1)).transform;
            arm.rope = arm.anchor.gameObject.AddComponent<LineRenderer>();
            arm.rope.useWorldSpace = true;
            arm.rope.positionCount = 2;
            arm.rope.startWidth = arm.rope.endWidth = ropeWidth;
            arm.rope.startColor = arm.rope.endColor = ropeColor;
            arm.rope.sortingOrder = sortingOrder;
            arm.rope.sharedMaterial = ropeMaterial;
            arm.rope.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (arms == null) return;
        for (int i = 0; i < arms.Length; i++)
            if (arms[i].routine == null)
                arms[i].routine = StartCoroutine(Follow(arms[i], i * shotDelay));
    }

    private void FixedUpdate()
    {
        //idle is a real hanging body: shorten the same anchors a little, then let go
        if (state == State.Idle)
            foreach (Arm arm in arms)
                if (arm.joint.enabled)
                    arm.joint.distance = Mathf.Max(closeDistance, arm.idleLength -
                        Mathf.Max(0f, Mathf.Sin(Time.time * breathSpeed)) * breathSize);

        foreach (Arm arm in arms)
        {
            if (arm.vine != null && arm.vine.IsBroken)
            {
                //a burned arm cannot immediately grow another rope into the fire
                arm.regrowAt = Time.time + regenTime;
                arm.restMade = false;
                Ignite();
                Release(arm);
            }

            if (!arm.joint.enabled) continue;
            if (arm.wall == null)
                Release(arm);
            else
            {
                arm.joint.connectedAnchor = arm.wall.TransformPoint(arm.wallPoint);
                float distance = Vector2.Distance(BodyCenter, arm.joint.connectedAnchor);

                //the older arm keeps some slack so two joints cannot lock the body
                if (state != State.Idle && arm != pullingArm)
                    arm.joint.distance = Mathf.Max(arm.joint.distance, distance + 0.25f);

                if (arm.vineAnchor != null && arm.aimedWall == null)
                    arm.vineAnchor.position = arm.joint.connectedAnchor;

                if (arm.vine != null)
                    arm.vine.SetLength(arm.aimedWall != null
                        ? Vector2.Distance(BodyCenter, arm.vineAnchor.position)
                        : Mathf.Max(arm.joint.distance, distance));
            }
        }

        float speedLimit = state == State.Hunt ? maxSpeed * huntSpeed :
            state == State.Idle || state == State.Recover ? 1.5f : maxSpeed;
        body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, speedLimit);
    }

    private void LateUpdate()
    {
        foreach (Arm arm in arms)
        {
            if (!arm.rope.enabled) continue;
            arm.rope.SetPosition(0, BodyCenter);
            arm.rope.SetPosition(1, arm.anchor.position);
        }
    }

    private IEnumerator Follow(Arm arm, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        while (true)
        {
            if (Time.time < arm.regrowAt)
            {
                yield return new WaitForSeconds(arm.regrowAt - Time.time);
                continue;
            }

            if (state == State.Idle)
            {
                //make each resting attachment once, then leave its destination alone
                if (!arm.restMade && FindAnchor(arm, out RaycastHit2D restHit))
                {
                    arm.restMade = true;
                    yield return Grapple(arm, restHit);
                }
                yield return new WaitForSeconds(pause);
                continue;
            }
            if (!ShouldPull())
            {
                yield return new WaitForSeconds(pause);
                continue;
            }


            arm.restMade = false;
            if (target == null)
            {
                PlayerMotor2D player = FindAnyObjectByType<PlayerMotor2D>();
                if (player != null) target = player.transform;
            }

            if (target != null &&
                FindAnchor(arm, out RaycastHit2D hit))
            {
                yield return Grapple(arm, hit);
            }

            yield return new WaitForSeconds(pause);
        }
    }

    private bool FindAnchor(Arm arm, out RaycastHit2D chosen)
    {
        chosen = default;
        Vector2 towardPlayer = AimDirection();
        Vector2 tangent = new Vector2(-towardPlayer.y, towardPlayer.x);

        //sample across the aiming cone so spaced anchors have more options
        float side = Random.value < 0.5f ? -1f : 1f;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 9; i++)
        {
            float cone = state == State.Idle ? 40f : spread;
            float angle = Mathf.Lerp(-cone, cone, i / 8f) * side;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = towardPlayer * Mathf.Cos(radians) +
                                tangent * Mathf.Sin(radians);

            //only an attack can grapple the player; hunting and retreat use walls
            int layers = wallMask.value;
            if (state == State.Aggro && target != null) layers |= 1 << target.gameObject.layer;
            RaycastHit2D hit = Physics2D.Raycast(BodyCenter, direction, reach, layers);

            //ignore surfaces too close to make a useful pull
            if (hit.collider == null || hit.distance <= closeDistance ||
                hit.collider.attachedRigidbody == body)
                continue;

            if (!HasSpace(arm, hit.point)) continue;

            //higher anchors help it glide instead of pulling into the floor
            float score = state != State.Idle
                ? Vector2.Dot(direction, towardPlayer) + direction.y * 0.6f
                : Random.value;
            if (score > bestScore)
            {
                bestScore = score;
                chosen = hit;
            }
        }

        return chosen.collider != null;
    }

    private bool HasSpace(Arm arm, Vector2 point)
    {
        foreach (Arm other in arms)
        {
            if (other == arm) continue;

            if (other.joint.enabled && other.wall != null &&
                Vector2.Distance(point, other.wall.TransformPoint(other.wallPoint)) < anchorSpacing)
                return false;

            //reserve the landing point while an anchor is still flying
            if (other.aimedWall != null &&
                Vector2.Distance(point, other.aimedWall.TransformPoint(other.aimedPoint)) < anchorSpacing)
                return false;
        }

        return true;
    }

    private IEnumerator Grapple(Arm arm, RaycastHit2D hit)
    {
        //keep the anchor attached even if the hit object moves
        bool playerAnchor = hit.collider.GetComponentInParent<PlayerVitals2D>() != null;
        Transform wall = hit.collider.transform;
        Vector3 localHit = wall.InverseTransformPoint(hit.point);
        arm.aimedWall = wall;
        arm.aimedPoint = localHit;
        Vector2 start = arm.vineAnchor != null
            ? (Vector2)arm.vineAnchor.position
            : BodyCenter;
        arm.anchor.position = start;
        arm.rope.enabled = arm.vine == null;

        //move the existing tip instead of destroying and rebuilding its vine
        int steps = Mathf.Max(1, moveSteps);
        for (int i = 1; i <= steps && wall != null; i++)
        {
            if (Time.time < arm.regrowAt || (playerAnchor && state != State.Aggro))
            {
                arm.aimedWall = null;
                arm.rope.enabled = false;
                yield break;
            }
            float t = Mathf.SmoothStep(0f, 1f, i / (float)steps);
            arm.anchor.position = Vector2.Lerp(
                start,
                wall.TransformPoint(localHit),
                t);

            if (arm.vineAnchor != null)
            {
                arm.vineAnchor.position = arm.anchor.position;
                arm.vine.SetLength(Vector2.Distance(BodyCenter, arm.anchor.position));
            }

            yield return new WaitForFixedUpdate();
        }

        if (wall != null)
        {
            yield return new WaitForSeconds(embedDelay);

            if (Time.time < arm.regrowAt || wall == null || (playerAnchor && state != State.Aggro) ||
                !HasSpace(arm, wall.TransformPoint(localHit)))
            {
                arm.rope.enabled = false;
                arm.aimedWall = null;
                yield break;
            }

            //keep the previous attachment until the new anchor lands
            arm.joint.enabled = false;
            arm.wall = wall;
            arm.wallPoint = localHit;
            arm.aimedWall = null;
            arm.anchor.position = wall.TransformPoint(localHit);

            arm.joint.anchor = transform.InverseTransformPoint(BodyCenter);
            arm.joint.distance = Vector2.Distance(BodyCenter, arm.anchor.position);
            arm.joint.connectedAnchor = arm.anchor.position;
            arm.idleLength = arm.joint.distance;
            arm.joint.enabled = true;
            pullingArm = arm;
            body.WakeUp();
            arm.rope.enabled = vines == null;

            //reuse the same sprites and preserve their burning progress
            if (vines != null && arm.vine == null)
            {
                arm.vineAnchor = new GameObject("Vine Anchor").transform;
                arm.vineAnchor.position = arm.anchor.position;
                arm.vine = vines.CreateVine(bodyPoint, arm.vineAnchor, arm.joint.distance);
                if (Time.time < fireProtectedUntil)
                    arm.vine.Extinguish(fireProtectedUntil - Time.time);
                //the endpoint is cleaned up with the broken or released vine
                arm.vineAnchor.SetParent(arm.vine.transform, true);
                arm.rope.enabled = false;
            }

            float remaining = pullTime;
            while (ShouldPull() && wall != null && arm.joint.enabled && remaining > 0f &&
                   Vector2.Distance(BodyCenter, arm.anchor.position) > closeDistance)
            {
                arm.anchor.position = wall.TransformPoint(localHit);

                //stop reeling if this anchor opposes the current state direction
                if (target == null || Vector2.Dot(
                    AimDirection(),
                    (Vector2)arm.anchor.position - BodyCenter) < 0f)
                    break;

                arm.joint.connectedAnchor = arm.anchor.position;
                if (pullingArm == arm)
                    arm.joint.distance = Mathf.MoveTowards(
                        arm.joint.distance, closeDistance, pullSpeed * (state == State.Hunt ? huntSpeed : 1f) * Time.fixedDeltaTime);

                remaining -= Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        //stay attached between attempts so gravity cannot simply drop it
        arm.rope.enabled = false;
        arm.aimedWall = null;
    }

    private void Release(Arm arm)
    {
        //scene unloading may already have destroyed the separate grapple objects
        if (arm.joint != null) arm.joint.enabled = false;
        if (arm.rope != null) arm.rope.enabled = false;
        if (arm.vine != null && !arm.vine.IsBroken)
            Destroy(arm.vine.gameObject);
        arm.vine = null;
        arm.vineAnchor = null;
        arm.wall = null;
        arm.aimedWall = null;
        if (pullingArm == arm) pullingArm = null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HurtPlayer(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HurtPlayer(collision);
    }

    private void HurtPlayer(Collision2D collision)
    {
        if (state != State.Aggro || contactDamage <= 0f ||
            Time.time < nextContactHit) return;

        PlayerVitals2D player = collision.collider.GetComponentInParent<PlayerVitals2D>();
        if (player == null || player.IsDead) return;

        //space out contact hits even if player invincibility is set to zero
        float health = player.CurrentHealth;
        player.TakeDamage(contactDamage);
        if (player.CurrentHealth < health) NotifyAttack();
        nextContactHit = Time.time + contactInterval;
    }

    private void OnDisable()
    {
        if (arms == null) return;
        foreach (Arm arm in arms)
        {
            if (arm.routine != null) StopCoroutine(arm.routine);
            arm.routine = null;
            Release(arm);
            arm.restMade = false;
        }
    }

    private void OnDestroy()
    {
        if (arms != null)
        foreach (Arm arm in arms)
        {
            if (arm.anchor != null) Destroy(arm.anchor.gameObject);
            if (arm.joint != null) Destroy(arm.joint);
        }
        if (bodyPoint != null) Destroy(bodyPoint.gameObject);
        if (ropeMaterial != null) Destroy(ropeMaterial);
    }
}
