using UnityEngine;

public class PlayerController : MonoBehaviour, IFollowable
{
    public Vector2 inputDir;
    public float FeetHeight => baseFeetHeight + inputVerticalLean * verticalLeanHeight;
    public float GroundedHeight => FeetHeight + groundedSpacing;
    public Vector2 RightDir => new Vector2(UpDir.y, -UpDir.x);
    public Rigidbody2D RB => characterRB;
    public Transform Transform => characterRB.transform;
    public World ClosestWorld { get; private set; }
    public Vector2 GroundPosition { get; private set; }
    public Vector2 GroundDir { get; private set; }
    public Vector2 UpDir { get; private set; }
    public Vector2 TargetPosition { get; private set; }
    public bool IsGrounded { get; private set; }
    public float MovementSlowdown { get; set; }
    public float OverrideLean { get; set; }

    public Transform GetFollowTransform() => characterRB.transform;

    public Vector2 GetFollowPosition() => characterRB.position;

    public Vector2 GetFollowUpwards() => UpDir;

    public Vector2 GetJumpDir()
    {
        Vector2 jumpDir = UpDir;

        if (characterRB.velocity.magnitude > verticalJumpThreshold)
        {
            float rightComponent = Vector2.Dot(characterRB.velocity.normalized, RightDir);
            jumpDir += RightDir * Mathf.Sign(rightComponent);
        }

        return jumpDir.normalized;
    }

    [Header("References")]
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private WorldGenerator world;
    [SerializeField] private GravityObject characterGravity;
    [SerializeField] private Rigidbody2D characterRB;

    [Header("Config")]
    [SerializeField] private float rotationSpeed = 4.0f;
    [SerializeField] private float groundedSpacing = 0.3f;
    [SerializeField] private float feetRaiseStrength = 1.0f;
    [SerializeField] private float feetLowerStrength = 0.1f;
    [SerializeField] private float verticalLeanHeight = 0.2f;
    [SerializeField] private float baseFeetHeight = 1.0f;
    [Space(10)]
    [SerializeField] private float airDrag = 1.0f;
    [SerializeField] private float airAngularDrag = 1.0f;
    [SerializeField] private float airMovementSpeed = 0.5f;
    [Space(10)]
    [SerializeField] private float groundDrag = 8.0f;
    [SerializeField] private float groundAngularDrag = 10.0f;
    [SerializeField] private float groundMovementSpeed = 2.0f;
    [Space(10)]
    [SerializeField] private float jumpForce = 15.0f;
    [SerializeField] private float jumpTimerMax = 0.5f;
    [SerializeField] private float verticalJumpThreshold = 0.1f;
    [Space(10)]
    [SerializeField] private bool drawGizmos = false;
    private bool inputJump;
    private float inputVerticalLean;
    private float jumpTimer = 0.0f;

    private void Start()
    {
        ClosestWorld = World.GetClosestWorld(characterRB.transform.position, out Vector2 closestGroundPosition);
        playerCamera.SetModeFollow(this, true);
    }

    private void Update()
    {
        if (GameManager.IsPaused) return;
        HandleInput();
    }

    private void HandleInput()
    {
        // Take in input for movement
        inputDir = Vector3.zero;
        inputDir += Input.GetAxisRaw("Horizontal") * RightDir;

        // Vertical movement while not grounded
        if (!IsGrounded) inputDir += Input.GetAxisRaw("Horizontal") * RightDir;

        // Take in input for jump
        inputJump = Input.GetKey(KeyCode.Space);

        // Take in input for leaning
        inputVerticalLean = 0.0f;
        if (Input.GetAxisRaw("Vertical") != 0) inputVerticalLean = (int)Mathf.Sign(Input.GetAxisRaw("Vertical"));
        if (OverrideLean != 0.0f) inputVerticalLean = -OverrideLean;
    }

    private void FixedUpdate()
    {
        if (GameManager.IsPaused) return;

        // Calculate closest world and ground position
        ClosestWorld = World.GetClosestWorld(characterRB.transform.position, out Vector2 closestGroundPosition);
        GroundPosition = closestGroundPosition;

        // Calculate ground positions
        GroundDir = GroundPosition - (Vector2)characterRB.transform.position;
        IsGrounded = GroundDir.magnitude < GroundedHeight;
        UpDir = GroundDir.normalized * -1;
        FixedUpdateMovement();
    }

    private void FixedUpdateMovement()
    {
        //  Rotate upwards
        Vector2 rotateTo = IsGrounded ? UpDir : RB.velocity.normalized;
        float angleDiff = Vector2.SignedAngle(characterRB.transform.up, rotateTo) % 360;
        characterRB.AddTorque(angleDiff * rotationSpeed * Mathf.Deg2Rad);

        // - While grounded
        if (IsGrounded)
        {
            characterRB.drag = groundDrag;
            characterRB.angularDrag = groundAngularDrag;
            characterGravity.IsEnabled = false;

            // Force to correct height with legs
            TargetPosition = GroundPosition + (UpDir * FeetHeight);
            Vector2 dir = TargetPosition - (Vector2)characterRB.transform.position;
            float upComponent = Vector2.Dot(UpDir, dir);
            if (upComponent > 0) characterRB.AddForce(dir * feetRaiseStrength, ForceMode2D.Impulse);
            else characterRB.AddForce(dir * feetLowerStrength, ForceMode2D.Impulse);

            // Force with input
            float speed = groundMovementSpeed * (1.0f - MovementSlowdown);
            characterRB.AddForce(inputDir.normalized * speed, ForceMode2D.Impulse);

            // Jump force if inputting and can
            if (inputJump && jumpTimer == 0.0f)
            {
                Vector2 jumpDir = GetJumpDir();
                characterRB.AddForce(jumpDir * jumpForce, ForceMode2D.Impulse);
                jumpTimer = jumpTimerMax;
            }

            // Update jump timer
            jumpTimer = Mathf.Max(jumpTimer - Time.deltaTime, 0.0f);
        }

        // - While in the air
        else
        {
            characterRB.drag = airDrag;
            characterRB.angularDrag = airAngularDrag;
            characterGravity.IsEnabled = true;

            // Reset jump timer to max
            jumpTimer = jumpTimerMax;
        }

        // Force with input
        characterRB.AddForce(inputDir.normalized * airMovementSpeed, ForceMode2D.Impulse);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Draw upwards
        //if (upDir != Vector2.zero)
        //{
        //    if (jumpTimer == 0.0f) Gizmos.color = Color.green;
        //    else Gizmos.color = Color.white;
        //    Gizmos.DrawLine(characterRB.transform.position, (Vector2)characterRB.transform.position + upDir);
        //}

        //// Draw to ground
        //if (groundPosition != Vector2.zero)
        //{
        //    Gizmos.color = Color.blue;
        //    Gizmos.DrawLine(characterRB.transform.position, groundPosition);
        //}

        // Draw to uncontrolled
        if (GroundPosition != Vector2.zero)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(GroundPosition + UpDir * GroundedHeight, 0.025f);
        }

        // Draw to target
        if (TargetPosition != Vector2.zero && IsGrounded)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(characterRB.transform.position, TargetPosition);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(TargetPosition, 0.025f);
        }
    }
}
