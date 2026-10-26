using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionView : MonoBehaviour
{
    [Header("Map Selection Title")]
    [SerializeField] private TMP_Text mapSelectionTitle;

    [Header("Selected Map")]
    [SerializeField] private Image selectedMapImage;
    [SerializeField] private TMP_Text selectedMapExplanation;

    [Header("Default / Random Map")]
    [SerializeField] private Sprite defaultMapImage;
    [SerializeField] private string defaultMapExplanation =
        "A random map will be selected.";

    private void Start()
    {
        ShowRandomMap();
    }

    public void ShowMap(MapData mapData)
    {
        if (mapData == null)
        {
            ShowRandomMap();
            return;
        }

        if (mapSelectionTitle != null)
        {
            mapSelectionTitle.text = mapData.MapName;
        }

        if (selectedMapImage != null)
        {
            selectedMapImage.sprite = mapData.MapImage;
        }

        if (selectedMapExplanation != null)
        {
            selectedMapExplanation.text = mapData.Description;
        }
    }

    public void ShowRandomMap()
    {
        if (mapSelectionTitle != null)
        {
            mapSelectionTitle.text = "MAP SELECTION";
        }

        if (selectedMapImage != null)
        {
            selectedMapImage.sprite = defaultMapImage;
        }

        if (selectedMapExplanation != null)
        {
            selectedMapExplanation.text = defaultMapExplanation;
        }
    }
}