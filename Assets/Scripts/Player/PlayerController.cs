
using UnityEngine;


public class PlayerController : MonoBehaviour, IFollowable
{
    [Header("References")]
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private WorldManager world;
    [SerializeField] private GravityObject characterGravity;
    [SerializeField] private Rigidbody2D characterRB;

    [Header("Config")]
    [SerializeField] private float rotationSpeed = 4.0f;
    [SerializeField] private float _groundedHeight = 1.3f;
    [SerializeField] private float _feetHeight = 1.0f;
    [SerializeField] private float feetRaiseStrength = 1.0f;
    [SerializeField] private float feetLowerStrength = 0.1f;
    [SerializeField] private float airDrag = 1.0f;
    [SerializeField] private float airAngularDrag = 1.0f;
    [SerializeField] private float airMovementSpeed = 0.5f;
    [SerializeField] private float groundDrag = 8.0f;
    [SerializeField] private float groundAngularDrag = 10.0f;
    [SerializeField] private float groundMovementSpeed = 2.0f;
    [SerializeField] private float jumpForce = 15.0f;
    [SerializeField] private float jumpTimerMax = 0.5f;
    [SerializeField] private float verticalJumpThreshold = 0.1f;
    [SerializeField] private bool drawGizmos = false;


    public Vector2 groundPosition { get; private set; }
    public Vector2 groundDir { get; private set; }
    public Vector2 upDir { get; private set; }
    public Vector2 targetPosition { get; private set; }
    public bool isGrounded { get; private set; }

    public float groundedHeight => _groundedHeight;
    public float feetHeight => _feetHeight;
    public Vector2 rightDir => new Vector2(upDir.y, -upDir.x);
    public Rigidbody2D rb => characterRB;
    public new Transform transform => characterRB.transform;

    private Vector2 inputDir;
    private bool inputJump;
    private float jumpTimer = 0.0f;


    private void Start()
    {
        playerCamera.SetModeFollow(this, true);
    }


    private void Update()
    {
        // Take in input
        inputDir = Vector3.zero;
        inputDir += Input.GetAxisRaw("Horizontal") * rightDir;
        
        // Vertical movement while not grounded
        if (!isGrounded) inputDir += Input.GetAxisRaw("Horizontal") * rightDir;

        // Update input jump
        inputJump = Input.GetKey(KeyCode.Space);
    }

    private void FixedUpdate()
    {
        // Calculate ground positions
        groundPosition = world.GetClosestOverallPoint(characterRB.transform.position);
        groundDir = groundPosition - (Vector2)characterRB.transform.position;
        isGrounded = groundDir.magnitude < groundedHeight;
        upDir = groundDir.normalized * -1;
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        //  Rotate upwards
        float angleDiff = (Vector2.SignedAngle(characterRB.transform.up, upDir) - 0) % 360 + 0;
        characterRB.AddTorque(angleDiff * rotationSpeed * Mathf.Deg2Rad);
        

        // - While grounded
        if (isGrounded)
        {
            characterRB.drag = groundDrag;
            characterRB.angularDrag = groundAngularDrag;
            characterGravity.isEnabled = false;

            // Force upwards with legs
            targetPosition = groundPosition + (upDir * feetHeight);
            Vector2 dir = targetPosition - (Vector2)characterRB.transform.position;
            float upComponent = Vector2.Dot(upDir, dir);

            if (upComponent > 0) characterRB.AddForce(dir * feetRaiseStrength, ForceMode2D.Impulse);
            else characterRB.AddForce(dir * feetLowerStrength, ForceMode2D.Impulse);
            
            // Force with input
            characterRB.AddForce(inputDir.normalized * groundMovementSpeed, ForceMode2D.Impulse);

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
            characterGravity.isEnabled = true;

            // Reset jump timer to max
            jumpTimer = jumpTimerMax;
        }


        // Force with input
        characterRB.AddForce(inputDir.normalized * airMovementSpeed, ForceMode2D.Impulse);
    }


    public Transform GetFollowTransform() => characterRB.transform;

    public Vector2 GetFollowUpwards() => upDir;

    public Vector2  GetJumpDir()
    {
        Vector2 jumpDir = upDir;

        if (characterRB.velocity.magnitude > verticalJumpThreshold)
        {
            float rightComponent = Vector2.Dot(characterRB.velocity.normalized, rightDir);
            jumpDir += rightDir * Mathf.Sign(rightComponent);
        }

        return jumpDir.normalized;
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
        if (groundPosition != Vector2.zero)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(groundPosition + upDir * groundedHeight, 0.025f);
        }

        // Draw to target
        if (targetPosition != Vector2.zero && isGrounded)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(characterRB.transform.position, targetPosition);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(targetPosition, 0.025f);
        }
    }
}
