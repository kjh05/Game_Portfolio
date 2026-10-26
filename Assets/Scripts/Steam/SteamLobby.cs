using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;
using System.Collections.Generic;

public class SteamLobby : MonoBehaviour
{
    private static SteamLobby instance;
    public static SteamLobby Instance => instance;

    public event Action<IReadOnlyList<LobbyData>> OnPublicLobbyListUpdated;

    // 개별 Lobby의 최신 데이터를 확인했을 때 발생
    public event Action<LobbyDataUpdate_t> OnLobbyDataUpdated;

    public event Action<IReadOnlyList<LobbyPlayerData>> OnLobbyPlayersUpdated;
    
    // Join 대기 UI용 이벤트
    public event Action OnLobbyJoinStarted;
    public event Action<float> OnLobbyJoinWaitingStarted;
    public event Action<float> OnLobbyJoinProgressChanged;
    public event Action OnLobbyJoinFinished;

    private CallResult<LobbyCreated_t> lobbyCreatedResult;

    private CallResult<LobbyMatchList_t> lobbyMatchListResult;

    // Room Code 조회용
    private CallResult<LobbyMatchList_t> roomCodeLookupResult;

    // Lobby 입장 결과
    private CallResult<LobbyEnter_t> lobbyEnterResult;

    private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;

    private CSteamID currentLobbyID;

    private RoomSettings currentRoomSettings;

    private string currentRoomCode;

    private const int roomCodeLength = 6;

    private const string roomCodeCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    // Lobby 씬 이름
    private const string lobbySceneName = "Lobby";

    private List<LobbyData> publicLobbyList =
        new List<LobbyData>();

    // 현재 Public Lobby 검색이 진행 중인지
    private bool isLobbyListRequestInProgress;

    // 현재 Room Code 조회가 진행 중인지
    private bool isRoomCodeLookupInProgress;

    // 현재 Lobby Join이 진행 중인지
    private bool isLobbyJoinInProgress;

    // Full Lobby 재입장 대기 상태
    private bool isLobbyJoinRetryWaiting;

    private int lobbyJoinRequestId;
    private bool isLobbyJoinCancelled;
    private bool hasActiveLobbyJoinRequest;

    // 재입장할 Lobby
    private CSteamID retryLobbyID;

    // 재입장 대기 시간
    private float lobbyJoinRetryElapsed;

    // 다음 재시도까지 남은 시간
    private float lobbyJoinRetryTimer;

    // 재시도 간격
    private const float lobbyJoinRetryInterval = 2f;

    // Public / Private 최대 대기 시간
    private const float lobbyJoinMaxWaitDuration = 60f;

    // 현재 Join 대상 Lobby의 최대 대기 시간
    private float lobbyJoinMaxWaitTime;

    // Room Code 조회 완료 후 호출할 콜백
    private Action<CSteamID> roomCodeLookupCallback;

    private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;

    // 현재 Room Code Join 요청에서 사용할 코드
    private string joiningRoomCode;

    // 검색 요청을 다시 시작할 때까지의 상태
    private bool isRoomCodeJoinRetryScheduled;

    // 현재 Lobby에 참여 중인 플레이어 데이터
    private readonly Dictionary<ulong, LobbyPlayerData>
        lobbyPlayerDataBySteamId =
            new Dictionary<ulong, LobbyPlayerData>();
    
    public bool IsLobbyListRequestInProgress
    {
        get
        {
            return isLobbyListRequestInProgress;
        }
    }

    public bool IsLobbyJoinInProgress
    {
        get
        {
            return isLobbyJoinInProgress;
        }
    }

    public CSteamID CurrentLobbyID
    {
        get
        {
            return currentLobbyID;
        }
    }

    public bool IsLobbyJoinRetryWaiting
    {
        get
        {
            return isLobbyJoinRetryWaiting;
        }
    }

    public float LobbyJoinRetryElapsed
    {
        get
        {
            return lobbyJoinRetryElapsed;
        }
    }

    public float LobbyJoinMaxWaitTime
    {
        get
        {
            return lobbyJoinMaxWaitTime;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);

        lobbyCreatedResult =
            CallResult<LobbyCreated_t>.Create(OnLobbyCreated);

        lobbyMatchListResult =
            CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);

        roomCodeLookupResult =
            CallResult<LobbyMatchList_t>.Create(
                OnRoomCodeLookup
            );

        lobbyEnterResult =
            CallResult<LobbyEnter_t>.Create(
                OnLobbyEntered
            );

        lobbyDataUpdateCallback =
            Callback<LobbyDataUpdate_t>.Create(
                HandleLobbyDataUpdated
            );
        
