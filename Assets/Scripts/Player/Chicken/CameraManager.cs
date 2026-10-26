
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraManager : MonoBehaviour
{
    public bool IsAiming { get; private set; }

    private bool isLocalControl;
    private PlayerControls controls;

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        if (isLocalControl)
            EnableLocalInput();
    }

    private void OnDisable()
    {
        DisableLocalInput();
    }

    public void SetLocalControl(bool isLocal)
    {
        isLocalControl = isLocal;

        if (!isActiveAndEnabled)
        {
            if (!isLocal)
                IsAiming = false;

            return;
        }

        if (isLocalControl)
        {
            EnableLocalInput();
        }
        else
        {
            DisableLocalInput();
        }
    }

    private void EnableLocalInput()
    {
        controls.Chicken.EggAim.started -= OnAimStarted;
        controls.Chicken.EggAim.canceled -= OnAimCanceled;

        controls.Chicken.EggAim.started += OnAimStarted;
        controls.Chicken.EggAim.canceled += OnAimCanceled;  

        controls.Enable();
    }

    private void DisableLocalInput()
    {
        if (controls != null)
        {
            controls.Chicken.EggAim.started -= OnAimStarted;
            controls.Chicken.EggAim.canceled -= OnAimCanceled;
            controls.Disable();
        }

        IsAiming = false;
    }

    private void OnAimStarted(InputAction.CallbackContext context)
    {
        if (isLocalControl)
            IsAiming = true;
    }

    private void OnAimCanceled(InputAction.CallbackContext context)
    {
        IsAiming = false;
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }
}
