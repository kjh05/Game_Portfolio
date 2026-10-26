using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapBox : MonoBehaviour
{
    [Header("Map Display")]
    [SerializeField] private Image mapImage;
    [SerializeField] private TMP_Text mapTitle;

    [Header("Selection")]
    [SerializeField] private Button selectionButton;
    [SerializeField] private Image selectionBoxImage;
    [SerializeField] private TMP_Text selectionText;

    [Header("Selection Colors")]
    [SerializeField] private Color normalColor = Color.black;
    [SerializeField] private Color selectedColor = Color.blue;

    private MapData mapData;
    private bool isSelected;

    public MapData Data => mapData;
    public bool IsSelected => isSelected;

    public void Initialize(MapData data)
    {
        mapData = data;

        if (mapImage != null)
        {
            mapImage.sprite = data.MapImage;
        }

        if (mapTitle != null)
        {
            mapTitle.text = data.MapName;
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selectionText != null)
        {
            selectionText.text =
                selected ? "SELECTED!" : "SELECT";
        }

        if (selectionBoxImage != null)
        {
            selectionBoxImage.color =
                selected ? selectedColor : normalColor;
        }
    }

    public void ToggleSelected()
    {
        MapListInnerPanel mapList =
            FindFirstObjectByType<MapListInnerPanel>();

        if (mapList == null)
        {
            Debug.LogWarning(
                "MapBox: MapListInnerPanel을 찾을 수 없습니다."
            );

            return;
        }

        mapList.ToggleMapSelection(this);
    }

    public void ShowMapInfo()
    {
        SelectionView selectionView =
            FindFirstObjectByType<SelectionView>();

        if (selectionView == null)
        {
            Debug.LogWarning(
                "MapBox: SelectionView를 찾을 수 없습니다."
            );

            return;
        }

        selectionView.ShowMap(mapData);
    }

    public void SelectAndShowMap()
    {
        ShowMapInfo();
        ToggleSelected();
    }
}