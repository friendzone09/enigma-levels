using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controlador 2D del personaje: caminar y saltar.
/// Usa el nuevo Input System (teclado y mando) con "coyote time" y
/// buffer de salto para que los controles se sientan tolerantes.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Caminar")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float groundAcceleration = 90f;
    [SerializeField] private float groundDeceleration = 120f;
    [SerializeField] private float airControl = 0.75f;

    [Header("Saltar")]
    [SerializeField] private float jumpVelocity = 13f;
    [SerializeField] private float fallGravityMultiplier = 1.55f;
    [SerializeField] private float lowJumpMultiplier = 2.4f;
    [SerializeField] private float maxFallSpeed = 22f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Seguridad")]
    [SerializeField] private float respawnIfBelowY = -6f;

    private Rigidbody2D body;
    private SpriteRenderer sprite;
    private float moveInput;
    private bool jumpHeld;
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;
    private Vector3 spawnPoint;

    public bool IsGrounded => Time.time - lastGroundedTime <= coyoteTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponentInChildren<SpriteRenderer>();

        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        spawnPoint = transform.position;
    }

    private void Update()
    {
        ReadInput();

        if (Time.time - lastJumpPressedTime <= jumpBufferTime && IsGrounded)
            Jump();

        if (sprite != null)
            sprite.flipX = moveInput < -0.01f;

        if (transform.position.y < respawnIfBelowY)
            Respawn();
    }

    private void FixedUpdate()
    {
        float targetSpeed = moveInput * moveSpeed;
        float rate = (Mathf.Abs(targetSpeed) > 0.01f ? groundAcceleration : groundDeceleration);
        if (!IsGrounded)
            rate *= airControl;

        float newX = Mathf.MoveTowards(body.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);

        float gravity = Physics2D.gravity.y * body.gravityScale;
        float velocityY = body.linearVelocity.y;

        if (velocityY > 0.01f && !jumpHeld)
            velocityY += gravity * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        else if (velocityY < 0f)
            velocityY += gravity * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;

        velocityY = Mathf.Max(velocityY, -maxFallSpeed);

        body.linearVelocity = new Vector2(newX, velocityY);
    }

    private void ReadInput()
    {
        float horizontal = 0f;
        jumpHeld = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                horizontal += 1f;

            jumpHeld = keyboard.spaceKey.isPressed;
            if (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.upArrowKey.wasPressedThisFrame ||
                keyboard.wKey.wasPressedThisFrame)
                lastJumpPressedTime = Time.time;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            float stick = pad.leftStick.ReadValue().x;
            if (Mathf.Abs(stick) > 0.2f)
                horizontal += stick;

            if (pad.buttonSouth.isPressed)
                jumpHeld = true;
            if (pad.buttonSouth.wasPressedThisFrame)
                lastJumpPressedTime = Time.time;
        }
#else
        horizontal = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetKey(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.W))
            lastJumpPressedTime = Time.time;
#endif

        moveInput = Mathf.Clamp(horizontal, -1f, 1f);
    }

    private void Jump()
    {
        lastJumpPressedTime = -999f;
        lastGroundedTime = -999f;
        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
    }

    private void Respawn()
    {
        body.linearVelocity = Vector2.zero;
        transform.position = spawnPoint;
    }

    private void OnCollisionEnter2D(Collision2D collision) => CheckGroundContact(collision);
    private void OnCollisionStay2D(Collision2D collision) => CheckGroundContact(collision);

    private void CheckGroundContact(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.55f)
            {
                lastGroundedTime = Time.time;
                return;
            }
        }
    }
}
