using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

public class PlayerInfoBoxUI : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerStatusText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Host Indicator")]
    [SerializeField] private GameObject hostIndicator;

    [Header("Kick Button")]
    [SerializeField] private Button kickButton;
    [SerializeField] private TMP_Text kickButtonText;

    private LobbyPlayerData currentPlayer;

    private void Awake()
    {
        if (kickButton != null)
        {
            kickButton.onClick.AddListener(OnKickButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (kickButton != null)
        {
            kickButton.onClick.RemoveListener(OnKickButtonClicked);
        }
    }

    private void OnKickButtonClicked()
    {
        if (currentPlayer == null)
            return;

        if (KickManager.Instance == null || SteamLobby.Instance == null)
        {
            Debug.LogWarning("[PlayerInfoBoxUI] KickManager 또는 SteamLobby가 없습니다.");
            return;
        }

        if (!SteamLobby.Instance.TryGetCurrentRoomType(out RoomType roomType))
        {
            Debug.LogWarning("[PlayerInfoBoxUI] 방 유형을 확인할 수 없습니다.");
            return;
        }

        if (roomType == RoomType.Public)
        {
            KickManager.Instance.VoteKick(currentPlayer.steamID);
        }
        else if (SteamLobby.Instance.IsCurrentLobbyHost())
        {
            KickManager.Instance.RequestKick(currentPlayer.steamID);
        }
    }

    public void SetPlayer(LobbyPlayerData player)
    {
        currentPlayer = player;

        if (player == null)
        {
            Clear();
            return;
        }

        if (playerNameText != null)
            playerNameText.text = player.steamName;

        if (playerStatusText != null)
            playerStatusText.text = GetStatusText(player.status);

        if (scoreText != null)
            scoreText.text = $"SCORE: {player.score}";

        if (hostIndicator != null)
            hostIndicator.SetActive(player.isHost);
    }

    public void SetKickButton(bool visible, string label)
    {
        if (kickButton != null)
            kickButton.gameObject.SetActive(visible);

        if (kickButtonText != null)
            kickButtonText.text = label;
    }

    public void Clear()
    {
        if (playerNameText != null)
            playerNameText.text = string.Empty;

        if (playerStatusText != null)
            playerStatusText.text = string.Empty;

        if (scoreText != null)
            scoreText.text = string.Empty;

        if (hostIndicator != null)
            hostIndicator.SetActive(false);

        SetKickButton(false, string.Empty);
    }

    private string GetStatusText(PlayerLobbyStatus status)
    {
        switch (status)
        {
            case PlayerLobbyStatus.Hunter:
                return "Hunter";

            case PlayerLobbyStatus.Hider:
                return "Hider";

            case PlayerLobbyStatus.Spectator:
                return "Spectator";

            case PlayerLobbyStatus.InLobby:
            default:
                return "In Lobby";
        }
    }
}