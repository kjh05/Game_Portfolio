
using UnityEngine;
using UnityEngine.InputSystem;

public class ChickenAnimation : MonoBehaviour
{
    private PlayerControls controls;
    private bool isLocalControl;
    private bool isPecking;

    private float wiggleTimer;
    private float wiggleTargetTime;

    private ChickenNetworkSync networkSync;

    public float ClimbAnimationSpeed { get; private set; } = 1f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private ChickenMovement chickenMovement;
    [SerializeField] private CameraManager cameraManager;

    [Header("Peck")]
    [SerializeField] private bool canPeck = false;
    [SerializeField] private float peckDuration = 0.48f;

    [Header("Wiggle")]
    [SerializeField] private float wiggleMinTime = 5f;
    [SerializeField] private float wiggleMaxTime = 9f;

    private void Awake()
    {
        controls = new PlayerControls();

        if (chickenMovement == null)
            chickenMovement = GetComponent<ChickenMovement>();

        if (cameraManager == null)
            cameraManager = GetComponentInChildren<CameraManager>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        networkSync = GetComponent<ChickenNetworkSync>();

        wiggleTargetTime = Random.Range(
            wiggleMinTime,
            wiggleMaxTime
        );
    }

    private void OnEnable()
    {
        if (isLocalControl)
            EnableLocalInput();
    }

    private void OnDisable()
    {
        DisableLocalInput();

        CancelInvoke(nameof(EndPeck));
        isPecking = false;

        if (animator != null)
            animator.speed = 1f;
    }

    public void SetLocalControl(bool isLocal)
    {
        isLocalControl = isLocal;

        if (!isActiveAndEnabled)
            return;

        if (isLocalControl)
        {
            EnableLocalInput();
        }
        else
        {
            DisableLocalInput();

            CancelInvoke(nameof(EndPeck));
            isPecking = false;

            if (animator != null)
                animator.speed = 1f;
        }
    }

    private void EnableLocalInput()
    {
        if (controls == null)
            return;

        controls.Chicken.Peck.performed -= OnPeck;
        controls.Chicken.Peck.performed += OnPeck;

        controls.Enable();
    }

    private void DisableLocalInput()
    {
        if (controls == null)
            return;

        controls.Chicken.Peck.performed -= OnPeck;
        controls.Disable();
    }

    public void UpdateAnimation()
    {
        if (!isLocalControl)
            return;

        if (animator == null || chickenMovement == null)
            return;

        float speed = 0f;

        if (chickenMovement.IsMoving)
            speed = chickenMovement.IsSprinting ? 1f : 0.5f;

        animator.SetFloat("Speed", speed);
        animator.SetBool("IsGrounded", chickenMovement.IsGrounded);
        animator.SetFloat(
            "VerticalVelocity",
            chickenMovement.VerticalVelocity
        );
        animator.SetBool("IsClimbing", chickenMovement.IsClimbing);

        bool aiming =
            cameraManager != null &&
            cameraManager.IsAiming;

        animator.SetBool("IsAiming", aiming);

        UpdateWiggle(aiming);
    }

    public void ApplyNetworkAnimationState(
        float speed,
        bool grounded,
        float verticalVelocity,
        bool climbing,
        bool aiming)
    {
        if (isLocalControl || animator == null)
            return;

        animator.SetFloat("Speed", speed);
        animator.SetBool("IsGrounded", grounded);
        animator.SetFloat("VerticalVelocity", verticalVelocity);
        animator.SetBool("IsClimbing", climbing);
        animator.SetBool("IsAiming", aiming);
    }

    private void UpdateWiggle(bool aiming)
    {
        if (!aiming)
        {
            wiggleTimer = 0f;
            return;
        }

        wiggleTimer += Time.deltaTime;

        if (wiggleTimer >= wiggleTargetTime)
        {
            if (animator != null)
                animator.SetTrigger("Wiggle");

            if (networkSync != null)
                networkSync.SendAnimationEvent("Wiggle");

            wiggleTimer = 0f;

            wiggleTargetTime = Random.Range(
                wiggleMinTime,
                wiggleMaxTime
            );
        }
    }

    private bool CanPeck()
    {
        if (!isLocalControl || !canPeck || isPecking)
            return false;

        if (chickenMovement == null)
            return false;

        if (!chickenMovement.IsGrounded)
            return false;

        if (chickenMovement.IsSprinting)
            return false;

        if (chickenMovement.IsClimbing)
            return false;

        return true;
    }

    private void OnPeck(InputAction.CallbackContext context)
    {
        if (!CanPeck())
            return;

        isPecking = true;

        if (animator != null)
            animator.SetTrigger("Peck");

        if (networkSync != null)
            networkSync.SendAnimationEvent("Peck");

        CancelInvoke(nameof(EndPeck));
        Invoke(nameof(EndPeck), peckDuration);
    }

    public void ApplyNetworkTrigger(string triggerName)
    {
        if (isLocalControl || animator == null)
            return;

        if (triggerName != "Peck" && triggerName != "Wiggle")
            return;

        animator.SetTrigger(triggerName);
    }

    public void SetClimbAnimationSpeed(float speed)
    {
        ClimbAnimationSpeed = speed;

        if (animator != null)
            animator.speed = speed;
    }

    public void ApplyNetworkClimbAnimationSpeed(float speed)
    {
        if (isLocalControl || animator == null)
            return;

        animator.speed = speed;
    }

    private void EndPeck()
    {
        isPecking = false;
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }
}
