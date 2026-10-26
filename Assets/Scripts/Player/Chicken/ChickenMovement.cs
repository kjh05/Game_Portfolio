
using UnityEngine;

public class ChickenMovement : MonoBehaviour
{
    private CharacterController controller;
    private Vector2 moveInput;

    private bool isLocalControl;
    private bool isSprinting;
    private bool isGrounded;
    private bool isJumpHeld;
    private bool isAiming;

    private float verticalVelocity;

    private bool isClimbing;
    private Ladder currentLadder;

    [Header("Movement")]
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float aimMoveSpeed = 1f;

    [Header("Camera Manager")]
    [SerializeField] private CameraManager cameraManager;

    [Header("Rotation")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform chickenModel;
    [SerializeField] private Transform aimCamera;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float aimJumpHeight = 0.8f;
    [SerializeField] private float glideGravityMultiplier = 0.5f;
    [SerializeField] private bool canGlide = false;

    [Header("Animation")]
    [SerializeField] private ChickenAnimation chickenAnimation;

    public bool IsLocalControl => isLocalControl;
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    public bool IsClimbing => isClimbing;
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f;
    public float VerticalVelocity => verticalVelocity;
    public bool IsAiming => isAiming;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraManager == null)
            cameraManager = GetComponentInChildren<CameraManager>();

        if (chickenAnimation == null)
            chickenAnimation = GetComponent<ChickenAnimation>();
    }

    private void Update()
    {
        if (isLocalControl)
        {
            GroundCheck();

            if (isClimbing)
            {
                Climb();
            }
            else
            {
                Gravity();
                Move();
            }
        }

        // 애니메이션 갱신은 이동 처리와 분리한다.
        // 네트워크 동기화 전까지 원격 닭은 기본 상태로 표시된다.
        if (chickenAnimation != null)
            chickenAnimation.UpdateAnimation();
    }
    
    private void LateUpdate()
    {
        if (!isLocalControl || !isAiming)
            return;

        if (chickenModel == null || cameraPivot == null)
            return;

        Vector3 direction = cameraPivot.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        // 모델 정면 보정: 기존 -90도 유지
        Quaternion targetRotation =
            Quaternion.LookRotation(direction) *
            Quaternion.Euler(0f, -90f, 0f);

        chickenModel.rotation = targetRotation;
    }
    
    public void SetLocalControl(bool isLocal)
    {
        isLocalControl = isLocal;

        if (!isLocal)
        {
            moveInput = Vector2.zero;
            isSprinting = false;
            isJumpHeld = false;
            isAiming = false;
        }
    }

    public void SetMoveInput(Vector2 input)
    {
        if (!isLocalControl)
            return;

        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void SetSprintInput(bool sprinting)
    {
        if (!isLocalControl)
            return;

        isSprinting = sprinting;
    }

    public void SetAimingInput(bool aiming)
    {
        if (!isLocalControl)
            return;

        isAiming = aiming;
    }

    public void SetJumpHeld(bool held)
    {
        if (!isLocalControl)
            return;

        isJumpHeld = held;
    }

    public void JumpPressed()
    {
        if (!isLocalControl)
            return;

        if (isClimbing)
        {
            isClimbing = false;

            if (chickenAnimation != null)
                chickenAnimation.SetClimbAnimationSpeed(1f);

            float currentJumpHeight =
                isAiming ? aimJumpHeight : jumpHeight;

            verticalVelocity =
                Mathf.Sqrt(currentJumpHeight * -2f * gravity);

            return;
        }

        if (!isGrounded)
            return;

        float jumpHeightToUse =
            isAiming ? aimJumpHeight : jumpHeight;

        verticalVelocity =
            Mathf.Sqrt(jumpHeightToUse * -2f * gravity);
    }

    private void Move()
    {
        if (cameraPivot == null || controller == null)
            return;

        Vector3 forward = cameraPivot.forward;
        Vector3 right = cameraPivot.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            forward * moveInput.y +
            right * moveInput.x;

        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();

            if (!isAiming && chickenModel != null)
            {
                // 모델 정면 보정: 기존 +90도 유지
                Quaternion targetRotation =
                    Quaternion.LookRotation(direction) *
                    Quaternion.Euler(0f, 90f, 0f);

                chickenModel.rotation = Quaternion.Slerp(
                    chickenModel.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        if (!isAiming &&
            currentLadder != null &&
            moveInput.y > 0.1f &&
            !isClimbing)
        {
            StartClimb();
        }

        float speed;

        if (isAiming)
        {
            speed = aimMoveSpeed;
        }
        else
        {
            bool canSprint = isGrounded && isSprinting;
            speed = canSprint ? sprintSpeed : walkSpeed;
        }

        Vector3 move = direction * speed;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    private void StartClimb()
    {
        isClimbing = true;
        verticalVelocity = 0f;
    }

    private void Climb()
    {
        if (currentLadder == null)
        {
            isClimbing = false;

            if (chickenAnimation != null)
                chickenAnimation.SetClimbAnimationSpeed(1f);

            return;
        }

        if (Mathf.Abs(moveInput.y) < 0.01f)
        {
            if (chickenAnimation != null)
                chickenAnimation.SetClimbAnimationSpeed(0f);

            return;
        }

        if (chickenAnimation != null)
            chickenAnimation.SetClimbAnimationSpeed(1f);

        Vector3 move = Vector3.up * moveInput.y * climbSpeed;
        controller.Move(move * Time.deltaTime);
    }

    private void GroundCheck()
    {
        if (controller != null)
            isGrounded = controller.isGrounded;
    }

    private void Gravity()
    {
        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        float currentGravity = gravity;

        if (canGlide && verticalVelocity < 0f && isJumpHeld)
        {
            currentGravity *= glideGravityMultiplier;
            verticalVelocity = Mathf.Max(verticalVelocity, -3f);
        }

        verticalVelocity += currentGravity * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        Ladder ladder = other.GetComponent<Ladder>();

        if (ladder != null)
            currentLadder = ladder;
    }

    private void OnTriggerExit(Collider other)
    {
        Ladder ladder = other.GetComponent<Ladder>();

        if (ladder == currentLadder)
            currentLadder = null;
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        // 순간이동 도중 CharacterController의 충돌 처리를 잠시 중단한다.
        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        // 이전 이동 상태가 순간이동 후에도 이어지지 않게 초기화한다.
        verticalVelocity = 0f;
        isGrounded = false;
        moveInput = Vector2.zero;
        isSprinting = false;
        isJumpHeld = false;

        if (controller != null)
            controller.enabled = true;

        Physics.SyncTransforms();
    }
}
