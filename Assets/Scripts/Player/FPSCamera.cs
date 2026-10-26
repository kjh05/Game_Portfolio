using UnityEngine;
using UnityEngine.InputSystem;

public class FPSCamera : MonoBehaviour
{
    private PlayerControls controls;

    private Vector2 lookInput;

    [Header("Sensitivity")]
    [SerializeField] private float horizontalSensitivity = 0.1f;
    [SerializeField] private float verticalSensitivity = 0.1f;

    [Header("Vertical Look")]
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float minPitch = -80f;

    [Header("Yaw")]
    [SerializeField] private Transform yawPivot;

    [Header("Pitch")]
    [SerializeField] private Transform pitchPivot;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Enable();

        controls.Chicken.Look.performed += OnLook;
        controls.Chicken.Look.canceled += OnLook;

        InitializeRotation();
    }

    private void OnDisable()
    {
        controls.Chicken.Look.performed -= OnLook;
        controls.Chicken.Look.canceled -= OnLook;

        controls.Disable();

        lookInput = Vector2.zero;
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    private void InitializeRotation()
    {
        if (yawPivot != null)
        {
            yaw = NormalizeAngle(yawPivot.localEulerAngles.y);
        }

        if (pitchPivot != null)
        {
            pitch = NormalizeAngle(pitchPivot.localEulerAngles.x);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    // CameraManager가 조준 시작 시 호출
    public void SetAimStartRotation(float initialYaw)
    {
        yaw = initialYaw;
        pitch = 0f;

        if (yawPivot != null)
        {
            yawPivot.localRotation =
                Quaternion.Euler(0f, yaw, 0f);
        }

        if (pitchPivot != null)
        {
            pitchPivot.localRotation =
                Quaternion.identity;
        }
    }

    private void Update()
    {
        if (yawPivot == null || pitchPivot == null)
            return;

        yaw += lookInput.x * horizontalSensitivity;
        pitch -= lookInput.y * verticalSensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        yawPivot.localRotation =
            Quaternion.Euler(0f, yaw, 0f);

        pitchPivot.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}