
using System.Collections.Generic;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReadyManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyCountText;

    [Header("Settings")]
    [SerializeField, Min(1f)] private float countdownDuration = 30f;
    [SerializeField, Min(0.1f)] private float playerRefreshInterval = 0.5f;

    private readonly HashSet<ulong> readyPlayers = new HashSet<ulong>();

    private float countdownRemaining;
    private float playerRefreshTimer;
    private int currentPlayerCount;
    private bool countdownRunning;
    private bool hasVoted;
    private bool startedGame;
    private int syncedReadyCount;

    private const string ReadyVotePrefix = "CC_READY_VOTE";
    private const string ReadyStatePrefix = "CC_READY_STATE";

    private void Start()
    {
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyButtonPressed);

        if (SteamNetworkMessenger.Instance != null)
            SteamNetworkMessenger.Instance.OnMessageReceived += HandleMessage;

        RefreshPlayerCount();
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnReadyButtonPressed);

        if (SteamNetworkMessenger.Instance != null)
            SteamNetworkMessenger.Instance.OnMessageReceived -= HandleMessage;
    }

    private void Update()
    {
        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            return;
        }

        playerRefreshTimer -= Time.deltaTime;

        if (playerRefreshTimer <= 0f)
        {
            playerRefreshTimer = playerRefreshInterval;
            RefreshPlayerCount();
        }

        if (!countdownRunning)
        {
            if (IsHost())
                TryStartCountdown();

            return;
        }

        // 호스트와 클라이언트 모두 화면의 카운트다운을 갱신한다.
        countdownRemaining -= Time.deltaTime;
        countdownRemaining = Mathf.Max(0f, countdownRemaining);

        UpdateUI();

        // 실제 게임 시작은 호스트만 담당한다.
        if (!IsHost())
            return;

        if (countdownRemaining <= 0f)
        {
            countdownRunning = false;
            startedGame = true;

            UpdateUI();
            BroadcastReadyState();

            if (MapSelectionManager.Instance != null)
            {
                MapSelectionManager.Instance.StartGame();
            }
            else
            {
                Debug.LogError(
                    "ReadyManager: MapSelectionManager를 찾을 수 없습니다."
                );
            }
        }
    }


    public void OnReadyButtonPressed()
    {
        if (startedGame || hasVoted)
            return;

        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            Debug.LogWarning("Ready: 참가 중인 로비가 없습니다.");
            return;
        }

        hasVoted = true;

        CSteamID mySteamID = SteamUser.GetSteamID();

        if (IsHost())
        {
            readyPlayers.Add(mySteamID.m_SteamID);
            RefreshPlayerCount();
            TryStartCountdown();
            BroadcastReadyState();
        }
        else
        {
            SendToHost(
                $"{ReadyVotePrefix}|{SteamLobby.Instance.CurrentLobbyID.m_SteamID}"
            );
        }

        UpdateUI();
    }

    private void HandleMessage(CSteamID sender, string message)
    {
        if (string.IsNullOrEmpty(message) ||
            SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            return;
        }

        string[] parts = message.Split('|');

        if (parts.Length < 2)
            return;

        if (!ulong.TryParse(parts[1], out ulong lobbyID) ||
            lobbyID != SteamLobby.Instance.CurrentLobbyID.m_SteamID)
        {
            return;
        }

        if (parts[0] == ReadyVotePrefix)
        {
            if (!IsHost() || !IsLobbyMember(sender))
                return;

            // 한 플레이어가 중복 투표하지 못하게 한다.
            readyPlayers.Add(sender.m_SteamID);

            RefreshPlayerCount();
            TryStartCountdown();
            BroadcastReadyState();
        }
        else if (parts[0] == ReadyStatePrefix)
        {
            if (IsHost() || !IsCurrentHost(sender) || parts.Length != 5)
                return;

            if (!int.TryParse(parts[2], out int readyCount) ||
                !int.TryParse(parts[3], out int playerCount) ||
                !float.TryParse(
                    parts[4],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float remaining))
            {
                return;
            }

            currentPlayerCount = playerCount;
            countdownRemaining = remaining;
            countdownRunning = remaining > 0f && playerCount >= 2;

            syncedReadyCount = readyCount;

            UpdateUI();
        }
    }

    private void RefreshPlayerCount()
    {
        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            currentPlayerCount = 0;
            return;
        }

        int previousCount = currentPlayerCount;

        currentPlayerCount = SteamMatchmaking.GetNumLobbyMembers(
            SteamLobby.Instance.CurrentLobbyID
        );

        if (!IsHost())
        {
            UpdateUI();
            return;
        }

        // 로비에서 나간 플레이어의 Ready 표를 제거한다.
        HashSet<ulong> currentMembers = GetLobbyMemberIDs();

        readyPlayers.RemoveWhere(id => !currentMembers.Contains(id));

        // 2명 미만이면 카운트다운을 취소한다.
        // Ready 표는 유지하므로, 인원이 다시 늘면 재평가된다.
        if (currentPlayerCount < 2 && countdownRunning)
        {
            countdownRunning = false;
            countdownRemaining = 0f;
            BroadcastReadyState();
        }

        // 인원 변동이 있으면 새 인원 기준으로 조건을 다시 평가한다.
        if (previousCount != currentPlayerCount)
        {
            TryStartCountdown();
            BroadcastReadyState();
        }

        UpdateUI();
    }

    private void TryStartCountdown()
    {
        if (!IsHost() || startedGame || countdownRunning)
            return;

        if (currentPlayerCount < 2)
            return;

        // 현재 로비 인원의 과반수. 예: 2명은 1명, 3명은 2명.
        int requiredVotes = (currentPlayerCount + 1) / 2;

        if (readyPlayers.Count < requiredVotes)
            return;

        countdownRunning = true;
        countdownRemaining = countdownDuration;

        Debug.Log($"Ready 조건 충족: {readyPlayers.Count}/{currentPlayerCount}");

        BroadcastReadyState();
        UpdateUI();
    }

    private void BroadcastReadyState()
    {
        if (!IsHost())
            return;

        string message =
            $"{ReadyStatePrefix}|" +
            $"{SteamLobby.Instance.CurrentLobbyID.m_SteamID}|" +
            $"{readyPlayers.Count}|" +
            $"{currentPlayerCount}|" +
            $"{countdownRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        BroadcastToLobby(message);
        UpdateUI();
    }

    private int GetRequiredVotes()
    {
        return (currentPlayerCount + 1) / 2;
    }

    
    private void UpdateClientUI(int readyCount)
    {
        UpdateButtonText(readyCount);
    }

    private void UpdateUI()
    {
        int readyCount = IsHost()
            ? readyPlayers.Count
            : syncedReadyCount;

        UpdateButtonText(readyCount);

        if (readyButton != null)
            readyButton.interactable = !hasVoted && !startedGame;
    }

    private void UpdateButtonText(int readyCount)
    {
        if (readyCountText == null)
            return;

        if (countdownRunning)
        {
            readyCountText.text = FormatTime(countdownRemaining);
        }
        else if (readyCount > 0)
        {
            readyCountText.text =
                $"READY {readyCount}/{GetRequiredVotes()}";
        }
        else
        {
            readyCountText.text = "READY";
        }
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private bool IsHost()
    {
        return SteamLobby.Instance != null &&
               SteamLobby.Instance.IsCurrentLobbyHost();
    }

    private bool IsCurrentHost(CSteamID sender)
    {
        return SteamLobby.Instance != null &&
               SteamMatchmaking.GetLobbyOwner(
                   SteamLobby.Instance.CurrentLobbyID
               ) == sender;
    }

    private bool IsLobbyMember(CSteamID playerID)
    {
        return GetLobbyMemberIDs().Contains(playerID.m_SteamID);
    }

    private HashSet<ulong> GetLobbyMemberIDs()
    {
        HashSet<ulong> members = new HashSet<ulong>();

        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            return members;
        }

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;
        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyID);

        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
            members.Add(member.m_SteamID);
        }

        return members;
    }

    private void SendToHost(string message)
    {
        if (SteamNetworkMessenger.Instance == null)
        {
            Debug.LogError("Ready: SteamNetworkMessenger가 없습니다.");
            return;
        }

        CSteamID host = SteamMatchmaking.GetLobbyOwner(
            SteamLobby.Instance.CurrentLobbyID
        );

        SteamNetworkMessenger.Instance.SendMessage(host, message);
    }

    private void BroadcastToLobby(string message)
    {
        if (SteamNetworkMessenger.Instance == null ||
            SteamLobby.Instance == null)
        {
            return;
        }

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;
        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyID);

        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);

            if (member != SteamUser.GetSteamID())
                SteamNetworkMessenger.Instance.SendMessage(member, message);
        }
    }
}
