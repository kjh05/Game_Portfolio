using System.Collections;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class ChickenSpawner : MonoBehaviour
{
    [Header("Chicken Prefab")]
    [SerializeField] private GameObject chickenPrefab;

    [Header("Spawn Points")]
    [Tooltip("SpawnPoints > IntermissionSpawn을 연결한다.")]
    [SerializeField] private Transform spawnRoot;

    private readonly Dictionary<ulong, ChickenNetworkIdentity> spawnedChickens =
        new Dictionary<ulong, ChickenNetworkIdentity>();

    private readonly HashSet<ulong> currentLobbyPlayers =
        new HashSet<ulong>();

    private Coroutine initializeCoroutine;

    private void OnEnable()
    {
        if (initializeCoroutine == null)
            initializeCoroutine = StartCoroutine(InitializeSpawner());
    }

    private void OnDisable()
    {
        if (initializeCoroutine != null)
        {
            StopCoroutine(initializeCoroutine);
            initializeCoroutine = null;
        }

        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyPlayersUpdated -= HandleLobbyPlayersUpdated;
        }
    }

    private IEnumerator InitializeSpawner()
    {
        // SteamLobby가 준비될 때까지 기다린다.
        yield return new WaitUntil(() => SteamLobby.Instance != null);

        if (chickenPrefab == null)
        {
            Debug.LogError(
                "[ChickenSpawner] Chicken Prefab이 연결되지 않았습니다.",
                this
            );

            initializeCoroutine = null;
            yield break;
        }

        if (spawnRoot == null)
        {
            Debug.LogError(
                "[ChickenSpawner] Spawn Root가 연결되지 않았습니다.",
                this
            );

            initializeCoroutine = null;
            yield break;
        }

        if (chickenPrefab.GetComponent<ChickenNetworkIdentity>() == null)
        {
            Debug.LogError(
                "[ChickenSpawner] Chicken Prefab 루트에 ChickenNetworkIdentity가 없습니다.",
                chickenPrefab
            );

            initializeCoroutine = null;
            yield break;
        }

        SteamLobby.Instance.OnLobbyPlayersUpdated += HandleLobbyPlayersUpdated;

        // 현재 Lobby 멤버 목록을 처음부터 요청한다.
        SteamLobby.Instance.RefreshLobbyPlayers();

        initializeCoroutine = null;
    }

    private void HandleLobbyPlayersUpdated(IReadOnlyList<LobbyPlayerData> players)
    {
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning(
                "[ChickenSpawner] Lobby 플레이어 목록이 비어 있습니다."
            );

            return;
        }

        List<ulong> playerIds = new List<ulong>();

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;

        if (lobbyId == CSteamID.Nil)
        {
            Debug.LogWarning("[ChickenSpawner] 현재 Lobby가 없습니다.");
            return;
        }

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            if (memberId != CSteamID.Nil)
                playerIds.Add(memberId.m_SteamID);
        }

        // 모든 클라이언트에서 같은 Steam ID 순서로 스폰 위치를 배정한다.
        playerIds.Sort();

        if (playerIds.Count == 0)
        {
            Debug.LogWarning(
                "[ChickenSpawner] 유효한 Steam Lobby 멤버가 없습니다."
            );

            return;
        }

        Transform[] spawnPoints = GetSpawnPoints();

        if (spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[ChickenSpawner] Spawn Root 아래에 자식 스폰 포인트가 없습니다.",
                spawnRoot
            );

            return;
        }

        ulong localSteamId = SteamUser.GetSteamID().m_SteamID;

        currentLobbyPlayers.Clear();

        foreach (ulong playerId in playerIds)
            currentLobbyPlayers.Add(playerId);

        // Lobby에서 나간 플레이어의 닭과 등록 정보 제거
        List<ulong> departedPlayers = new List<ulong>();

        foreach (KeyValuePair<ulong, ChickenNetworkIdentity> pair in spawnedChickens)
        {
            if (!currentLobbyPlayers.Contains(pair.Key))
                departedPlayers.Add(pair.Key);
        }

        foreach (ulong departedId in departedPlayers)
        {
            // 먼저 매니저의 플레이어 등록 정보를 제거한다.
            if (PlayerCharacterManager.Instance != null)
                PlayerCharacterManager.Instance.UnregisterPlayer(departedId);

            if (spawnedChickens.TryGetValue(
                departedId,
                out ChickenNetworkIdentity chicken))
            {
                if (chicken != null)
                    Destroy(chicken.gameObject);
            }

            spawnedChickens.Remove(departedId);
        }

        // 플레이어별 닭을 한 번씩 생성
        for (int i = 0; i < playerIds.Count; i++)
        {
            ulong playerId = playerIds[i];

            if (spawnedChickens.TryGetValue(
                playerId,
                out ChickenNetworkIdentity existing)
                && existing != null)
            {
                // 이미 생성된 닭은 중복 생성하지 않는다.
                continue;
            }

            Transform spawnPoint = spawnPoints[i % spawnPoints.Length];

            GameObject chickenObject = Instantiate(
                chickenPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            ChickenNetworkIdentity identity =
                chickenObject.GetComponent<ChickenNetworkIdentity>();

            bool isLocalPlayer = playerId == localSteamId;

            identity.Initialize(playerId, isLocalPlayer);

            chickenObject.name = isLocalPlayer
                ? $"Chicken_Local_{playerId}"
                : $"Chicken_Remote_{playerId}";

            spawnedChickens[playerId] = identity;

            // 새로 생성한 닭을 PlayerCharacterManager에 등록한다.
            if (PlayerCharacterManager.Instance != null)
            {
                PlayerCharacterManager.Instance.RegisterChicken(
                    playerId,
                    isLocalPlayer,
                    identity
                );
            }
            else
            {
                Debug.LogWarning(
                    "[ChickenSpawner] PlayerCharacterManager가 없어 " +
                    $"플레이어 {playerId}의 캐릭터를 등록하지 못했습니다."
                );
            }

            Debug.Log(
                $"[ChickenSpawner] Spawn 완료 | SteamID: {playerId} | " +
                $"Local: {isLocalPlayer} | Spawn: {spawnPoint.name}"
            );
        }
    }

    private Transform[] GetSpawnPoints()
    {
        List<Transform> points = new List<Transform>();

        for (int i = 0; i < spawnRoot.childCount; i++)
        {
            Transform point = spawnRoot.GetChild(i);

            if (point != null)
                points.Add(point);
        }

        return points.ToArray();
    }
}