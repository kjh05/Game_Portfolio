using TMPro;
using UnityEngine;

public class RoomTypeSelector : MonoBehaviour
{
    [Header("Room Type")]
    [SerializeField] private RoomType currentRoomType = RoomType.Private;

    [Header("UI")]
    [SerializeField] private TMP_Text roomTypeText;

    public RoomType CurrentRoomType => currentRoomType;

    private void Start()
    {
        UpdateUI();
    }

    public void ToggleRoomType()
    {
        if (currentRoomType == RoomType.Private)
        {
            currentRoomType = RoomType.Public;
        }
        else
        {
            currentRoomType = RoomType.Private;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (roomTypeText == null)
            return;

        roomTypeText.text = currentRoomType.ToString();
    }

    public RoomType GetValue()
    {
        return currentRoomType;
    }
}