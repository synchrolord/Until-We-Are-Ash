using UnityEngine;
using UnityEngine.InputSystem;

//a quick way to move around the level while testing
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerMotor2D))]
public sealed class PlayerCheats2D : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float flySpeed = 16f;

    private Rigidbody2D body;
    private PlayerMotor2D motor;
    private PlayerVitals2D vitals;
    private Collider2D[] colliders;
    private bool[] colliderStates;
    private RigidbodyType2D normalBodyType;
    private float normalGravity;
    private bool motorWasEnabled;
    private bool wasIgnoringDamage;
    private bool flying;
    public bool IsFlying => flying;
    private Vector2 direction;
    private bool fast;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        motor = GetComponent<PlayerMotor2D>();
        vitals = GetComponent<PlayerVitals2D>();
    }

    private void Update()
    {
        if (vitals != null && vitals.IsDead)
        {
            SetFlying(false);
            return;
        }
        Keyboard keys = Keyboard.current;
        direction = Vector2.zero;
        if (keys == null || Time.timeScale == 0f) return;
        if (!flying) return;

        if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) direction.x--;
        if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) direction.x++;
        if (keys.sKey.isPressed || keys.downArrowKey.isPressed) direction.y--;
        if (keys.wKey.isPressed || keys.upArrowKey.isPressed) direction.y++;
        direction = Vector2.ClampMagnitude(direction, 1f);
        fast = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
    }

    private void FixedUpdate()
    {
        if (!flying) return;
        body.MovePosition(body.position + direction * flySpeed * (fast ? 2f : 1f) * Time.fixedDeltaTime);
    }

    public void SetFlying(bool value)
    {
        if (flying == value) return;
        flying = value;
        direction = Vector2.zero;
        if (flying)
        {
            //save the actual settings so disabled colliders stay disabled afterward
            if (vitals != null)
            {
                //poison and fire do not need a collider to damage the player
                wasIgnoringDamage = vitals.IgnoreDamage;
                vitals.IgnoreDamage = true;
            }
            normalGravity = body.gravityScale;
            normalBodyType = body.bodyType;
            motorWasEnabled = motor.enabled;
            colliders = GetComponentsInChildren<Collider2D>(true);
            colliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderStates[i] = colliders[i].enabled;
                if (colliders[i].attachedRigidbody == body) colliders[i].enabled = false;
            }
            motor.ResetJumps();
            motor.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }
        else
        {
            if (vitals != null) vitals.IgnoreDamage = wasIgnoringDamage;
            //also runs when this component is disabled or the player dies
            if (body != null)
            {
                body.bodyType = normalBodyType;
                body.gravityScale = normalGravity;
                body.linearVelocity = Vector2.zero;
            }
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
            if (motor != null)
            {
                motor.ResetJumps();
                motor.enabled = motorWasEnabled;
            }
        }
    }

    private void OnDisable() => SetFlying(false);
}
