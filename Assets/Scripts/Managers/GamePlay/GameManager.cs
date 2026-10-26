using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GamePhase
    {
        Waiting,
        AssigningRoles,
        MovingChickens,
        Hiding,
        MovingFarmers,
        Playing,
        RoundEnded
    }

    public enum PlayerRole
    {
        Runner,
        Hunter
    }

    [Serializable]
    private class PlayerAssignment
    {
        public ulong steamId;
        public PlayerRole role;
        public int teleportPointIndex;
    }

    [Serializable]
    private class GameStatePacket
    {
        public string messageType;
        public GamePhase phase;
        public float phaseDuration;
        public List<PlayerAssignment> assignments =
            new List<PlayerAssignment>();
    }

    private const string MessagePrefix = "GAME_MANAGER|";
    private const string StateMessageType = "STATE";
    
    private const string TimeMessageType = "TIME";

    [Header("Timer Synchronization")]
    [SerializeField, Min(0.1f)]
    private float timeBroadcastInterval = 1f;

    [Tooltip("클라이언트 시간 보정 속도")]
    [SerializeField, Min(0.1f)]
    private float timeCorrectionSpeed = 2f;

    private float timeBroadcastTimer;
    private float targetRemainingPhaseTime;

    [Header("Initial Waiting")]
    [SerializeField, Min(0f)]
    private float initialWaitingTime = 10f;

    [Header("Main Area Teleport Points")]
    [SerializeField]
    private Transform teleportPointsRoot;

    [Header("Character Prefabs")]
    [SerializeField]
    private GameObject farmerPrefab;

    [Header("Debug")]
    [SerializeField]
    private bool logPhaseChanges = true;

    public GamePhase CurrentPhase { get; private set; }
        = GamePhase.Waiting;

    public float RemainingPhaseTime { get; private set; }

    private readonly Dictionary<ulong, PlayerRole> playerRoles =
        new Dictionary<ulong, PlayerRole>();

    private readonly Dictionary<ulong, int> teleportPointIndices =
        new Dictionary<ulong, int>();

    private readonly HashSet<ulong> teleportedPlayers =
        new HashSet<ulong>();

    private GameStatePacket pendingStatePacket;
    private bool hasReceivedAssignments;
    private bool assignmentsApplied;
    private bool gameStarted;
    private float phaseTimer;

    private Transform[] teleportPoints;

    private bool farmersTransformed = false;

    private bool farmersMovedToMainArea = false;

    public bool IsHunter(ulong steamId)
    {
        return playerRoles.TryGetValue(steamId, out PlayerRole role)
            && role == PlayerRole.Hunter;
    }

    public bool IsRunner(ulong steamId)
    {
        return playerRoles.TryGetValue(steamId, out PlayerRole role)
            && role == PlayerRole.Runner;
    }

    public void GetRoleCounts(out int hunterCount, out int runnerCount)
    {
        hunterCount = 0;
        runnerCount = 0;

        foreach (PlayerRole role in playerRoles.Values)
        {
            if (role == PlayerRole.Hunter)
                hunterCount++;
            else if (role == PlayerRole.Runner)
                runnerCount++;
        }
    }

    public bool TryGetRole(ulong steamId, out PlayerRole role)
    {
        return playerRoles.TryGetValue(steamId, out role);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[GameManager] 중복된 GameManager가 있습니다.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentPhase = GamePhase.Waiting;
    }

    private void OnEnable()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived -=
                HandleNetworkMessage;

            SteamNetworkMessenger.Instance.OnMessageReceived +=
                HandleNetworkMessage;
        }
    }

    private void Start()
    {
        if (teleportPointsRoot == null)
        {
            Debug.LogError(
                "[GameManager] Teleport Points Root가 연결되지 않았습니다.",
                this
            );

            enabled = false;
            return;
        }

        int pointCount = teleportPointsRoot.childCount;
        teleportPoints = new Transform[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            teleportPoints[i] = teleportPointsRoot.GetChild(i);
        }

        if (teleportPoints.Length == 0)
        {
            Debug.LogError(
                "[GameManager] 이동 포인트가 없습니다.",
                this
            );

            enabled = false;
            return;
        }

        if (farmerPrefab == null)
        {
            Debug.LogWarning(
                "[GameManager] Farmer Prefab이 연결되지 않았습니다. " +
                "농부 전환은 아직 실행되지 않습니다.",
                this
            );
        }

        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            Debug.LogWarning(
                "[GameManager] 현재 Steam Lobby가 없습니다."
            );

            enabled = false;
            return;
        }

        if (SteamNetworkMessenger.Instance == null)
        {
            Debug.LogError(
                "[GameManager] SteamNetworkMessenger가 없습니다."
            );

            enabled = false;
            return;
        }

        if (SteamLobby.Instance.IsCurrentLobbyHost())
        {
            BeginInitialWaiting();
        }
        else
        {
            LogPhase("호스트의 게임 시작 명령을 기다립니다.");
        }
    }

    private void Update()
    {
        if (hasReceivedAssignments && !assignmentsApplied)
        {
            TryApplyAssignments();
        }

        // 모든 클라이언트: 헌터 변신
        if (CurrentPhase == GamePhase.Hiding &&
            hasReceivedAssignments &&
            assignmentsApplied &&
            !farmersTransformed)
        {
            TransformHuntersIntoFarmers();
        }
        // 모든 클라이언트: 농부 메인 공간 이동
        if (CurrentPhase == GamePhase.MovingFarmers &&
            hasReceivedAssignments &&
            assignmentsApplied &&
            !farmersMovedToMainArea)
        {
            MoveFarmersToMainArea();
        }
        // 타이머와 페이즈 진행은 호스트만 담당
        if (!SteamLobbyIsHost())
        {
            // 클라이언트는 화면 표시용 시간을 독립적으로 감소시킨다.
            RemainingPhaseTime = Mathf.Max(
                0f,
                RemainingPhaseTime - Time.deltaTime
            );

            // 목표 시간도 같은 속도로 감소시켜 오래된 패킷 때문에
            // 타이머가 매 프레임 되돌아가지 않도록 한다.
            targetRemainingPhaseTime = Mathf.Max(
                0f,
                targetRemainingPhaseTime - Time.deltaTime
            );

            // 현재 표시 시간과 호스트 기준 시간의 오차를 부드럽게 줄인다.
            RemainingPhaseTime = Mathf.MoveTowards(
                RemainingPhaseTime,
                targetRemainingPhaseTime,
                timeCorrectionSpeed * Time.deltaTime
            );

            return;
        }

        if (CurrentPhase == GamePhase.Waiting ||
            CurrentPhase == GamePhase.Hiding ||
            CurrentPhase == GamePhase.MovingFarmers ||
            CurrentPhase == GamePhase.Playing)
        {
            phaseTimer -= Time.deltaTime;
            RemainingPhaseTime = Mathf.Max(0f, phaseTimer);

            // 호스트가 주기적으로 시간 동기화 패킷을 전송한다.
            timeBroadcastTimer -= Time.deltaTime;

            if (timeBroadcastTimer <= 0f)
            {
                timeBroadcastTimer = timeBroadcastInterval;
                BroadcastTimeUpdate();
            }

            if (phaseTimer <= 0f)
            {
                if (CurrentPhase == GamePhase.Waiting)
                {
                    AssignRolesAndMoveRunners();
                }
                else if (CurrentPhase == GamePhase.Hiding)
                {
                    BeginFarmerEntryPhase();
                }
                
                else if (CurrentPhase == GamePhase.MovingFarmers)
                {
                    if (!SteamLobby.Instance.TryLoadGameSettings(
                            out LobbyGameSettings settings))
                    {
                        Debug.LogError(
                            "[GameManager] 라운드 진행 시간을 불러오지 못했습니다."
                        );
                        return;
                    }

                    SetPhase(GamePhase.Playing, settings.roundDuration);
                    BroadcastState(playerRolesToAssignments());

                    Debug.Log(
                        $"[GameManager] 본 게임 시작 | " +
                        $"진행 시간: {settings.roundDuration}초"
                    );
                }
                else if (CurrentPhase == GamePhase.Playing)
                {   
                    SetPhase(GamePhase.RoundEnded, 0f);
                    BroadcastState(playerRolesToAssignments());

                    Debug.Log("[GameManager] Round 종료");
                }
            }
        }
    }

    private bool SteamLobbyIsHost()
    {
        return SteamLobby.Instance != null &&
               SteamLobby.Instance.IsCurrentLobbyHost();
    }

    private void BeginInitialWaiting()
    {
        if (gameStarted)
            return;

        gameStarted = true;

        SetPhase(GamePhase.Waiting, initialWaitingTime);

        BroadcastState(new List<PlayerAssignment>());
    }

    private void AssignRolesAndMoveRunners()
    {
        if (!SteamLobbyIsHost())
            return;

        if (!SteamLobby.Instance.TryLoadGameSettings(
                out LobbyGameSettings settings))
        {
            Debug.LogError(
                "[GameManager] LobbyGameSettings를 불러오지 못했습니다. " +
                "호스트가 설정을 저장했는지 확인하세요."
            );

            gameStarted = false;
            return;
        }

        List<ulong> playerIds = GetLobbyMemberIds();

        if (playerIds.Count < 2)
        {
            Debug.LogWarning(
                "[GameManager] 역할 배정에는 최소 2명이 필요합니다."
            );

            gameStarted = false;
            return;
        }

        Shuffle(playerIds);

        int hunterCount = Mathf.RoundToInt(
            playerIds.Count * settings.hunterRatio / 100f
        );

        hunterCount = Mathf.Clamp(
            hunterCount,
            1,
            playerIds.Count - 1
        );

        playerRoles.Clear();
        teleportPointIndices.Clear();
        teleportedPlayers.Clear();

        List<int> pointIndices = new List<int>();

        for (int i = 0; i < teleportPoints.Length; i++)
        {
            pointIndices.Add(i);
        }

        Shuffle(pointIndices);

        List<PlayerAssignment> assignments =
            new List<PlayerAssignment>();

        for (int i = 0; i < playerIds.Count; i++)
        {
            ulong steamId = playerIds[i];

            bool isHunter = i < hunterCount;

            PlayerRole role = isHunter
                ? PlayerRole.Hunter
                : PlayerRole.Runner;

            // 술래는 대기실에 남으므로 아직 이동 지점이 없다.
            int pointIndex = -1;

            if (!isHunter)
            {
                // 포인트 수보다 플레이어가 많아도 재사용할 수 있다.
                pointIndex = pointIndices[
                    (i - hunterCount) % pointIndices.Count
                ];
            }

            playerRoles[steamId] = role;
            teleportPointIndices[steamId] = pointIndex;

            assignments.Add(new PlayerAssignment
            {
                steamId = steamId,
                role = role,
                teleportPointIndex = pointIndex
            });

            Debug.Log(
                $"[GameManager] 역할 배정 | SteamID: {steamId} | " +
                $"Role: {role} | Point: {pointIndex}"
            );
        }

        SetPhase(GamePhase.AssigningRoles, 0f);
        SetPhase(GamePhase.MovingChickens, 0f);

        // 호스트도 동일한 패킷 처리 경로를 사용한다.
        GameStatePacket packet = new GameStatePacket
        {
            messageType = StateMessageType,
            phase = GamePhase.MovingChickens,
            phaseDuration = 0f,
            assignments = assignments
        };

        ApplyStatePacket(packet);
        BroadcastState(assignments);

        if (!assignmentsApplied)
        {
            Debug.LogWarning(
                "[GameManager] 아직 모든 닭이 등록되지 않았습니다. " +
                "등록이 완료되면 이동을 재시도합니다."
            );
        }
    }

    private void TryApplyAssignments()
    {
        if (pendingStatePacket == null)
            return;

        bool allReady = true;

        foreach (PlayerAssignment assignment
                 in pendingStatePacket.assignments)
        {
            if (assignment == null ||
                assignment.steamId == 0)
            {
                continue;
            }

            if (!PlayerCharacterManager.Instance.TryGetChicken(
                    assignment.steamId,
                    out ChickenNetworkIdentity chicken))
            {
                Debug.LogWarning(
                    $"[GameManager] 닭을 찾지 못함: {assignment.steamId}"
                );

                allReady = false;
                continue;
            }

            if (assignment.role != PlayerRole.Runner)
                continue;
            
            if (teleportedPlayers.Contains(assignment.steamId))
                continue;

            int index = assignment.teleportPointIndex;

            if (index < 0 || index >= teleportPoints.Length)
            {
                Debug.LogWarning(
                    $"[GameManager] 잘못된 이동 포인트: {index}"
                );

                allReady = false;
                continue;
            }

            Transform point = teleportPoints[index];

            Vector3 before = chicken.transform.position;

            ChickenMovement movement =
                chicken.GetComponent<ChickenMovement>();

            if (movement != null)
            {
                movement.TeleportTo(
                    point.position,
                    point.rotation
                );
            }
            else
            {
                chicken.transform.SetPositionAndRotation(
                    point.position,
                    point.rotation
                );
            }

            Vector3 after = chicken.transform.position;

            Debug.Log(
                $"[GameManager] 이동 시도 | ID: {assignment.steamId} | " +
                $"Point: {point.name} | " +
                $"Before: {before} | Target: {point.position} | After: {after}"
            );

            teleportedPlayers.Add(assignment.steamId);

            Debug.Log(
                $"[GameManager] 도망자 이동 완료 | " +
                $"SteamID: {assignment.steamId} | Point: {index}"
            );
        }

        if (!allReady)
            return;

        assignmentsApplied = true;

        // 이동 적용이 끝난 뒤에만 숨는 시간을 시작한다.
        if (SteamLobbyIsHost() &&
            CurrentPhase == GamePhase.MovingChickens)
        {
            StartHidingPhase();
        }
    }

    private void StartHidingPhase()
    {
        if (!SteamLobbyIsHost())
            return;

        if (!SteamLobby.Instance.TryLoadGameSettings(
                out LobbyGameSettings settings))
        {
            Debug.LogError(
                "[GameManager] 숨는 시간을 불러오지 못했습니다."
            );
            return;
        }

        SetPhase(GamePhase.Hiding, settings.hidingTime);

        // 모든 클라이언트에 역할과 Hide Time 시작을 알린다.
        BroadcastState(playerRolesToAssignments());

        // 호스트 자신도 동일한 전환 처리를 수행한다.
        TransformHuntersIntoFarmers();
    }

    private void BeginFarmerEntryPhase()
    {
        if (!SteamLobbyIsHost())
            return;

        farmersMovedToMainArea = false;

        SetPhase(GamePhase.MovingFarmers, 2f);
        BroadcastState(playerRolesToAssignments());

        Debug.Log(
            "[GameManager] Hide Time 종료. 농부 메인 공간 이동을 시작합니다."
        );
    }

    private List<PlayerAssignment> playerRolesToAssignments()
    {
        List<PlayerAssignment> assignments =
            new List<PlayerAssignment>();

        foreach (KeyValuePair<ulong, PlayerRole> pair in playerRoles)
        {
            int pointIndex = teleportPointIndices.TryGetValue(
                pair.Key,
                out int index
            ) ? index : -1;

            assignments.Add(new PlayerAssignment
            {
                steamId = pair.Key,
                role = pair.Value,
                teleportPointIndex = pointIndex
            });
        }

        return assignments;
    }

    private List<ulong> GetLobbyMemberIds()
    {
        List<ulong> ids = new List<ulong>();

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;
        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        for (int i = 0; i < count; i++)
        {
            CSteamID member =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            if (member.IsValid() && !ids.Contains(member.m_SteamID))
                ids.Add(member.m_SteamID);
        }

        return ids;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private void SetPhase(GamePhase phase, float duration)
    {
        CurrentPhase = phase;
        phaseTimer = Mathf.Max(0f, duration);
        RemainingPhaseTime = phaseTimer;
        timeBroadcastTimer = timeBroadcastInterval;
        LogPhase(
            $"페이즈 변경: {phase} | 남은 시간: {RemainingPhaseTime:F1}초"
        );
    }

    private void LogPhase(string message)
    {
        if (logPhaseChanges)
            Debug.Log($"[GameManager] {message}");
    }

    private void BroadcastState(List<PlayerAssignment> assignments)
    {
        if (!SteamLobbyIsHost())
            return;

        GameStatePacket packet = new GameStatePacket
        {
            messageType = StateMessageType,
            phase = CurrentPhase,
            phaseDuration = RemainingPhaseTime,
            assignments = assignments
        };

        string message =
            MessagePrefix + JsonUtility.ToJson(packet);

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;
        CSteamID localId = SteamUser.GetSteamID();

        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        for (int i = 0; i < count; i++)
        {
            CSteamID member =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            if (!member.IsValid() || member == localId)
                continue;

            SteamNetworkMessenger.Instance.SendMessage(
                member,
                message
            );
        }
    }

    private void BroadcastTimeUpdate()
    {
        if (!SteamLobbyIsHost() ||
            SteamNetworkMessenger.Instance == null ||
            SteamLobby.Instance == null)
        {
            return;
        }

        GameStatePacket packet = new GameStatePacket
        {
            messageType = TimeMessageType,
            phase = CurrentPhase,
            phaseDuration = RemainingPhaseTime
        };

        string message =
            MessagePrefix + JsonUtility.ToJson(packet);

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;
        CSteamID localId = SteamUser.GetSteamID();

        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        for (int i = 0; i < count; i++)
        {
            CSteamID member =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            if (!member.IsValid() || member == localId)
                continue;

            SteamNetworkMessenger.Instance.SendMessage(
                member,
                message
            );
        }
    }

    private void HandleNetworkMessage(
        CSteamID senderId,
        string message)
    {
        if (string.IsNullOrEmpty(message) ||
            !message.StartsWith(MessagePrefix, StringComparison.Ordinal))
        {
            return;
        }

        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            return;
        }

        CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(
            SteamLobby.Instance.CurrentLobbyID
        );

        // 게임 상태 명령은 현재 로비 호스트만 보낼 수 있다.
        if (senderId != lobbyOwner)
            return;

        string json = message.Substring(MessagePrefix.Length);

        GameStatePacket packet;

        try
        {
            packet = JsonUtility.FromJson<GameStatePacket>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[GameManager] 상태 패킷 파싱 실패: {exception.Message}"
            );
            return;
        }

        if (packet == null)
            return;

        if (packet.messageType == TimeMessageType)
        {
            // 시간 패킷은 클라이언트의 표시 시간만 보정한다.
            // 페이즈가 바뀐 경우에는 즉시 새로운 시간을 적용한다.
            if (packet.phase != CurrentPhase)
            {   
                ApplyStatePacket(packet);
                targetRemainingPhaseTime = RemainingPhaseTime;
            }
            else
            {
                targetRemainingPhaseTime = Mathf.Max(
                    0f,
                    packet.phaseDuration
                );
            }

            return;
        }

        if (packet.messageType != StateMessageType)
            return;

        ApplyStatePacket(packet);
        targetRemainingPhaseTime = RemainingPhaseTime;
    }

    private bool AssignmentsMatch(GameStatePacket packet)
    {
        if (packet.assignments == null ||
            packet.assignments.Count != playerRoles.Count)
        {
            return false;
        }

        foreach (PlayerAssignment assignment in packet.assignments)
        {
            if (assignment == null || assignment.steamId == 0)
                continue;

            if (!playerRoles.TryGetValue(
                    assignment.steamId, out PlayerRole currentRole) ||
                currentRole != assignment.role)
            {
                return false;
            }

            if (!teleportPointIndices.TryGetValue(
                    assignment.steamId, out int currentIndex) ||
                currentIndex != assignment.teleportPointIndex)
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyStatePacket(GameStatePacket packet)
    {
        if (packet == null)
            return;

        // 단계와 타이머는 매 패킷마다 갱신한다.
        SetPhase(packet.phase, packet.phaseDuration);

        if (packet.assignments == null ||
            packet.assignments.Count == 0)
        {
            return;
        }

        // 역할 배정이 동일하다면 단계만 변경된 것이므로
        // 닭 이동이나 배정 초기화를 다시 하지 않는다.
        if (hasReceivedAssignments && AssignmentsMatch(packet))
        {
            return;
        }

        farmersTransformed = false;

        playerRoles.Clear();
        teleportPointIndices.Clear();
        teleportedPlayers.Clear();

        foreach (PlayerAssignment assignment in packet.assignments)
        {
            if (assignment == null || assignment.steamId == 0)
                continue;

            playerRoles[assignment.steamId] = assignment.role;
            teleportPointIndices[assignment.steamId] =
                assignment.teleportPointIndex;
        }

        pendingStatePacket = packet;
        hasReceivedAssignments = true;
        assignmentsApplied = false;

        TryApplyAssignments();
    }

    private void OnDisable()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived -=
                HandleNetworkMessage;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void TransformHuntersIntoFarmers()
    {
        if (farmersTransformed)
            return;

        if (farmerPrefab == null)
        {
            Debug.LogError("[GameManager] Farmer Prefab이 지정되지 않았습니다.");
            return;
        }

        if (PlayerCharacterManager.Instance == null)
        {
            Debug.LogError("[GameManager] PlayerCharacterManager를 찾을 수 없습니다.");
            return;
        }

        bool allHuntersTransformed = true;

        foreach (KeyValuePair<ulong, PlayerRole> pair in playerRoles)
        {
            ulong steamId = pair.Key;

            if (pair.Value != PlayerRole.Hunter)
                continue;

            if (PlayerCharacterManager.Instance.TryGetFarmer(
                    steamId, out GameObject existingFarmer) &&
                existingFarmer != null)
            {
                continue;
            }

            if (!PlayerCharacterManager.Instance.TryGetPlayer(
                    steamId, out var player) ||
                player.Chicken == null)
            {
                allHuntersTransformed = false;
                continue;
            }

            ChickenNetworkIdentity chicken = player.Chicken;

            Vector3 spawnPosition = chicken.transform.position;

            GameObject farmer = Instantiate(
                farmerPrefab,
                spawnPosition,
                Quaternion.identity
            );

            FarmerLocalInput farmerInput =
                farmer.GetComponent<FarmerLocalInput>();

            FarmerNetworkSync farmerNetworkSync =
                farmer.GetComponent<FarmerNetworkSync>();

            bool isLocalPlayer =
                steamId == SteamUser.GetSteamID().m_SteamID;

            if (farmerInput == null)
            {
                Debug.LogError(
                    $"[GameManager] FarmerLocalInput이 없습니다. Steam ID: {steamId}"
                );

                Destroy(farmer);
                allHuntersTransformed = false;
                continue;
            }

            if (farmerNetworkSync == null)
            {
                Debug.LogError(
                    $"[GameManager] FarmerNetworkSync가 없습니다. Steam ID: {steamId}"
                );

                Destroy(farmer);
                allHuntersTransformed = false;
                continue;
            }

            // 로컬 입력 및 네트워크 동기화 초기화
            farmerNetworkSync.Initialize(steamId, isLocalPlayer);


            bool registered =
                PlayerCharacterManager.Instance.RegisterFarmer(
                    steamId, farmer
                );

            if (!registered)
            {
                Destroy(farmer);
                allHuntersTransformed = false;
                continue;
            }

            PlayerCharacterManager.Instance.SetCurrentCharacter(
                steamId, farmer
            );

            chicken.gameObject.SetActive(false);

            Debug.Log(
                $"[GameManager] 헌터 변신 완료: {steamId}"
            );
        }

        farmersTransformed = allHuntersTransformed;
    }

    private void MoveFarmersToMainArea()
    {
        if (PlayerCharacterManager.Instance == null)
        {
            Debug.LogError(
                "[GameManager] PlayerCharacterManager가 없습니다."
            );
            return;
        }

        if (teleportPoints == null || teleportPoints.Length == 0)
        {
            Debug.LogError(
                "[GameManager] 메인 공간 이동 포인트가 없습니다."
            );
            return;
        }

        List<ulong> hunterIds = new List<ulong>();

        foreach (KeyValuePair<ulong, PlayerRole> pair in playerRoles)
        {
            if (pair.Value == PlayerRole.Hunter)
                hunterIds.Add(pair.Key);
        }

        // 모든 클라이언트에서 동일한 순서로 포인트를 배정한다.
        hunterIds.Sort();

        for (int i = 0; i < hunterIds.Count; i++)
        {
            ulong steamId = hunterIds[i];

            if (!PlayerCharacterManager.Instance.TryGetFarmer(
                    steamId, out GameObject farmer) ||
                farmer == null)
            {
                Debug.LogWarning(
                    $"[GameManager] 이동할 농부를 찾지 못함: {steamId}"
                );
                return;
            }

            Transform point = teleportPoints[i % teleportPoints.Length];

            CharacterController controller =
                farmer.GetComponent<CharacterController>();

            // CharacterController가 켜진 상태에서 Transform을 변경하면
            // 이동이 충돌 처리에 영향을 받을 수 있으므로 잠시 끈다.
            if (controller != null)
                controller.enabled = false;

            farmer.transform.SetPositionAndRotation(
                point.position,
                point.rotation
            );

            if (controller != null)
                controller.enabled = true;

            Debug.Log(
                $"[GameManager] 농부 메인 공간 이동 | " +
                $"SteamID: {steamId} | Point: {point.name}"
            );
        }

        farmersMovedToMainArea = true;
    }
}
