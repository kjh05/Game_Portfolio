using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomRow : MonoBehaviour
{
    [Header("Room Data UI")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text languageText;
    [SerializeField] private TMP_Text gameModeText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text statusText;

    private LobbyData currentLobbyData;

    private Button roomButton;
    private Image roomImage;

    private Color originalBackgroundColor;

    private Color originalRoomCodeColor;
    private Color originalLanguageColor;
    private Color originalGameModeColor;
    private Color originalPlayerCountColor;
    private Color originalStatusColor;

    private bool isSelected;

    public event Action<RoomRow> OnRowClicked;

    private void Awake()
    {
        roomButton = GetComponentInParent<Button>();
        roomImage = GetComponentInParent<Image>();

        if (roomButton == null)
        {
            Debug.LogError(
                $"{gameObject.name}: 부모 Room에서 Button을 찾을 수 없습니다."
            );
            return;
        }

        if (roomImage == null)
        {
            Debug.LogError(
                $"{gameObject.name}: 부모 Room에서 Image를 찾을 수 없습니다."
            );
            return;
        }

        originalBackgroundColor = roomImage.color;

        originalRoomCodeColor = roomCodeText.color;
        originalLanguageColor = languageText.color;
        originalGameModeColor = gameModeText.color;
        originalPlayerCountColor = playerCountText.color;
        originalStatusColor = statusText.color;

        roomButton.onClick.AddListener(HandleButtonClick);
    }

    private void OnDestroy()
    {
        if (roomButton != null)
        {
            roomButton.onClick.RemoveListener(HandleButtonClick);
        }
    }

    private void HandleButtonClick()
    {
        if (currentLobbyData == null)
        {
            return;
        }

        OnRowClicked?.Invoke(this);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (roomImage == null)
        {
            return;
        }

        if (selected)
        {
            roomImage.color = Color.white;

            roomCodeText.color = Color.black;
            languageText.color = Color.black;
            gameModeText.color = Color.black;
            playerCountText.color = Color.black;
            statusText.color = Color.black;
        }
        else
        {
            roomImage.color = originalBackgroundColor;

            roomCodeText.color = originalRoomCodeColor;
            languageText.color = originalLanguageColor;
            gameModeText.color = originalGameModeColor;
            playerCountText.color = originalPlayerCountColor;
            statusText.color = originalStatusColor;
        }
    }

    public void SetData(LobbyData lobbyData)
    {
        currentLobbyData = lobbyData;

        roomCodeText.text = lobbyData.roomCode;
        languageText.text = lobbyData.language.ToString();
        gameModeText.text = lobbyData.gameMode.ToString();
        playerCountText.text =
            $"{lobbyData.currentPlayers}/{lobbyData.maxPlayers}";
        statusText.text = lobbyData.state.ToString();

        SetSelected(false);
    }

    public void Clear()
    {
        currentLobbyData = null;

        roomCodeText.text = "";
        languageText.text = "";
        gameModeText.text = "";
        playerCountText.text = "";
        statusText.text = "";

        SetSelected(false);
    }

    public LobbyData GetLobbyData()
    {
        return currentLobbyData;
    }

    public bool IsSelected()
    {
        return isSelected;
    }
}