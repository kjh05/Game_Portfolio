using TMPro;
using UnityEngine;

public class LobbyPlayerSlot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text hostIndicatorText;

    private LobbyPlayerData currentPlayerData;

    private void Awake()
    {
        Clear();
    }

    public void SetPlayer(LobbyPlayerData playerData)
    {
        Debug.Log(
            $"LobbyPlayerSlot.SetPlayer 호출 | " +
            $"Object: {gameObject.name} | " +
            $"Name: {playerData.steamName}"
        );

        currentPlayerData = playerData;

        // 플레이어 이름 표시
        playerNameText.text =
            FormatPlayerName(playerData.steamName);

        // Ready 표시
        readyIndicator.SetActive(
            playerData.isReady
        );

        // Host 표시
        if (playerData.isHost)
        {
            hostIndicatorText.text = "★";
        }
        else
        {
            hostIndicatorText.text = "";
        }
    }

    public void Clear()
    {
        currentPlayerData = null;

        if (playerNameText != null)
        {
            playerNameText.text = "";
        }

        if (readyIndicator != null)
        {
            readyIndicator.SetActive(false);
        }

        if (hostIndicatorText != null)
        {
            hostIndicatorText.text = "";
        }
    }

    private string FormatPlayerName(string steamName)
    {
        if (string.IsNullOrEmpty(steamName))
        {
            return "";
        }

        if (steamName.Length <= 16)
        {
            return steamName;
        }

        return steamName.Substring(0, 16) + "...";
    }

    public LobbyPlayerData GetPlayerData()
    {
        return currentPlayerData;
    }
}