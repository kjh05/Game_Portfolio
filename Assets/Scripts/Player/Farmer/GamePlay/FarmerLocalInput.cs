
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class FarmerLocalInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FarmerMovement farmerMovement;
    [SerializeField] private FarmerLook farmerLook;
    [SerializeField] private PistolFire pistolFire;
    [SerializeField] private FarmerHeadVisibility headVisibility;
    [SerializeField] private Camera farmerCamera;
    [SerializeField] private AudioListener audioListener;

    private PlayerControls controls;
    private bool isLocalControl;

    public bool IsLocalPlayer => isLocalControl;

    [SerializeField] private Canvas farmerCanvas;

    private void Awake()
    {
        if (farmerMovement == null)
            farmerMovement = GetComponent<FarmerMovement>();

        if (farmerLook == null)
            farmerLook = GetComponentInChildren<FarmerLook>(true);

        if (pistolFire == null)
            pistolFire = GetComponentInChildren<PistolFire>(true);

        if (headVisibility == null)
            headVisibility = GetComponentInChildren<FarmerHeadVisibility>(true);

        if (farmerCamera == null)
            farmerCamera = GetComponentInChildren<Camera>(true);

        if (audioListener == null && farmerCamera != null)
            audioListener = farmerCamera.GetComponent<AudioListener>();
        
        if (farmerCanvas == null)
            farmerCanvas = GetComponentInChildren<Canvas>(true);

        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        if (controls == null)
            return;

        controls.Farmer.Move.performed += OnMove;
        controls.Farmer.Move.canceled += OnMove;

        controls.Farmer.Sprint.performed += OnSprint;
        controls.Farmer.Sprint.canceled += OnSprint;

        controls.Farmer.Jump.performed += OnJump;
        controls.Farmer.Jump.canceled += OnJumpReleased;

        controls.Farmer.Look.performed += OnLook;
        controls.Farmer.Look.canceled += OnLook;

        controls.Farmer.Fire.performed += OnFire;

        if (isLocalControl)
            controls.Farmer.Enable();
    }

    private void OnDisable()
    {
        if (controls != null)
        {
            controls.Farmer.Move.performed -= OnMove;
            controls.Farmer.Move.canceled -= OnMove;

            controls.Farmer.Sprint.performed -= OnSprint;
            controls.Farmer.Sprint.canceled -= OnSprint;

            controls.Farmer.Jump.performed -= OnJump;
            controls.Farmer.Jump.canceled -= OnJumpReleased;

            controls.Farmer.Look.performed -= OnLook;
            controls.Farmer.Look.canceled -= OnLook;

            controls.Farmer.Fire.performed -= OnFire;

            controls.Disable();
        }

        ClearInput();
    }

    public void Initialize(bool isLocalPlayer)
    {
        SetLocalPlayer(isLocalPlayer);
    }

    public void SetLocalPlayer(bool isLocalPlayer)
    {
        isLocalControl = isLocalPlayer;

        if (farmerMovement != null)
            farmerMovement.SetLocalControl(isLocalPlayer);

        if (farmerLook != null)
            farmerLook.SetLocalControl(isLocalPlayer);

        if (pistolFire != null)
            pistolFire.SetLocalControl(isLocalPlayer);

        if (headVisibility != null)
            headVisibility.SetLocalPlayer(isLocalPlayer);

        if (farmerCamera != null)
            farmerCamera.enabled = isLocalPlayer;

        if (audioListener != null)
            audioListener.enabled = isLocalPlayer;
        
        if (farmerCanvas != null)
            farmerCanvas.enabled = isLocalPlayer;

        if (controls != null)
        {
            if (isLocalPlayer && isActiveAndEnabled)
                controls.Farmer.Enable();
            else
                controls.Farmer.Disable();
        }

        if (isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            ClearInput();
        }
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        if (!isLocalControl || farmerMovement == null)
            return;

        farmerMovement.SetMoveInput(context.ReadValue<Vector2>());
    }

    private void OnSprint(InputAction.CallbackContext context)
    {
        if (!isLocalControl || farmerMovement == null)
            return;

        farmerMovement.SetSprintInput(context.ReadValueAsButton());
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (!isLocalControl || farmerMovement == null)
            return;

        farmerMovement.SetJumpHeld(true);
        farmerMovement.JumpPressed();
    }
    
    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        if (!isLocalControl || farmerMovement == null)
            return;

        farmerMovement.SetJumpHeld(false);
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        if (!isLocalControl || farmerLook == null)
            return;

        farmerLook.SetLookInput(context.ReadValue<Vector2>());
    }

    private void OnFire(InputAction.CallbackContext context)
    {
        if (!isLocalControl || pistolFire == null)
            return;

        pistolFire.Fire();
    }

    private void ClearInput()
    {
        if (farmerMovement != null)
        {
            farmerMovement.SetMoveInput(Vector2.zero);
            farmerMovement.SetSprintInput(false);
            farmerMovement.SetJumpHeld(false);
        }

        if (farmerLook != null)
            farmerLook.SetLookInput(Vector2.zero);
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }
}
