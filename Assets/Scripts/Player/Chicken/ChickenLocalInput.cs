
using UnityEngine;
using UnityEngine.InputSystem;

public class ChickenLocalInput : MonoBehaviour
{
    [SerializeField] private ChickenMovement chickenMovement;
    [SerializeField] private CameraManager cameraManager;

    private PlayerControls controls;
    private bool isLocalControl;

    private void Awake()
    {
        if (chickenMovement == null)
            chickenMovement = GetComponent<ChickenMovement>();

        if (cameraManager == null)
            cameraManager = GetComponentInChildren<CameraManager>();

        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Chicken.Move.performed += OnMove;
        controls.Chicken.Move.canceled += OnMove;

        controls.Chicken.Sprint.performed += OnSprint;
        controls.Chicken.Sprint.canceled += OnSprint;

        controls.Chicken.Jump.performed += OnJump;
        controls.Chicken.Jump.canceled += OnJumpReleased;

        if (isLocalControl)
            controls.Enable();
    }

    private void OnDisable()
    {
        controls.Chicken.Move.performed -= OnMove;
        controls.Chicken.Move.canceled -= OnMove;

        controls.Chicken.Sprint.performed -= OnSprint;
        controls.Chicken.Sprint.canceled -= OnSprint;

        controls.Chicken.Jump.performed -= OnJump;
        controls.Chicken.Jump.canceled -= OnJumpReleased;

        controls.Disable();
        ClearInput();
    }

    public void SetLocalControl(bool isLocal)
    {
        isLocalControl = isLocal;

        if (controls == null)
            return;

        if (isLocalControl && isActiveAndEnabled)
        {
            controls.Enable();
        }
        else
        {
            controls.Disable();
            ClearInput();
        }
    }

    private void Update()
    {
        if (!isLocalControl || chickenMovement == null)
            return;

        chickenMovement.SetAimingInput(
            cameraManager != null && cameraManager.IsAiming
        );
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        if (!isLocalControl || chickenMovement == null)
            return;

        chickenMovement.SetMoveInput(
            context.ReadValue<Vector2>()
        );
    }

    private void OnSprint(InputAction.CallbackContext context)
    {
        if (!isLocalControl || chickenMovement == null)
            return;

        chickenMovement.SetSprintInput(
            context.ReadValueAsButton()
        );
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (!isLocalControl || chickenMovement == null)
            return;

        chickenMovement.SetJumpHeld(true);
        chickenMovement.JumpPressed();
    }

    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        if (!isLocalControl || chickenMovement == null)
            return;

        chickenMovement.SetJumpHeld(false);
    }

    private void ClearInput()
    {
        if (chickenMovement == null)
            return;

        // 로컬 입력을 비활성화하기 전에 이동 상태를 정리한다.
        chickenMovement.SetMoveInput(Vector2.zero);
        chickenMovement.SetSprintInput(false);
        chickenMovement.SetJumpHeld(false);
        chickenMovement.SetAimingInput(false);
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }
}
