using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LanguageTagSelector : MonoBehaviour
{
    [Header("Language Buttons")]
    [SerializeField] private Button[] languageButtons;

    [Header("Selected")]
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color selectedTextColor = Color.black;

    private Color[] originalButtonColors;
    private Color[] originalTextColors;

    private int selectedIndex = 0;

    public LanguageTag CurrentLanguageTag =>
        (LanguageTag)selectedIndex;

    private void Awake()
    {
        if (languageButtons == null || languageButtons.Length == 0)
            return;

        originalButtonColors = new Color[languageButtons.Length];
        originalTextColors = new Color[languageButtons.Length];

        for (int i = 0; i < languageButtons.Length; i++)
        {
            int index = i;

            Image buttonImage =
                languageButtons[i].GetComponent<Image>();

            TMP_Text text =
                languageButtons[i].GetComponentInChildren<TMP_Text>();

            if (buttonImage != null)
                originalButtonColors[i] = buttonImage.color;

            if (text != null)
                originalTextColors[i] = text.color;

            languageButtons[i].onClick.AddListener(
                () => SelectLanguage(index)
            );
        }

        // 기본값: N/A
        SelectLanguage(0);
    }

    private void SelectLanguage(int index)
    {
        if (index < 0 || index >= languageButtons.Length)
            return;

        selectedIndex = index;

        for (int i = 0; i < languageButtons.Length; i++)
        {
            Image buttonImage =
                languageButtons[i].GetComponent<Image>();

            TMP_Text text =
                languageButtons[i].GetComponentInChildren<TMP_Text>();

            bool isSelected = i == selectedIndex;

            if (buttonImage != null)
            {
                buttonImage.color =
                    isSelected
                    ? selectedColor
                    : originalButtonColors[i];
            }

            if (text != null)
            {
                text.color =
                    isSelected
                    ? selectedTextColor
                    : originalTextColors[i];
            }
        }
    }

    public LanguageTag GetValue()
    {
        return CurrentLanguageTag;
    }
}