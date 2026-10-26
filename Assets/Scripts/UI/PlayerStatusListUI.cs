using System.Collections.Generic;
using UnityEngine;
using Steamworks;

public class PlayerStatusListUI : MonoBehaviour
{
    [Header("Player List")]
    [SerializeField] private Transform content;
    [SerializeField] private PlayerInfoBoxUI playerInfoBoxPrefab;

    private readonly Dictionary<ulong, PlayerInfoBoxUI> playerBoxes =
        new Dictionary<ulong, PlayerInfoBoxUI>();

    private SteamLobby subscribedLobby;

    private void OnEnable()
    {
        TrySubscribe();
        SubscribeKickManager();
    }

    private void Start()
    {
        TrySubscribe();
        SubscribeKickManager();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (KickManager.Instance != null)
        {
            KickManager.Instance.OnVoteCountUpdated -= HandleVoteCountUpdated;
        }
    }

    private void TrySubscribe()
    {
        if (subscribedLobby != null)
            return;

        if (SteamLobby.Instance == null)
            return;

        subscribedLobby = SteamLobby.Instance;
        subscribedLobby.OnLobbyPlayersUpdated += HandlePlayersUpdated;

        subscribedLobby.RefreshLobbyPlayers();
    }

    private void Unsubscribe()
    {
        if (subscribedLobby != null)
        {
            subscribedLobby.OnLobbyPlayersUpdated -= HandlePlayersUpdated;
            subscribedLobby = null;
        }
    }

    private void SubscribeKickManager()
    {
        if (KickManager.Instance == null)
            return;

        KickManager.Instance.OnVoteCountUpdated -= HandleVoteCountUpdated;
        KickManager.Instance.OnVoteCountUpdated += HandleVoteCountUpdated;
    }

    private void HandleVoteCountUpdated(
        CSteamID targetSteamID,
        int count,
        int threshold)
    {
        if (playerBoxes.TryGetValue(
            targetSteamID.m_SteamID,
            out PlayerInfoBoxUI box))
        {
            box.SetKickButton(true, $"KICK {count}/{threshold}");
        }
    }

    private void HandlePlayersUpdated(
        IReadOnlyList<LobbyPlayerData> players)
    {
        if (content == null || playerInfoBoxPrefab == null)
        {
            Debug.LogError(
                "[PlayerStatusListUI] Content 또는 PlayerInfoBox Prefab이 연결되지 않았습니다."
            );
            return;
        }

        if (subscribedLobby == null)
            return;

        bool isPublicRoom = false;
        bool roomTypeValid =
            subscribedLobby.TryGetCurrentRoomType(out RoomType roomType);

        if (roomTypeValid)
        {
            isPublicRoom = roomType == RoomType.Public;
        }
        else
        {
            Debug.LogWarning(
                "[PlayerStatusListUI] 현재 방 유형을 확인할 수 없습니다."
            );
        }

        bool localPlayerIsHost =
            subscribedLobby.IsCurrentLobbyHost();

        ulong localSteamID = SteamUser.GetSteamID().m_SteamID;

        int playerCount = players.Count;
        int threshold = (playerCount + 1) / 2;

        HashSet<ulong> currentPlayerIDs = new HashSet<ulong>();

        foreach (LobbyPlayerData player in players)
        {
            ulong id = player.steamID.m_SteamID;
            currentPlayerIDs.Add(id);

            if (!playerBoxes.TryGetValue(
                    id, out PlayerInfoBoxUI box))
            {
                box = Instantiate(playerInfoBoxPrefab, content);
                playerBoxes.Add(id, box);
            }

            box.SetPlayer(player);

            bool isSelf = id == localSteamID;

            bool showKickButton = false;
            string kickLabel = string.Empty;

            if (!isSelf && roomTypeValid)
            {
                if (isPublicRoom)
                {
                    showKickButton = true;
                    int currentVotes = KickManager.Instance != null
                        ? KickManager.Instance.GetVoteCount(player.steamID)
                        : 0;

                    kickLabel = $"KICK {currentVotes}/{threshold}";
                }
                else
                {
                    showKickButton = localPlayerIsHost;
                    kickLabel = "KICK";
                }
            }

            box.SetKickButton(showKickButton, kickLabel);
        }

        List<ulong> departedIDs = new List<ulong>();

        foreach (ulong id in playerBoxes.Keys)
        {
            if (!currentPlayerIDs.Contains(id))
                departedIDs.Add(id);
        }

        foreach (ulong id in departedIDs)
        {
            Destroy(playerBoxes[id].gameObject);
            playerBoxes.Remove(id);
        }
    }
}
