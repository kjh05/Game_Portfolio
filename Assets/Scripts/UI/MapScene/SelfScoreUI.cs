
using TMPro;
using UnityEngine;
using Steamworks;

public class SelfScoreUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text scoreText;

    private void OnEnable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyPlayersUpdated
                += HandleLobbyPlayersUpdated;
        }

        RefreshScore();
    }

    private void OnDisable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyPlayersUpdated
                -= HandleLobbyPlayersUpdated;
        }
    }

    private void HandleLobbyPlayersUpdated(
        System.Collections.Generic.IReadOnlyList<LobbyPlayerData> players)
    {
        RefreshScore();
    }

    private void RefreshScore()
    {
        if (scoreText == null)
            return;

        // Steam 초기화 또는 Lobby 입장 전
        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            scoreText.text = "0";
            return;
        }

        ulong mySteamId = SteamUser.GetSteamID().m_SteamID;

        if (SteamLobby.Instance.TryGetLobbyPlayerData(
                mySteamId,
                out LobbyPlayerData playerData))
        {
            scoreText.text = playerData.score.ToString();
        }
        else
        {
            scoreText.text = "0";
        }
    }
}
