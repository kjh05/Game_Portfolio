using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using System.Collections;

public class LobbyRoomInfoUI : MonoBehaviour
{
    [Header("Room Info Texts")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text gameModeText;
    [SerializeField] private TMP_Text languageText;

    [Header("Room Code Copy")]
    [SerializeField] private Button roomCodeButton;
    [SerializeField] private float copiedMessageDuration = 1.5f;

    private string currentRoomCode;
    private string originalRoomCodeText;
    private Coroutine copiedMessageCoroutine;

    private void Start()
    {
        if (SteamLobby.Instance == null)
        {
            Debug.LogError("SteamLobby 인스턴스를 찾을 수 없습니다.");
            return;
        }

        SteamLobby.Instance.OnLobbyDataUpdated += HandleLobbyDataUpdated;

        roomCodeButton.onClick.AddListener(CopyRoomCode);

        RefreshRoomInfo();
    }

    private void OnDestroy()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyDataUpdated -= HandleLobbyDataUpdated;
        }

        if (roomCodeButton != null)
        {
            roomCodeButton.onClick.RemoveListener(CopyRoomCode);
        }
    }

    private void HandleLobbyDataUpdated(LobbyDataUpdate_t callback)
    {
        if (SteamLobby.Instance == null)
            return;

        if (callback.m_ulSteamIDLobby !=
            SteamLobby.Instance.CurrentLobbyID.m_SteamID)
            return;

        if (callback.m_bSuccess == 0)
            return;

        RefreshRoomInfo();
    }

    private void RefreshRoomInfo()
    {
        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        if (lobbyID == CSteamID.Nil)
            return;

        currentRoomCode =
            SteamMatchmaking.GetLobbyData(lobbyID, "room_code");

        string gameMode =
            SteamMatchmaking.GetLobbyData(lobbyID, "game_mode");

        string language =
            SteamMatchmaking.GetLobbyData(lobbyID, "language");

        roomCodeText.text = currentRoomCode;
        gameModeText.text = FormatDisplayName(gameMode);
        languageText.text = FormatDisplayName(language);

        originalRoomCodeText = roomCodeText.text;
    }

    private string FormatDisplayName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "-";

        return value.Replace("_", " ");
    }

    private void CopyRoomCode()
    {
        if (string.IsNullOrEmpty(currentRoomCode))
        {
            Debug.LogWarning("복사할 Room Code가 없습니다.");
            return;
        }

        GUIUtility.systemCopyBuffer = currentRoomCode;

        if (copiedMessageCoroutine != null)
            StopCoroutine(copiedMessageCoroutine);

        copiedMessageCoroutine = StartCoroutine(
            ShowCopiedMessage()
        );

        Debug.Log($"Room Code 복사 완료: {currentRoomCode}");
    }

    private IEnumerator ShowCopiedMessage()
    {
        roomCodeText.text = "Copied!";

        yield return new WaitForSeconds(copiedMessageDuration);

        roomCodeText.text = originalRoomCodeText;
        copiedMessageCoroutine = null;
    }
}