
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    private PlayerControls controls;
    private Vector2 lookInput;
    private bool isLocalControl;

    [Header("Camera")]
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private CameraManager cameraManager;

    private float pitch;
    private float yaw;

    [Header("Aim Pitch")]
    [SerializeField] private float aimMinPitch = -45f;
    [SerializeField] private float aimMaxPitch = 45f;

    [Header("Camera Transform")]
    [SerializeField] private Transform cameraTransform;

    [Header("Zoom")]
    [SerializeField] private float zoomSensitivity = 0.02f;
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float zoomSmoothSpeed = 12f;
    [SerializeField] private float maxDistance = 8f;

    [Header("Aim Zoom")]
    [SerializeField] private float aimDistance = 3f;

    [Header("Camera Collision")]
    [SerializeField] private LayerMask cameraCollisionMask;
    [SerializeField] private float cameraCollisionRadius = 0.2f;
    [SerializeField] private float cameraCollisionOffset = 0.1f;

    private float currentDistance = 6f;
    private float targetDistance = 6f;

    private void Awake()
    {
        controls = new PlayerControls();

        if (cameraManager == null)
            cameraManager = GetComponent<CameraManager>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
                lookInput = Vector2.zero;

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
        controls.Chicken.Look.performed -= OnLook;
        controls.Chicken.Look.canceled -= OnLook;

        controls.Chicken.Look.performed += OnLook;
        controls.Chicken.Look.canceled += OnLook;

        controls.Enable();
    }

    private void DisableLocalInput()
    {
        if (controls != null)
        {
            controls.Chicken.Look.performed -= OnLook;
            controls.Chicken.Look.canceled -= OnLook;
            controls.Disable();
        }

        lookInput = Vector2.zero;
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        if (!isLocalControl)
            return;

        lookInput = context.ReadValue<Vector2>();
    }

    private void Update()
    {
        if (!isLocalControl)
            return;

        // Yaw
        yaw += lookInput.x * sensitivity;

        // Pitch
        pitch -= lookInput.y * sensitivity;

        if (cameraManager != null && cameraManager.IsAiming)
        {
            pitch = Mathf.Clamp(pitch, aimMinPitch, aimMaxPitch);
        }
        else
        {
            pitch = Mathf.Clamp(pitch, -80f, 80f);
        }

        // Camera rotation
        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);

        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Zoom
        if (cameraManager != null && cameraManager.IsAiming)
        {
            targetDistance = aimDistance;
        }
        else if (Mouse.current != null)
        {
            Vector2 scroll = Mouse.current.scroll.ReadValue();

            targetDistance -= scroll.y * zoomSensitivity;

            targetDistance = Mathf.Clamp(
                targetDistance,
                minDistance,
                maxDistance
            );
        }

        currentDistance = Mathf.Lerp(
            currentDistance,
            targetDistance,
            zoomSmoothSpeed * Time.deltaTime
        );

        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        if (cameraTransform == null)
            return;

        Vector3 origin = transform.position;

        Vector3 desiredPosition =
            transform.TransformPoint(
                new Vector3(0f, 0f, -currentDistance)
            );

        Vector3 direction = desiredPosition - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return;

        direction.Normalize();

        if (Physics.SphereCast(
            origin,
            cameraCollisionRadius,
            direction,
            out RaycastHit hit,
            distance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore))
        {
            float safeDistance =
                Mathf.Max(0f, hit.distance - cameraCollisionOffset);

            cameraTransform.position =
                origin + direction * safeDistance;
        }
        else
        {
            cameraTransform.position = desiredPosition;
        }
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }
}
