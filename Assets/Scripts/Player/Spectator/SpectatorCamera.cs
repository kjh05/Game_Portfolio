
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SpectatorCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform spectatorView;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 8f;
    [SerializeField, Min(0f)] private float sprintMultiplier = 2f;

    [Header("Look")]
    [SerializeField, Min(0f)] private float sensitivity = 0.1f;
    [SerializeField] private float minPitch = -89f;
    [SerializeField] private float maxPitch = 89f;

    [Header("Collision")]
    [SerializeField, Min(0.01f)] private float controllerHeight = 1f;
    [SerializeField, Min(0.01f)] private float controllerRadius = 0.25f;

    private CharacterController characterController;

    private InputActionMap spectatorMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;

    private float pitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        characterController.height = controllerHeight;
        characterController.radius = controllerRadius;
        characterController.center = Vector3.zero;
        characterController.minMoveDistance = 0f;

        if (inputActions == null)
        {
            Debug.LogError(
                "[SpectatorCamera] Input Actions Asset이 연결되지 않았습니다.",
                this
            );
            enabled = false;
            return;
        }

        spectatorMap = inputActions.FindActionMap("Spectator", true);

        moveAction = spectatorMap.FindAction("Move", true);
        lookAction = spectatorMap.FindAction("Look", true);
        sprintAction = spectatorMap.FindAction("Sprint", true);
    }

    private void OnEnable()
    {
        if (spectatorMap == null)
            return;

        spectatorMap.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (spectatorMap != null)
            spectatorMap.Disable();
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        Vector2 mouseDelta = lookAction.ReadValue<Vector2>();

        transform.Rotate(
            Vector3.up,
            mouseDelta.x * sensitivity,
            Space.Self
        );

        pitch -= mouseDelta.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (spectatorView != null)
        {
            spectatorView.localRotation =
                Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        if (spectatorView == null)
            return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 moveDirection =
            spectatorView.forward * input.y +
            transform.right * input.x;

        float speed = moveSpeed;

        if (sprintAction.IsPressed())
            speed *= sprintMultiplier;

        characterController.Move(
            moveDirection * speed * Time.deltaTime
        );
    }
}
