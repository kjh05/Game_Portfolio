using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStatusPanelToggle : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject playerStatusPanel;

    [Header("Input")]
    [SerializeField] private InputActionReference playerStatusKey;

    private void Awake()
    {
        if (playerStatusPanel == null)
            playerStatusPanel = gameObject;
    }

    private void OnEnable()
    {
        if (playerStatusKey == null ||
            playerStatusKey.action == null)
        {
            Debug.LogError(
                "[PlayerStatusPanelToggle] PlayerStatusKey가 연결되지 않았습니다."
            );
            return;
        }

        playerStatusKey.action.performed += OnTogglePanel;
        playerStatusKey.action.Enable();
    }

    private void OnDisable()
    {
        if (playerStatusKey == null ||
            playerStatusKey.action == null)
            return;

        playerStatusKey.action.performed -= OnTogglePanel;
        playerStatusKey.action.Disable();
    }

    private void OnTogglePanel(
        InputAction.CallbackContext context)
    {
        if (playerStatusPanel == null)
            return;

        bool isActive = playerStatusPanel.activeSelf;
        playerStatusPanel.SetActive(!isActive);
    }
}