        lobbyChatUpdateCallback =
            Callback<LobbyChatUpdate_t>.Create(
                HandleLobbyChatUpdate
            );
    }

    private void Update()
    {
        if (!isLobbyJoinRetryWaiting)
            return;

        lobbyJoinRetryElapsed += Time.deltaTime;
        lobbyJoinRetryTimer -= Time.deltaTime;

        float progress = lobbyJoinMaxWaitTime > 0f
            ? Mathf.Clamp01(
                lobbyJoinRetryElapsed / lobbyJoinMaxWaitTime)
            : 1f;

        OnLobbyJoinProgressChanged?.Invoke(progress);

        // 최대 대기 시간 초과
        if (lobbyJoinRetryElapsed >= lobbyJoinMaxWaitTime)
        {
            Debug.LogWarning("Room Code Join 대기 시간 초과");
            FinishLobbyJoinRequest();
            return;
        }

        // 이전 검색 또는 참가 요청이 처리 중이면 기다린다.
        if (isRoomCodeLookupInProgress || isLobbyJoinInProgress)
            return;

        if (lobbyJoinRetryTimer > 0f)
            return;

        lobbyJoinRetryTimer = lobbyJoinRetryInterval;

        Debug.Log(
            $"Room Code 재검색 | " +
            $"{lobbyJoinRetryElapsed:F1}/{lobbyJoinMaxWaitTime:F0}초 | " +
            $"Code: {joiningRoomCode}");

        bool started = FindLobbyByRoomCode(
            joiningRoomCode,
            lobbyID => OnRoomCodeFoundForJoin(
                lobbyID, lobbyJoinRequestId));

        if (!started)
        {
            // 다음 Update 주기에서 다시 시도할 수 있도록 예약한다.
            lobbyJoinRetryTimer = 0.5f;
        }
    }

    // =========================================================
    // Lobby 생성
    // =========================================================

    public void CreateLobby(RoomSettings settings)
    {
        if (settings == null)
        {
            Debug.LogError("RoomSettings가 없습니다.");
            return;
        }

        if (isRoomCodeLookupInProgress)
        {
            Debug.LogWarning(
                "Room Code 확인이 이미 진행 중입니다."
            );

            return;
        }

        currentRoomSettings = settings;

        // Room Code를 생성하고
        // Steam에 이미 같은 코드가 있는지 확인한다.
        GenerateAndCheckRoomCode();
    }

    private void GenerateAndCheckRoomCode()
    {
        currentRoomCode = GenerateRoomCode();

        Debug.Log(
            $"Room Code 후보 생성 | " +
            $"Room Code: {currentRoomCode}"
        );

        FindLobbyByRoomCode(
            currentRoomCode,
            OnRoomCodeCheckForCreation
        );
    }

    private void OnRoomCodeCheckForCreation(
        CSteamID lobbyID)
    {
        // 같은 Room Code를 가진 Lobby가 이미 존재함
        if (lobbyID != CSteamID.Nil)
        {
            Debug.LogWarning(
                $"Room Code 중복 발견 | " +
                $"Room Code: {currentRoomCode} | " +
                $"Lobby ID: {lobbyID}"
            );

            // 기존 코드를 버리고 새 코드 생성
            GenerateAndCheckRoomCode();

            return;
        }

        // 같은 Room Code를 가진 Lobby가 없음
        Debug.Log(
            $"Room Code 사용 가능 | " +
            $"Room Code: {currentRoomCode}"
        );

        // 이제 실제 Steam Lobby 생성
        CreateSteamLobby();
    }

    private void CreateSteamLobby()
    {
        ELobbyType lobbyType;

        if (currentRoomSettings.roomType == RoomType.Public)
        {
            lobbyType =
                ELobbyType.k_ELobbyTypePublic;
        }
        else
        {
            // Private 게임 방은 Steam Invisible Lobby 사용
            lobbyType =
                ELobbyType.k_ELobbyTypeInvisible;
        }

        SteamAPICall_t handle =
            SteamMatchmaking.CreateLobby(
                lobbyType,
                currentRoomSettings.maxPlayers
            );

        lobbyCreatedResult.Set(handle);

        Debug.Log(
            $"Steam Lobby 생성 요청 | " +
            $"Room Code: {currentRoomCode}, " +
            $"Max Players: {currentRoomSettings.maxPlayers}, " +
            $"Room Type: {currentRoomSettings.roomType}, " +
            $"Language: {currentRoomSettings.languageTag}, " +
            $"Game Mode: {currentRoomSettings.gameMode}"
        );
    }

    private void OnLobbyCreated(
        LobbyCreated_t result,
        bool ioFailure)
    {
        if (ioFailure)
        {
            Debug.LogError(
                "Lobby 생성 중 Steam 통신 오류"
            );

            return;
        }

        if (result.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError(
                $"Lobby 생성 실패: {result.m_eResult}"
            );

            return;
        }

        currentLobbyID =
            new CSteamID(result.m_ulSteamIDLobby);

        Debug.Log(
            $"Lobby 생성 성공! ID: {currentLobbyID}"
        );

        SetLobbyData();

        // Lobby 생성자는 이미 해당 Lobby의 멤버이자 Host다.
        // 따라서 JoinLobby()를 다시 호출하지 않는다.
        //
        // 이제 Lobby 씬으로 이동한다.
        LoadLobbyScene();
    }

    private void SetLobbyData()
    {
        bool success;

        success = SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            "room_code",
            currentRoomCode
        );

        if (!success)
        {
            Debug.LogError(
                "room_code Lobby Data 설정 실패"
            );
        }

        success = SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            "max_players",
            currentRoomSettings.maxPlayers.ToString()
        );

        if (!success)
        {
            Debug.LogError(
                "max_players Lobby Data 설정 실패"
            );
        }

        success = SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            "room_type",
            currentRoomSettings.roomType.ToString()
        );

        if (!success)
        {
            Debug.LogError(
                "room_type Lobby Data 설정 실패"
            );
        }

        success = SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            "language",
            currentRoomSettings.languageTag.ToString()
        );

        if (!success)
        {
            Debug.LogError(
                "language Lobby Data 설정 실패"
            );
        }

        success = SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            "game_mode",
            currentRoomSettings.gameMode.ToString()
        );

        if (!success)
        {
            Debug.LogError(
                "game_mode Lobby Data 설정 실패"
            );
        }

        success = SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            "state",
            "InLobby"
        );

        if (!success)
        {
            Debug.LogError(
                "state Lobby Data 설정 실패"
            );
        }

        Debug.Log(
            "Steam Lobby Data 설정 완료 | " +
            $"Room Code: {currentRoomCode}, " +
            $"Max Players: {currentRoomSettings.maxPlayers}, " +
            $"Room Type: {currentRoomSettings.roomType}, " +
            $"Language: {currentRoomSettings.languageTag}, " +
            $"Game Mode: {currentRoomSettings.gameMode}, " +
            "State: InLobby"
        );
    }

    // =========================================================
    // Lobby Scene
    // =========================================================

    private void LoadLobbyScene()
    {
        Debug.Log(
            $"Lobby 씬으로 이동 | Scene: {lobbySceneName}"
        );

        SceneManager.LoadScene(lobbySceneName);
    }

    // =========================================================
    // Room Code
    // =========================================================

    private string GenerateRoomCode()
    {
        StringBuilder code =
            new StringBuilder(roomCodeLength);

        for (int i = 0; i < roomCodeLength; i++)
        {
            int index =
                UnityEngine.Random.Range(
                    0,
                    roomCodeCharacters.Length
                );

            code.Append(
                roomCodeCharacters[index]
            );
        }

        return code.ToString();
    }

    // =========================================================
    // Room Code로 Lobby 찾기
    // =========================================================

    public bool FindLobbyByRoomCode(
        string roomCode,
        Action<CSteamID> callback)
    {
        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogError(
                "Room Code가 비어 있습니다."
            );

            callback?.Invoke(CSteamID.Nil);

            return false;
        }

        if (isRoomCodeLookupInProgress)
        {
            Debug.LogWarning(
                "이미 Room Code 조회가 진행 중입니다."
            );

            return false;
        }

        isRoomCodeLookupInProgress = true;

        roomCodeLookupCallback = callback;

        // Room Code가 정확히 일치하는 Lobby 검색
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            "room_code",
            roomCode,
            ELobbyComparison.k_ELobbyComparisonEqual
        );

        // 지역 제한 없이 검색
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(
            ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide
        );

        // 같은 코드가 있는지 하나만 확인하면 됨
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);

        SteamAPICall_t handle =
            SteamMatchmaking.RequestLobbyList();

        roomCodeLookupResult.Set(handle);

        Debug.Log(
            $"Room Code 조회 요청 | " +
            $"Room Code: {roomCode}"
        );

        return true;
    }

    private void OnRoomCodeLookup(
        LobbyMatchList_t result,
        bool ioFailure)
    {
        isRoomCodeLookupInProgress = false;

        Action<CSteamID> callback =
            roomCodeLookupCallback;

        roomCodeLookupCallback = null;

        if (ioFailure)
        {
            Debug.LogError(
                "Room Code 조회 중 Steam 통신 오류"
            );

            callback?.Invoke(CSteamID.Nil);

            return;
        }

        int lobbyCount =
            (int)result.m_nLobbiesMatching;
        Debug.Log(
            $"[Room Code Search] " +
            $"Count: {lobbyCount} | " +
            $"IO Failure: {ioFailure}"
        );


        if (lobbyCount <= 0)
        {
            Debug.Log(
                "Room Code에 해당하는 Lobby가 없습니다."
            );

            callback?.Invoke(CSteamID.Nil);

            return;
        }

        CSteamID lobbyID =
            SteamMatchmaking.GetLobbyByIndex(0);

        Debug.Log(
            $"Room Code Lobby 발견 | " +
            $"Lobby ID: {lobbyID}"
        );

        callback?.Invoke(lobbyID);
    }

    // =========================================================
    // Lobby Join
    // =========================================================

    // Room Code를 이용해서 Lobby에 참가
    public bool JoinLobbyByRoomCode(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode) ||
            roomCode.Length != roomCodeLength)
        {
            Debug.LogWarning("유효하지 않은 Room Code입니다.");
            return false;
        }

        if (hasActiveLobbyJoinRequest || isLobbyJoinInProgress)
        {
            Debug.LogWarning("이미 Lobby Join이 진행 중입니다.");
            return false;
        }

        joiningRoomCode = roomCode;
        hasActiveLobbyJoinRequest = true;
        isLobbyJoinCancelled = false;

        int requestId = ++lobbyJoinRequestId;

        // 모든 코드 검색은 최대 60초 대기
        lobbyJoinMaxWaitTime = lobbyJoinMaxWaitDuration;
        lobbyJoinRetryElapsed = 0f;
        lobbyJoinRetryTimer = 0f;
        isLobbyJoinRetryWaiting = true;
        isRoomCodeJoinRetryScheduled = false;
        retryLobbyID = CSteamID.Nil;

        OnLobbyJoinStarted?.Invoke();
        OnLobbyJoinWaitingStarted?.Invoke(lobbyJoinMaxWaitTime);
        OnLobbyJoinProgressChanged?.Invoke(0f);

        Debug.Log($"Room Code Join 요청 | Room Code: {roomCode}");

        bool lookupStarted = FindLobbyByRoomCode(
            roomCode,
            lobbyID => OnRoomCodeFoundForJoin(lobbyID, requestId));

        if (!lookupStarted)
        {
            // 첫 검색이 시작되지 않았어도 대기 상태는 유지한다.
            lobbyJoinRetryTimer = lobbyJoinRetryInterval;
        }

        return true;
    }
    private void OnRoomCodeFoundForJoin(
        CSteamID lobbyID,
        int requestId)
    {
        if (requestId != lobbyJoinRequestId ||
            isLobbyJoinCancelled ||
            !hasActiveLobbyJoinRequest)
        {
            return;
        }

        // 검색 결과가 없으면 종료하지 않고 재검색한다.
        if (lobbyID == CSteamID.Nil)
        {
            Debug.LogWarning(
                $"Room Code 검색 결과 없음. 재검색 예정: {joiningRoomCode}");

            isRoomCodeJoinRetryScheduled = true;
            lobbyJoinRetryTimer = lobbyJoinRetryInterval;
            return;
        }

        Debug.Log($"Room Code Lobby 발견 | Lobby ID: {lobbyID}");

        // 찾은 Lobby에 참가를 시도한다.
        isRoomCodeJoinRetryScheduled = false;

        bool joinStarted = JoinLobbyByLobbyID(lobbyID);

        if (!joinStarted)
        {
            lobbyJoinRetryTimer = 0.5f;
            isRoomCodeJoinRetryScheduled = true;
        }
    }

    // Lobby ID를 알고 있을 때 실제 Steam Lobby Join
    public bool JoinLobbyByLobbyID(
        CSteamID lobbyID)
    {
        if (lobbyID == CSteamID.Nil)
        {
            Debug.LogError(
                "Join하려는 Lobby ID가 유효하지 않습니다."
            );

            return false;
        }

        if (isLobbyJoinInProgress)
        {
            Debug.LogWarning(
                "이미 Lobby Join이 진행 중입니다."
            );

            return false;
        }

        isLobbyJoinInProgress = true;

        CSteamID localSteamID = SteamUser.GetSteamID();

        if (KickManager.IsBannedFromLobby(
                lobbyID,
                localSteamID,
                out long remainingSeconds))
        {
            Debug.LogWarning(
                $"재입장 제한 중입니다. 남은 시간: " +
                $"{Math.Ceiling(remainingSeconds / 60.0)}분"
            );

            return false;
        }

        SteamAPICall_t handle =
            SteamMatchmaking.JoinLobby(lobbyID);

        lobbyEnterResult.Set(handle);   

        Debug.Log(
            $"Steam Lobby Join 요청 | " +
            $"Lobby ID: {lobbyID}"
        );

        return true;
    }

    private void OnLobbyEntered(LobbyEnter_t result, bool ioFailure)
    {
        isLobbyJoinInProgress = false;

        if (isLobbyJoinCancelled)
            return;

        if (ioFailure)
        {
            Debug.LogError("Lobby 입장 중 Steam 통신 오류");
            FinishLobbyJoinRequest();
            return;
        }

        CSteamID enteredLobbyID =
            new CSteamID(result.m_ulSteamIDLobby);

        Debug.Log(
            $"LobbyEnter 결과 | Lobby ID: {enteredLobbyID} | " +
            $"Response: {result.m_EChatRoomEnterResponse}"
        );

        if (result.m_EChatRoomEnterResponse ==
            (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            currentLobbyID = enteredLobbyID;

            FinishLobbyJoinRequest();

            Debug.Log($"Lobby 입장 성공! | Lobby ID: {currentLobbyID}");
            LoadLobbyScene();
            return;
        }

        if (result.m_EChatRoomEnterResponse ==
            (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseFull)
        {
            Debug.LogWarning(
                $"Lobby가 가득 차 있습니다. 코드 재검색을 계속합니다. | " +
                $"Lobby ID: {enteredLobbyID}");

            isRoomCodeJoinRetryScheduled = true;
            lobbyJoinRetryTimer = lobbyJoinRetryInterval;
            return;
        }
        Debug.LogWarning(
            $"Lobby 입장 실패 | Response: {result.m_EChatRoomEnterResponse}"
        );

        FinishLobbyJoinRequest();
    }

    private void StartLobbyJoinRetry(CSteamID lobbyID)
    {
        if (isLobbyJoinCancelled)
            return;

        retryLobbyID = lobbyID;
        isLobbyJoinRetryWaiting = true;

        lobbyJoinRetryElapsed = 0f;
        lobbyJoinRetryTimer = lobbyJoinRetryInterval;

        Debug.Log(
            $"Lobby Join 재시도 대기 시작 | " +
            $"Lobby ID: {retryLobbyID} | " +
            $"최대 대기: {lobbyJoinMaxWaitTime:F0}초"
        );
    }

    public void CancelLobbyJoinRetry()
    {
        if (!hasActiveLobbyJoinRequest)
            return;

        isLobbyJoinCancelled = true;

        // 현재 요청의 식별자를 무효화해 늦은 콜백을 차단한다.
        lobbyJoinRequestId++;

        isLobbyJoinRetryWaiting = false;

        isRoomCodeJoinRetryScheduled = false;
        joiningRoomCode = null;
        
        retryLobbyID = CSteamID.Nil;

        lobbyJoinRetryElapsed = 0f;
        lobbyJoinRetryTimer = 0f;
        lobbyJoinMaxWaitTime = 0f;

        isLobbyJoinInProgress = false;
        hasActiveLobbyJoinRequest = false;

        OnLobbyJoinFinished?.Invoke();

        Debug.Log("Lobby Join 요청을 취소했습니다.");
    }

    private void FinishLobbyJoinRequest()
    {
        if (!hasActiveLobbyJoinRequest)
            return;

        isLobbyJoinRetryWaiting = false;

        isRoomCodeJoinRetryScheduled = false;
        joiningRoomCode = null;

        retryLobbyID = CSteamID.Nil;

        lobbyJoinRetryElapsed = 0f;
        lobbyJoinRetryTimer = 0f;
        lobbyJoinMaxWaitTime = 0f;

        isLobbyJoinInProgress = false;
        hasActiveLobbyJoinRequest = false;

        OnLobbyJoinFinished?.Invoke();
    }

    // =========================================================
    // Public Lobby 검색
    // =========================================================

    public void RequestPublicLobbyList()
    {
        // 이미 검색 중이면 새로운 검색을 시작하지 않음
        if (isLobbyListRequestInProgress)
        {
            Debug.Log(
                "Public Lobby 검색이 이미 진행 중입니다."
            );

            return;
        }

        isLobbyListRequestInProgress = true;

        // 이전 검색 조건 초기화
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            "room_type",
            "Public",
            ELobbyComparison.k_ELobbyComparisonEqual
        );

        // 빈자리가 최소 1자리 있는 Lobby만 요청
        SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(
            1
        );

        // 최대 50개까지 요청
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(
            50
        );

        SteamAPICall_t handle =
            SteamMatchmaking.RequestLobbyList();

        lobbyMatchListResult.Set(handle);

        Debug.Log(
            "Public Lobby 목록 요청"
        );
    }

    private void OnLobbyMatchList(
        LobbyMatchList_t result,
        bool ioFailure)
    {
        // 검색 종료
        isLobbyListRequestInProgress = false;

        if (ioFailure)
        {
            Debug.LogError(
                "Lobby 목록을 가져오는 중 Steam 통신 오류"
            );

            return;
        }

        publicLobbyList.Clear();

        int lobbyCount =
            (int)result.m_nLobbiesMatching;

        Debug.Log(
            $"Public Lobby 목록 수신 완료 | 총 {lobbyCount}개"
        );

        for (int i = 0; i < lobbyCount; i++)
        {
            CSteamID lobbyID =
                SteamMatchmaking.GetLobbyByIndex(i);

            string roomCode =
                SteamMatchmaking.GetLobbyData(
                    lobbyID,
                    "room_code"
                );

            string languageString =
                SteamMatchmaking.GetLobbyData(
                    lobbyID,
                    "language"
                );

            string gameModeString =
                SteamMatchmaking.GetLobbyData(
                    lobbyID,
                    "game_mode"
                );

            string stateString =
                SteamMatchmaking.GetLobbyData(
                    lobbyID,
                    "state"
                );

            if (!System.Enum.TryParse(
                    languageString,
                    out LanguageTag language))
            {
                Debug.LogWarning(
                    $"Lobby {lobbyID}: " +
                    "Language 데이터를 읽을 수 없습니다."
                );

                continue;
            }

            if (!System.Enum.TryParse(
                    gameModeString,
                    out GameMode gameMode))
            {
                Debug.LogWarning(
                    $"Lobby {lobbyID}: " +
                    "Game Mode 데이터를 읽을 수 없습니다."
                );

                continue;
            }

            if (!System.Enum.TryParse(
                    stateString,
                    out LobbyState state))
            {
                Debug.LogWarning(
                    $"Lobby {lobbyID}: " +
                    "State 데이터를 읽을 수 없습니다."
                );

                continue;
            }

            int currentPlayers =
                SteamMatchmaking.GetNumLobbyMembers(
                    lobbyID
                );

            int maxPlayers =
                SteamMatchmaking.GetLobbyMemberLimit(
                    lobbyID
                );

            LobbyData lobbyData =
                new LobbyData(
                    lobbyID,
                    roomCode,
                    language,
                    gameMode,
                    currentPlayers,
                    maxPlayers,
                    state
                );

            publicLobbyList.Add(lobbyData);

            Debug.Log(
                $"Public Lobby [{i}] | " +
                $"ID: {lobbyData.lobbyID} | " +
                $"Room Code: {lobbyData.roomCode} | " +
                $"Language: {lobbyData.language} | " +
                $"Game Mode: {lobbyData.gameMode} | " +
                $"Players: " +
                $"{lobbyData.currentPlayers}/" +
                $"{lobbyData.maxPlayers} | " +
                $"State: {lobbyData.state}"
            );
        }

        OnPublicLobbyListUpdated?.Invoke(
            publicLobbyList
        );
    }

    // =========================================================
    // 개별 Lobby Data 갱신
    // =========================================================

    public bool RequestLobbyData(CSteamID lobbyID)
    {
        if (lobbyID == CSteamID.Nil)
        {
            Debug.LogError(
                "유효하지 않은 Lobby ID입니다."
            );

            return false;
        }

        bool success =
            SteamMatchmaking.RequestLobbyData(
                lobbyID
            );

        if (!success)
        {
            Debug.LogWarning(
                $"Lobby Data 요청 실패 | Lobby ID: {lobbyID}"
            );

            return false;
        }

        Debug.Log(
            $"Lobby Data 갱신 요청 | Lobby ID: {lobbyID}"
        );

        return true;
    }

    private void HandleLobbyDataUpdated(
        LobbyDataUpdate_t callback)
    {
        Debug.Log(
            $"Lobby Data 갱신 결과 | " +
            $"Lobby ID: " +
            $"{callback.m_ulSteamIDLobby} | " +
            $"Success: " +
            $"{callback.m_bSuccess}"
        );

        OnLobbyDataUpdated?.Invoke(
            callback
        );
    }

    // =========================================================

    public IReadOnlyList<LobbyData> PublicLobbyList
    {
        get
        {
            return publicLobbyList;
        }
    }

    public void RefreshLobbyPlayers()
    {
        if (currentLobbyID == CSteamID.Nil)
        {
            Debug.LogWarning("현재 Lobby가 없습니다.");
            return;
        }

        CSteamID lobbyOwner =
            SteamMatchmaking.GetLobbyOwner(currentLobbyID);

        int memberCount =
            SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);

        List<LobbyPlayerData> players =
            new List<LobbyPlayerData>();

        HashSet<ulong> currentMemberIDs =
            new HashSet<ulong>();

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID playerID =
                SteamMatchmaking.GetLobbyMemberByIndex(
                    currentLobbyID,
                    i
                );

            ulong steamIDValue = playerID.m_SteamID;

            currentMemberIDs.Add(steamIDValue);

            string playerName =
                SteamFriends.GetFriendPersonaName(playerID);

            bool isHost = playerID == lobbyOwner;

            LobbyPlayerData playerData;

            // 기존 멤버라면 데이터를 재사용한다.
            if (lobbyPlayerDataBySteamId.TryGetValue(
                    steamIDValue,
                    out playerData))
            {
                playerData.steamName = playerName;
                playerData.isHost = isHost;
            }
            else
            {
                // 새로 들어온 플레이어는 0점, In Lobby로 시작한다.
                playerData = new LobbyPlayerData(
                    playerID,
                    playerName,
                    false,
                    isHost
                );

                lobbyPlayerDataBySteamId.Add(
                    steamIDValue,
                    playerData
                );
            }

            players.Add(playerData);

            Debug.Log(
                $"Lobby Player [{i}] | " +
                $"Steam ID: {playerID} | " +
                $"Name: {playerName} | " +
                $"Host: {isHost} | " +
                $"Status: {playerData.status} | " +
                $"Score: {playerData.score}"
            );
        }

        // 현재 Lobby에서 사라진 플레이어의 데이터 제거
        List<ulong> departedPlayerIDs =
            new List<ulong>();

        foreach (ulong steamIDValue
                 in lobbyPlayerDataBySteamId.Keys)
        {
            if (!currentMemberIDs.Contains(steamIDValue))
            {
                departedPlayerIDs.Add(steamIDValue);
            }
        }

        foreach (ulong steamIDValue in departedPlayerIDs)
        {
            lobbyPlayerDataBySteamId.Remove(steamIDValue);
        }

        OnLobbyPlayersUpdated?.Invoke(players);
    }

    private void HandleLobbyChatUpdate(
        LobbyChatUpdate_t callback)
    {
        if (currentLobbyID == CSteamID.Nil)
        {
            return;
        }

        CSteamID changedLobbyID =
            new CSteamID(
                callback.m_ulSteamIDLobby
            );

        if (changedLobbyID != currentLobbyID)
        {
            return;
        }

        Debug.Log(
            $"Lobby Member 변경 감지 | " +
            $"Lobby ID: {changedLobbyID}"
        );

        RefreshLobbyPlayers();
    }

    
    // =========================================================
    // Game Settings 동기화
    // =========================================================

    private const string settingsInitializedKey = "settings_initialized";
    private const string selectedMapsKey = "selected_maps";
    private const string randomMapSelection = "RANDOM"; 

    public bool IsCurrentLobbyHost()
    {
        if (currentLobbyID == CSteamID.Nil)
            return false;

        return SteamMatchmaking.GetLobbyOwner(currentLobbyID)
            == SteamUser.GetSteamID();
    }

    public bool SaveGameSettings(LobbyGameSettings settings)
    {
        if (settings == null)
        {
            Debug.LogError("저장할 Game Settings가 없습니다.");
            return false;
        }

        if (currentLobbyID == CSteamID.Nil || !IsCurrentLobbyHost())
        {
            Debug.LogWarning("Host만 Game Settings를 저장할 수 있습니다.");
            return false;
        }

        bool success = true;

        success &= SetGameSettingData("hunter_ratio", settings.hunterRatio.ToString());
        success &= SetGameSettingData("hiding_time", settings.hidingTime.ToString());
        success &= SetGameSettingData("round_number", settings.roundNumber.ToString());
        success &= SetGameSettingData("round_duration", settings.roundDuration.ToString());
        success &= SetGameSettingData("forced_taunt_enabled", settings.forcedTauntEnabled.ToString());
        success &= SetGameSettingData("forced_taunt_interval", settings.forcedTauntInterval.ToString());
        success &= SetGameSettingData("freeze_time", settings.freezeTime.ToString());
        success &= SaveSelectedMaps(settings.selectedMapSceneNames);

        if (success)
        {
            success &= SetGameSettingData(settingsInitializedKey, "true");
        }

        if (success)
            Debug.Log("Game Settings를 Steam Lobby에 저장했습니다.");

        return success;
    }

    private bool SetGameSettingData(string key, string value)
    {
        bool result = SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            key,
            value
        );

        if (!result)
            Debug.LogError($"Lobby 설정 저장 실패 | Key: {key}");

        return result;
    }

    public bool TryLoadGameSettings(out LobbyGameSettings settings)
    {
        settings = null;

        if (currentLobbyID == CSteamID.Nil)
            return false;

        string initialized = SteamMatchmaking.GetLobbyData(
            currentLobbyID,
            settingsInitializedKey
        );

        if (initialized != "true")
            return false;

        LobbyGameSettings loaded = new LobbyGameSettings();

        if (!int.TryParse(SteamMatchmaking.GetLobbyData(currentLobbyID, "hunter_ratio"), out loaded.hunterRatio) ||
            !int.TryParse(SteamMatchmaking.GetLobbyData(currentLobbyID, "hiding_time"), out loaded.hidingTime) ||
            !int.TryParse(SteamMatchmaking.GetLobbyData(currentLobbyID, "round_number"), out loaded.roundNumber) ||
            !int.TryParse(SteamMatchmaking.GetLobbyData(currentLobbyID, "round_duration"), out loaded.roundDuration) ||
            !bool.TryParse(SteamMatchmaking.GetLobbyData(currentLobbyID, "forced_taunt_enabled"), out loaded.forcedTauntEnabled) ||
            !int.TryParse(SteamMatchmaking.GetLobbyData(currentLobbyID, "forced_taunt_interval"), out loaded.forcedTauntInterval) ||
            !int.TryParse(SteamMatchmaking.GetLobbyData(currentLobbyID, "freeze_time"), out loaded.freezeTime))
        {
            Debug.LogWarning("Steam Lobby의 Game Settings를 읽지 못했습니다.");
            return false;
        }

        List<string> selectedMaps;

        if (!TryLoadSelectedMaps(out selectedMaps))
        {
            Debug.LogWarning("Steam Lobby의 Map Selection을 읽지 못했습니다.");
            return false;
        }

        loaded.selectedMapSceneNames = selectedMaps;

        settings = loaded;

        Debug.Log("Steam Lobby의 Game Settings를 불러왔습니다.");
        return true;
    }
    
    // =========================================================
    // Leave Lobby
    // =========================================================

    public void LeaveCurrentLobby(string destinationScene)
    {
        CancelLobbyJoinRetry();

        if (currentLobbyID == CSteamID.Nil)
        {
            Debug.LogWarning("나갈 Lobby가 없습니다.");
            SceneManager.LoadScene(destinationScene);
            return;
        }

        CSteamID lobbyToLeave = currentLobbyID;

        // 로컬 Lobby 상태 초기화
        currentLobbyID = CSteamID.Nil;
        currentRoomCode = null;
        currentRoomSettings = null;

        lobbyPlayerDataBySteamId.Clear();

        // Steam Lobby 나가기
        SteamMatchmaking.LeaveLobby(lobbyToLeave);

        Debug.Log($"Lobby 나가기 완료 | Lobby ID: {lobbyToLeave}");

        // 메인 메뉴 씬으로 이동
        SceneManager.LoadScene(destinationScene);
    }
    // Map 관련
    public bool SaveSelectedMaps(IReadOnlyList<string> selectedMapSceneNames)
    {
        if (currentLobbyID == CSteamID.Nil)
        {
            Debug.LogWarning("현재 Lobby가 없습니다.");
            return false;
        }

        if (!IsCurrentLobbyHost())
        {
            Debug.LogWarning("Host만 Map Selection을 변경할 수 있습니다.");
            return false;
        }

        if (selectedMapSceneNames == null ||
            selectedMapSceneNames.Count == 0)
        {
            return SteamMatchmaking.SetLobbyData(
                currentLobbyID,
                selectedMapsKey,
                randomMapSelection
            );
        }

        List<string> validNames = new List<string>();

        foreach (string sceneName in selectedMapSceneNames)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                continue;
            }

            if (!validNames.Contains(sceneName))
            {
                validNames.Add(sceneName);
            }
        }

        if (validNames.Count == 0)
        {
            return SteamMatchmaking.SetLobbyData(
                currentLobbyID,
                selectedMapsKey,
                randomMapSelection
            );
        }

        string value = string.Join("|", validNames);

        return SteamMatchmaking.SetLobbyData(
            currentLobbyID,
            selectedMapsKey,
            value
        );
    }

    public bool TryLoadSelectedMaps(out List<string> selectedMapSceneNames)
    {
        selectedMapSceneNames = new List<string>();

        if (currentLobbyID == CSteamID.Nil)
        {
            return false;
        }

        string value = SteamMatchmaking.GetLobbyData(
            currentLobbyID,
            selectedMapsKey
        );

        // 데이터가 없거나 RANDOM이면
        // Random 선택 상태로 취급
        if (string.IsNullOrEmpty(value) ||
            value == randomMapSelection)
        {
            return true;
        }

        string[] split = value.Split('|');

        foreach (string sceneName in split)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                continue;
            }

            if (!selectedMapSceneNames.Contains(sceneName))
            {
                selectedMapSceneNames.Add(sceneName);
            }
        }

        return true;
    }

    public bool TryGetCurrentRoomType(out RoomType roomType)
    {
        roomType = default;

        if (currentLobbyID == CSteamID.Nil)
            return false;

        string roomTypeString = SteamMatchmaking.GetLobbyData(
            currentLobbyID,
            "room_type"
        );

        return System.Enum.TryParse(
            roomTypeString,
            out roomType
        );
    }

    public bool TryGetLobbyPlayerData(
        ulong steamId,
        out LobbyPlayerData playerData)
    {
        return lobbyPlayerDataBySteamId.TryGetValue(
            steamId,
            out playerData
        );
    }

    public bool SetPlayerScore(ulong steamId, int score)
    {
        if (currentLobbyID == CSteamID.Nil)
            return false;

        if (!lobbyPlayerDataBySteamId.TryGetValue(
                steamId,
                out LobbyPlayerData playerData))
        {
            Debug.LogWarning(
                $"점수 변경 실패: Lobby 플레이어를 찾을 수 없습니다. ID: {steamId}"
            );
            return false;
        }

        playerData.score = score;

        // 기존 UI 구독자들에게 변경된 플레이어 목록을 알린다.
        RefreshLobbyPlayers();

        Debug.Log(
            $"점수 갱신 | {playerData.steamName}: {playerData.score}"
        );

        return true;
    }
}