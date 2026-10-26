using UnityEngine;
using UnityEngine.UI;

public class PanelTransition : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject departingPanel;
    [SerializeField] private GameObject arrivingPanel;

    [Header("Transition")]
    [SerializeField] private MonoBehaviour transitionType;
    [SerializeField] private float transitionDuration = 0.25f;

    [Header("Trigger")]
    [SerializeField] private Button triggerButton;

    private void Awake()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.AddListener(ExecuteTransition);
        }
    }

    private void OnDestroy()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.RemoveListener(ExecuteTransition);
        }
    }

    public void ExecuteTransition()
    {
        if (transitionType == null)
        {
            SwitchPanels();
            return;
        }
    }

    private void SwitchPanels()
    {
        if (departingPanel != null)
        {
            departingPanel.SetActive(false);
        }

        if (arrivingPanel != null)
        {
            arrivingPanel.SetActive(true);
        }
    }
}