
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FarmerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(1f)] private float sprintMultiplier = 1.5f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpHeight = 2f;

    [Header("Ladder")]
    [SerializeField, Min(0f)] private float climbSpeed = 3f;

    [Header("Animation")]
    [SerializeField] private Animator farmerAnimator;
    [SerializeField, Min(0f)] private float walkAnimationSpeed = 1f;
    [SerializeField, Min(1f)] private float sprintAnimationMultiplier = 1.5f;

    private static readonly int FarmerWalkHash =
        Animator.StringToHash("FarmerWalk");

    private CharacterController characterController;
    private float verticalVelocity;

    private bool hasLocalControl;
    private bool jumpRequested;
    private bool jumpHeld;
    private bool isClimbing;

    private Vector2 moveInput;
    private bool sprintInput;

    private Ladder currentLadder;

    public bool IsClimbing => isClimbing;
    public float VerticalVelocity => verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (farmerAnimator == null)
            farmerAnimator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (!hasLocalControl)
            return;

        HandleMovement();
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void SetSprintInput(bool isSprinting)
    {
        sprintInput = isSprinting;
    }

    public void SetJumpHeld(bool isHeld)
    {
        jumpHeld = isHeld;
    }

    public void JumpPressed()
    {
        jumpRequested = true;
    }

    private void HandleMovement()
    {
        Vector2 input = moveInput;

        bool isMoving = input.sqrMagnitude > 0.01f;
        bool isSprinting = isMoving && sprintInput;

        // 사다리에서 점프하면 등반을 취소하고 점프한다.
        if (isClimbing && jumpRequested)
        {
            isClimbing = false;

            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);

            jumpRequested = false;
        }

        // 사다리 등반 중에는 중력과 일반 이동을 적용하지 않는다.
        if (isClimbing)
        {
            HandleClimbing(input);
            jumpRequested = false;
            return;
        }

        // 사다리 범위 안에서 W를 누르면 등반을 시작한다.
        if (currentLadder != null && input.y > 0.1f)
        {
            Debug.Log("[Farmer] Climb started");

            isClimbing = true;
            verticalVelocity = 0f;

            HandleClimbing(input);
            jumpRequested = false;
            return;
        }

        float speed = moveSpeed;

        if (isSprinting)
            speed *= sprintMultiplier;

        Vector3 moveDirection =
            transform.forward * input.y +
            transform.right * input.x;

        bool isGrounded = characterController.isGrounded;

        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jumpRequested && isGrounded)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        jumpRequested = false;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity =
            moveDirection * speed +
            Vector3.up * verticalVelocity;

        characterController.Move(
            velocity * Time.deltaTime
        );

        UpdateAnimation(isMoving, isSprinting);
    }

    private void HandleClimbing(Vector2 input)
    {
        if (currentLadder == null)
        {
            Debug.LogWarning("[Farmer] Ladder reference lost!");

            isClimbing = false;
            return;
        }

        float climbInput = input.y;

        Vector3 climbMovement =
            Vector3.up * climbInput * climbSpeed * Time.deltaTime;

        CollisionFlags flags =
            characterController.Move(climbMovement);

        Debug.Log(
            $"[Farmer] Climbing | Input: {climbInput:F1} | " +
            $"Y: {transform.position.y:F2} | " +
            $"Movement: {climbMovement.y:F3} | " +
            $"Flags: {flags}"
        );

        if (farmerAnimator != null)
        {
            farmerAnimator.SetBool(FarmerWalkHash, false);
            farmerAnimator.speed = walkAnimationSpeed;
        }
    }

    private void UpdateAnimation(bool isMoving, bool isSprinting)
    {
        if (farmerAnimator == null)
            return;

        farmerAnimator.SetBool(FarmerWalkHash, isMoving);

        farmerAnimator.speed = isSprinting
            ? walkAnimationSpeed * sprintAnimationMultiplier
            : walkAnimationSpeed;
    }

    private void OnTriggerEnter(Collider other)
    {
        Ladder ladder = other.GetComponentInParent<Ladder>();

        if (ladder != null)
        {
            currentLadder = ladder;
            Debug.Log($"[Farmer] Ladder detected: {ladder.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Ladder ladder = other.GetComponentInParent<Ladder>();

        if (ladder != null && ladder == currentLadder)
        {
            currentLadder = null;
            isClimbing = false;
        }
    }

    public void SetLocalControl(bool isLocalPlayer)
    {
        hasLocalControl = isLocalPlayer;

        if (!hasLocalControl)
        {
            moveInput = Vector2.zero;
            sprintInput = false;
            jumpRequested = false;
            jumpHeld = false;
            isClimbing = false;
            verticalVelocity = 0f;

            if (farmerAnimator != null)
            {
                farmerAnimator.SetBool(FarmerWalkHash, false);
                farmerAnimator.speed = walkAnimationSpeed;
            }
        }
    }
}
