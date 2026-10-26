
using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class KickManager : MonoBehaviour
{
    public static KickManager Instance { get; private set; }

    public event Action<CSteamID, int, int> OnVoteCountUpdated;

    private const string KickCommand = "KICK";
    private const string VoteCommand = "VOTE";
    private const string VoteCountCommand = "VOTECOUNT";

    // target Steam ID -> voter Steam IDs
    private readonly Dictionary<ulong, HashSet<ulong>> votes =
        new Dictionary<ulong, HashSet<ulong>>();

    // 클라이언트에서 방장이 보낸 최신 집계 결과
    private readonly Dictionary<ulong, int> displayedVoteCounts =
        new Dictionary<ulong, int>();

    private readonly HashSet<ulong> pendingKicks =
        new HashSet<ulong>();

    private SteamLobby subscribedLobby;

    private const long RejoinBanDurationSeconds = 600;

    private readonly HashSet<ulong> rejoinKickSent =
        new HashSet<ulong>();

    private const string PendingKickedWarningKey = "pending_kicked_warning";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            // 중복 구독 방지
            SteamNetworkMessenger.Instance.OnMessageReceived
                -= HandleMessageReceived;

            SteamNetworkMessenger.Instance.OnMessageReceived
                += HandleMessageReceived;
        }

        if (subscribedLobby == null && SteamLobby.Instance != null)
        {
            subscribedLobby = SteamLobby.Instance;
            subscribedLobby.OnLobbyPlayersUpdated += HandlePlayersUpdated;
            subscribedLobby.RefreshLobbyPlayers();
        }
    }

    private void OnDisable()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived
                -= HandleMessageReceived;
        }

        if (subscribedLobby != null)
        {
            subscribedLobby.OnLobbyPlayersUpdated
                -= HandlePlayersUpdated;

            subscribedLobby = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // 공개 API
    // =========================================================

    public int GetVoteCount(CSteamID targetSteamID)
    {
        ulong targetID = targetSteamID.m_SteamID;

        if (SteamLobby.Instance != null &&
            SteamLobby.Instance.IsCurrentLobbyHost())
        {
            if (votes.TryGetValue(targetID, out HashSet<ulong> voters))
                return voters.Count;

            return 0;
        }

        if (displayedVoteCounts.TryGetValue(targetID, out int count))
            return count;

        return 0;
    }

    public int GetVoteThreshold()
    {
        if (SteamLobby.Instance == null)
            return 0;

        int playerCount = SteamMatchmaking.GetNumLobbyMembers(
            SteamLobby.Instance.CurrentLobbyID
        );

        return (playerCount + 1) / 2;
    }

    public void VoteKick(CSteamID targetSteamID)
    {
        if (SteamLobby.Instance == null ||
            SteamNetworkMessenger.Instance == null)
        {
            Debug.LogWarning("[KickManager] 로비 또는 메신저가 없습니다.");
            return;
        }

        if (!SteamLobby.Instance.TryGetCurrentRoomType(out RoomType roomType) ||
            roomType != RoomType.Public)
        {
            Debug.LogWarning("[KickManager] 공개 방에서만 투표할 수 있습니다.");
            return;
        }

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;
        CSteamID localID = SteamUser.GetSteamID();

        if (!lobbyID.IsValid() ||
            !targetSteamID.IsValid() ||
            targetSteamID == localID)
        {
            Debug.LogWarning("[KickManager] 잘못된 투표 대상입니다.");
            return;
        }

        if (!IsLobbyMember(targetSteamID))
        {
            Debug.LogWarning("[KickManager] 대상이 현재 방에 없습니다.");
            return;
        }

        // 방장은 투표를 즉시 집계할 수 있습니다.
        if (SteamLobby.Instance.IsCurrentLobbyHost())
        {
            RegisterVote(localID, targetSteamID);
            return;
        }

        string message =
            $"{VoteCommand}|{lobbyID.m_SteamID}|{targetSteamID.m_SteamID}";

        bool sent = SteamNetworkMessenger.Instance.SendMessage(
            SteamMatchmaking.GetLobbyOwner(lobbyID),
            message
        );

        if (sent)
        {
            Debug.Log(
                $"[KickManager] 투표 요청 전송: {targetSteamID.m_SteamID}"
            );
        }
        else
        {
            Debug.LogWarning("[KickManager] 투표 요청 전송 실패");
        }
    }

    // =========================================================
    // 메시지 처리
    // =========================================================

    private void HandleMessageReceived(CSteamID senderID, string message)
    {
        if (string.IsNullOrEmpty(message) ||
            SteamLobby.Instance == null)
            return;

        string[] parts = message.Split('|');

        if (parts.Length == 3 && parts[0] == VoteCommand)
        {
            HandleVoteMessage(senderID, parts);
            return;
        }

        if (parts.Length == 5 && parts[0] == VoteCountCommand)
        {
            HandleVoteCountMessage(senderID, parts);
            return;
        }

        if (parts.Length == 3 && parts[0] == KickCommand)
        {
            HandleKickMessage(senderID, parts);
        }
    }

    private void HandleVoteMessage(CSteamID senderID, string[] parts)
    {
        if (!ulong.TryParse(parts[1], out ulong lobbyValue) ||
            !ulong.TryParse(parts[2], out ulong targetValue))
            return;

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        if (!lobbyID.IsValid() ||
            lobbyID.m_SteamID != lobbyValue ||
            !SteamLobby.Instance.IsCurrentLobbyHost())
            return;

        if (!SteamLobby.Instance.TryGetCurrentRoomType(out RoomType roomType) ||
            roomType != RoomType.Public)
            return;

        if (!IsLobbyMember(senderID) ||
            !IsLobbyMember(new CSteamID(targetValue)) ||
            senderID.m_SteamID == targetValue)
            return;

        RegisterVote(senderID, new CSteamID(targetValue));
    }

    private void HandleVoteCountMessage(CSteamID senderID, string[] parts)
    {
        if (!ulong.TryParse(parts[1], out ulong lobbyValue) ||
            !ulong.TryParse(parts[2], out ulong targetValue) ||
            !int.TryParse(parts[3], out int count) ||
            !int.TryParse(parts[4], out int threshold))
            return;

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        if (!lobbyID.IsValid() ||
            lobbyID.m_SteamID != lobbyValue ||
            senderID != SteamMatchmaking.GetLobbyOwner(lobbyID))
            return;

        if (count < 0 || threshold < 1 || count > threshold)
            return;

        ulong localID = SteamUser.GetSteamID().m_SteamID;

        if (!IsLobbyMember(new CSteamID(targetValue)))
            return;

        displayedVoteCounts[targetValue] = count;

        OnVoteCountUpdated?.Invoke(
            new CSteamID(targetValue),
            count,
            threshold
        );
    }

    private void HandleKickMessage(CSteamID senderID, string[] parts)
    {
        if (!ulong.TryParse(parts[1], out ulong lobbyValue) ||
            !ulong.TryParse(parts[2], out ulong targetValue))
            return;

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        if (!lobbyID.IsValid() ||
            lobbyID.m_SteamID != lobbyValue ||
            SteamUser.GetSteamID().m_SteamID != targetValue)
            return;

        if (senderID != SteamMatchmaking.GetLobbyOwner(lobbyID))
        {
            Debug.LogWarning("[KickManager] 방장이 아닌 사용자의 강퇴 명령입니다.");
            return;
        }

        // MainMenu 씬으로 돌아온 뒤 강퇴 경고를 표시하도록 기록한다.
        PlayerPrefs.SetInt(PendingKickedWarningKey, 1);
        PlayerPrefs.Save();

        SteamLobby.Instance.LeaveCurrentLobby("MainMenu");
    }

    // =========================================================
    // 방장 투표 집계
    // =========================================================

    private void RegisterVote(CSteamID voter, CSteamID target)
    {
        if (!SteamLobby.Instance.IsCurrentLobbyHost())
            return;

        ulong voterID = voter.m_SteamID;
        ulong targetID = target.m_SteamID;

        if (voterID == targetID ||
            !IsLobbyMember(voter) ||
            !IsLobbyMember(target) ||
            pendingKicks.Contains(targetID))
            return;

        if (!votes.TryGetValue(targetID, out HashSet<ulong> voters))
        {
            voters = new HashSet<ulong>();
            votes[targetID] = voters;
        }

        // 같은 사람이 같은 대상을 여러 번 눌러도 한 표만 인정
        bool added = voters.Add(voterID);

        if (!added)
        {
            Debug.Log("[KickManager] 이미 투표한 대상입니다.");
            return;
        }

        int count = voters.Count;
        int threshold = GetVoteThreshold();

        Debug.Log(
            $"[KickManager] 투표: {targetID} ({count}/{threshold})"
        );

        BroadcastVoteCount(target, count, threshold);

        if (count >= threshold)
        {
            pendingKicks.Add(targetID);

            Debug.Log(
                $"[KickManager] 과반수 달성. 강퇴 실행: {targetID}"
            );

            RequestKick(target);
        }
    }

    private void BroadcastVoteCount(
        CSteamID target,
        int count,
        int threshold)
    {
        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        OnVoteCountUpdated?.Invoke(target, count, threshold);

        string message =
            $"{VoteCountCommand}|{lobbyID.m_SteamID}|" +
            $"{target.m_SteamID}|{count}|{threshold}";

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID member =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);

            if (member == SteamUser.GetSteamID())
                continue;

            SteamNetworkMessenger.Instance.SendMessage(member, message);
        }
    }

    // =========================================================
    // 기존 강퇴 기능
    // =========================================================
    public bool RequestKick(CSteamID targetSteamID)
    {
        if (SteamLobby.Instance == null ||
            SteamNetworkMessenger.Instance == null)
            return false;

        if (!SteamLobby.Instance.IsCurrentLobbyHost())
            return false;

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        if (!lobbyID.IsValid() ||
            !targetSteamID.IsValid() ||
            targetSteamID == SteamUser.GetSteamID() ||
            !IsLobbyMember(targetSteamID))
            return false;

        // 먼저 10분 제한을 기록합니다.
        if (!SaveRejoinBan(lobbyID, targetSteamID))
            return false;

        // 기록이 성공한 뒤 강퇴 명령을 전송합니다.
        bool sent = SendKickCommand(targetSteamID);

        if (!sent)
        {
            Debug.LogWarning(
                "[KickManager] 강퇴 명령 전송에 실패했습니다. " +
                "재입장 제한 기록은 유지됩니다."
            );
        }

        return sent;
    }

    // =========================================================
    // 플레이어 입장 / 퇴장
    // =========================================================
    private void HandlePlayersUpdated(
        IReadOnlyList<LobbyPlayerData> players)
    {
        HashSet<ulong> currentIDs = new HashSet<ulong>();

        foreach (LobbyPlayerData player in players)
            currentIDs.Add(player.steamID.m_SteamID);

        if (SteamLobby.Instance != null &&
            SteamLobby.Instance.IsCurrentLobbyHost())
        {
            ulong localID = SteamUser.GetSteamID().m_SteamID;

            foreach (LobbyPlayerData player in players)
            {
                ulong playerID = player.steamID.m_SteamID;

                if (playerID == localID)
                    continue;

                bool banned = IsBannedFromLobby(
                    SteamLobby.Instance.CurrentLobbyID,
                    player.steamID,
                    out _
                );

                if (banned && rejoinKickSent.Add(playerID))
                {
                    Debug.Log(
                        $"[KickManager] 재입장 제한 대상 발견: {playerID}"
                    );

                    SendKickCommand(player.steamID);
                }
            }

            rejoinKickSent.RemoveWhere(
                playerID => !currentIDs.Contains(playerID)
            );
        }

        // 모든 클라이언트에서 현재 로비에 없는 대상의
        // 이전 투표 표시를 제거합니다.
        List<ulong> departedTargets = new List<ulong>();

        foreach (ulong targetID in displayedVoteCounts.Keys)
        {
            if (!currentIDs.Contains(targetID))
                departedTargets.Add(targetID);
        }

        foreach (ulong targetID in departedTargets)
            displayedVoteCounts.Remove(targetID);

        // 투표 기록의 실제 집계는 방장만 담당합니다.
        if (SteamLobby.Instance == null ||
            !SteamLobby.Instance.IsCurrentLobbyHost())
            return;

        List<ulong> targetsToClear = new List<ulong>();

        foreach (var entry in votes)
        {
            ulong targetID = entry.Key;

            // 강퇴 대상이 퇴장하면 해당 대상의 투표 기록을 초기화합니다.
            if (!currentIDs.Contains(targetID))
            {
                targetsToClear.Add(targetID);
                continue;
            }

            // 퇴장한 투표자의 표를 제거합니다.
            entry.Value.RemoveWhere(id => !currentIDs.Contains(id));
        }

        foreach (ulong targetID in targetsToClear)
        {
            votes.Remove(targetID);
            pendingKicks.Remove(targetID);
            displayedVoteCounts.Remove(targetID);
        }

        // 투표자 퇴장으로 표 수가 바뀐 대상의 집계 결과를 다시 전파합니다.
        foreach (var entry in votes)
        {
            CSteamID target = new CSteamID(entry.Key);
            int count = entry.Value.Count;
            int threshold = GetVoteThreshold();

            BroadcastVoteCount(target, count, threshold);
        }
    }

    private bool IsLobbyMember(CSteamID target)
    {
        if (SteamLobby.Instance == null)
            return false;

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        if (!lobbyID.IsValid())
            return false;

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);

        for (int i = 0; i < memberCount; i++)
        {
            if (SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i) == target)
                return true;
        }

        return false;
    }

    private static string GetBanKey(CSteamID targetSteamID)
    {
        return $"kickban_{targetSteamID.m_SteamID}";
    }

    private bool SaveRejoinBan(
        CSteamID lobbyID,
        CSteamID targetSteamID)
    {
        long expiresAt =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            + RejoinBanDurationSeconds;

        bool saved = SteamMatchmaking.SetLobbyData(
            lobbyID,
            GetBanKey(targetSteamID),
            expiresAt.ToString()
        );

        if (!saved)
        {
            Debug.LogError(
                "[KickManager] 재입장 제한 기록 저장에 실패했습니다."
            );
        }

        return saved;
    }

    public static bool IsBannedFromLobby(
        CSteamID lobbyID,
        CSteamID playerSteamID,
        out long remainingSeconds)
    {
        remainingSeconds = 0;

        if (!lobbyID.IsValid() || !playerSteamID.IsValid())
            return false;

        string value = SteamMatchmaking.GetLobbyData(
            lobbyID,
            GetBanKey(playerSteamID)
        );

        if (!long.TryParse(value, out long expiresAt))
            return false;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        remainingSeconds = expiresAt - now;

        return remainingSeconds > 0;
    }

    private bool SendKickCommand(CSteamID targetSteamID)
    {
        if (SteamLobby.Instance == null ||
            SteamNetworkMessenger.Instance == null)
            return false;

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        string message =
            $"KICK|{lobbyID.m_SteamID}|{targetSteamID.m_SteamID}";

        return SteamNetworkMessenger.Instance.SendMessage(
            targetSteamID,
            message
        );
    }
}
