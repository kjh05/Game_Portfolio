using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIHoverEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("Hover")]
    [SerializeField] private Image targetImage;
    [SerializeField] private float hoverBrightness = 0.15f;

    private Color originalColor;

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetImage != null)
        {
            originalColor = targetImage.color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetImage == null)
            return;

        targetImage.color = originalColor * (1f + hoverBrightness);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetColor();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ResetColor();
    }

    private void ResetColor()
    {
        if (targetImage == null)
            return;

        targetImage.color = originalColor;
    }
}