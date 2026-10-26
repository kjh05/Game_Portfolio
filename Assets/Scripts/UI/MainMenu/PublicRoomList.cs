using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

public class PublicRoomList : MonoBehaviour
{
    [Header("Room Rows")]
    [SerializeField] private RoomRow[] roomRows;

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 5f;

    [Header("Join Waiting UI")]
    [SerializeField] private JoinWaitingUI joinWaitingUI;

    private Coroutine refreshCoroutine;

    private RoomRow selectedRoomRow;
    private LobbyData selectedLobbyData;

    // 현재 진행 중인 Lobby 검색이
    // 수동 Refresh에 의한 것인지 여부
    private bool isManualRefresh;

    // --------------------------------------------------
    // 자동 Refresh 검증용 데이터
    // --------------------------------------------------

    // 가장 최근 자동 Refresh에서 받은 최대 50개 검색 결과
    private List<LobbyData> latestAutoSearchResults =
        new List<LobbyData>();

    // RequestLobbyData()를 요청하고 결과를 기다리는 Lobby들
    private HashSet<CSteamID> pendingLobbyValidation =
        new HashSet<CSteamID>();

    // 현재 자동 검증 과정이 진행 중인지
    private bool isAutoValidationInProgress;

    private void Awake()
    {
        ClearRows();

        if (roomRows == null)
            return;

        for (int i = 0; i < roomRows.Length; i++)
        {
            if (roomRows[i] != null)
            {
                roomRows[i].OnRowClicked += HandleRowClicked;
            }
        }
    }

    private void OnEnable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnPublicLobbyListUpdated
                += DisplayLobbyList;

            SteamLobby.Instance.OnLobbyDataUpdated
                += HandleLobbyDataUpdated;
        }

        refreshCoroutine = StartCoroutine(AutoRefresh());
    }

    private void OnDisable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnPublicLobbyListUpdated
                -= DisplayLobbyList;

            SteamLobby.Instance.OnLobbyDataUpdated
                -= HandleLobbyDataUpdated;
        }

        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }

        pendingLobbyValidation.Clear();
        isAutoValidationInProgress = false;
    }

    private void OnDestroy()
    {
        if (roomRows == null)
            return;

        for (int i = 0; i < roomRows.Length; i++)
        {
            if (roomRows[i] != null)
            {
                roomRows[i].OnRowClicked -= HandleRowClicked;
            }
        }
    }

    // ==================================================
    // 자동 Refresh
    // ==================================================

    private IEnumerator AutoRefresh()
    {
        yield return new WaitUntil(
            () => SteamLobby.Instance != null
        );

        RequestAutoRefresh();

        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);

            RequestAutoRefresh();
        }
    }

    private void RequestAutoRefresh()
    {
        if (SteamLobby.Instance == null)
        {
            Debug.LogError(
                "SteamLobby를 찾을 수 없습니다."
            );

            return;
        }

        // 이전 자동 검증이 아직 끝나지 않았다면
        // 새로운 자동 검색을 시작하지 않음
        if (isAutoValidationInProgress)
        {
            Debug.Log(
                "자동 Lobby 검증이 아직 진행 중입니다. " +
                "이번 자동 Refresh를 건너뜁니다."
            );

            return;
        }

        // Steam Lobby 검색 자체가 이미 진행 중이면
        // 새로운 검색을 시작하지 않음
        if (SteamLobby.Instance.IsLobbyListRequestInProgress)
        {
            Debug.Log(
                "Public Lobby 검색이 이미 진행 중입니다. " +
                "이번 자동 Refresh를 건너뜁니다."
            );

            return;
        }

        isManualRefresh = false;

        SteamLobby.Instance.RequestPublicLobbyList();
    }

    // ==================================================
    // 수동 Refresh
    // ==================================================

    // REFRESH 버튼에서 호출
    public void RefreshRoomList()
    {
        if (SteamLobby.Instance == null)
        {
            Debug.LogError(
                "SteamLobby를 찾을 수 없습니다."
            );

            return;
        }

        // 자동 검증 중에는 수동 Refresh와 결과가
        // 서로 꼬이지 않도록 잠시 막음
        if (isAutoValidationInProgress)
        {
            Debug.Log(
                "자동 Lobby 검증이 진행 중입니다. " +
                "잠시 후 다시 Refresh해주세요."
            );

            return;
        }

        // 이미 검색 중이면 새로운 검색을 시작하지 않음
        if (SteamLobby.Instance.IsLobbyListRequestInProgress)
        {
            Debug.Log(
                "Public Lobby 검색이 이미 진행 중입니다."
            );

            return;
        }

        isManualRefresh = true;

        SteamLobby.Instance.RequestPublicLobbyList();

        Debug.Log(
            "Public Room 수동 Refresh 요청"
        );
    }

    // ==================================================
    // Lobby 검색 결과 수신
    // ==================================================

    private void DisplayLobbyList(
        IReadOnlyList<LobbyData> lobbyList)
    {
        if (isManualRefresh)
        {
            DisplayManualRefresh(lobbyList);
        }
        else
        {
            StartAutoRefreshValidation(lobbyList);
        }

        isManualRefresh = false;
    }

    // ==================================================
    // 자동 Refresh
    // 검증 시작
    // ==================================================

    private void StartAutoRefreshValidation(
        IReadOnlyList<LobbyData> lobbyList)
    {
        if (roomRows == null ||
            roomRows.Length == 0)
        {
            return;
        }

        latestAutoSearchResults =
            new List<LobbyData>(lobbyList);

        pendingLobbyValidation.Clear();

        isAutoValidationInProgress = true;

        int displayedLobbyCount = 0;
        int searchResultMatchCount = 0;
        int validationRequestCount = 0;

        // ----------------------------------------------
        // 현재 화면의 Lobby들을 하나씩 확인
        // ----------------------------------------------

        for (int i = 0; i < roomRows.Length; i++)
        {
            RoomRow row = roomRows[i];

            if (row == null)
                continue;

            // 선택된 방은 자동 검증 대상에서 제외
            if (row == selectedRoomRow)
                continue;

            LobbyData currentLobby =
                row.GetLobbyData();

            // 빈 Row
            if (currentLobby == null)
                continue;

            displayedLobbyCount++;

            // ------------------------------------------
            // 새 검색 결과에 현재 방이 존재하는 경우
            // ------------------------------------------

            LobbyData refreshedLobby =
                FindLobbyByID(
                    latestAutoSearchResults,
                    currentLobby.lobbyID
                );

            if (refreshedLobby != null)
            {
                // 같은 Row에 최신 데이터만 적용
                row.SetData(refreshedLobby);

                searchResultMatchCount++;

                continue;
            }

            // ------------------------------------------
            // 새 검색 결과에 없는 경우
            // ------------------------------------------
            //
            // 검색 결과에 없다고 바로 제거하지 않는다.
            //
            // RequestLobbyData()로 실제 존재 여부 확인
            // ------------------------------------------

            bool requestSuccess =
                SteamLobby.Instance.RequestLobbyData(
                    currentLobby.lobbyID
                );

            if (requestSuccess)
            {
                pendingLobbyValidation.Add(
                    currentLobby.lobbyID
                );

                validationRequestCount++;
            }
            else
            {
                // Steam에 요청 자체를 보내지 못한 경우
                // 일단 해당 Row를 비움
                row.Clear();

                Debug.LogWarning(
                    $"Lobby Data 요청 실패로 방 제거 | " +
                    $"Lobby ID: {currentLobby.lobbyID}"
                );
            }
        }

        Debug.Log(
            $"Public Room 자동 Refresh 검사 시작 | " +
            $"현재 표시: {displayedLobbyCount}개 | " +
            $"검색 결과에서 확인: {searchResultMatchCount}개 | " +
            $"개별 검증 요청: {validationRequestCount}개"
        );

        // 검증할 Lobby가 하나도 없다면
        // 바로 빈 Row를 채운다.
        if (pendingLobbyValidation.Count == 0)
        {
            CompleteAutoRefreshValidation();
        }
    }

    // ==================================================
    // 개별 Lobby Data 검증 결과
    // ==================================================

    private void HandleLobbyDataUpdated(
        LobbyDataUpdate_t callback)
    {
        CSteamID lobbyID =
            new CSteamID(
                callback.m_ulSteamIDLobby
            );

        // 우리가 이번 자동 Refresh에서
        // 검증 요청한 Lobby가 아니면 무시
        if (!pendingLobbyValidation.Contains(lobbyID))
        {
            return;
        }

        pendingLobbyValidation.Remove(lobbyID);

        RoomRow targetRow =
            FindRowByLobbyID(lobbyID);

        // Row가 이미 없어졌거나
        // 선택된 Row라면 결과만 소비
        if (targetRow == null ||
            targetRow == selectedRoomRow)
        {
            CheckAutoRefreshValidationComplete();
            return;
        }

        // ----------------------------------------------
        // Lobby 자체가 존재하지 않음
        // ----------------------------------------------

        if (callback.m_bSuccess == 0)
        {
            targetRow.Clear();

            Debug.Log(
                $"Lobby가 존재하지 않아 Row 제거 | " +
                $"Lobby ID: {lobbyID}"
            );

            CheckAutoRefreshValidationComplete();

            return;
        }

        // ----------------------------------------------
        // Lobby가 존재함
        // 최신 Lobby Data를 읽는다.
        // ----------------------------------------------

        if (!TryCreateLobbyData(
                lobbyID,
                out LobbyData refreshedLobby))
        {
            // Data를 제대로 읽지 못한 경우
            // 일단 기존 방을 유지한다.
            Debug.LogWarning(
                $"Lobby Data를 읽지 못했습니다. " +
                $"기존 Row 유지 | Lobby ID: {lobbyID}"
            );

            CheckAutoRefreshValidationComplete();

            return;
        }

        // ----------------------------------------------
        // Public Lobby가 아니거나
        // 빈자리가 없는 경우
        // ----------------------------------------------

        if (refreshedLobby.currentPlayers >=
            refreshedLobby.maxPlayers)
        {
            targetRow.Clear();

            Debug.Log(
                $"Lobby가 가득 차서 Row 제거 | " +
                $"Lobby ID: {lobbyID} | " +
                $"Players: " +
                $"{refreshedLobby.currentPlayers}/" +
                $"{refreshedLobby.maxPlayers}"
            );

            CheckAutoRefreshValidationComplete();

            return;
        }

        if (refreshedLobby.roomTypeIsPublic == false)
        {
            targetRow.Clear();

            Debug.Log(
                $"Public Lobby가 아니므로 Row 제거 | " +
                $"Lobby ID: {lobbyID}"
            );

            CheckAutoRefreshValidationComplete();

            return;
        }

        // ----------------------------------------------
        // 살아있는 Lobby
        // 기존 Row 위치를 그대로 유지하고
        // 최신 데이터만 반영
        // ----------------------------------------------

        targetRow.SetData(refreshedLobby);

        Debug.Log(
            $"Lobby 검증 성공 | " +
            $"Room Code: {refreshedLobby.roomCode} | " +
            $"Players: " +
            $"{refreshedLobby.currentPlayers}/" +
            $"{refreshedLobby.maxPlayers} | " +
            $"State: {refreshedLobby.state}"
        );

        CheckAutoRefreshValidationComplete();
    }

    private void CheckAutoRefreshValidationComplete()
    {
        if (pendingLobbyValidation.Count > 0)
        {
            return;
        }

        CompleteAutoRefreshValidation();
    }

    // ==================================================
    // 자동 Refresh 완료
    // 빈 Row만 새 Lobby로 채움
    // ==================================================

    private void CompleteAutoRefreshValidation()
    {
        if (!isAutoValidationInProgress)
        {
            return;
        }

        isAutoValidationInProgress = false;

        HashSet<CSteamID> displayedLobbyIDs =
            new HashSet<CSteamID>();

        // ----------------------------------------------
        // 현재 화면에 살아있는 Lobby ID 기록
        // ----------------------------------------------

        for (int i = 0; i < roomRows.Length; i++)
        {
            RoomRow row = roomRows[i];

            if (row == null)
                continue;

            LobbyData data =
                row.GetLobbyData();

            if (data == null)
                continue;

            displayedLobbyIDs.Add(
                data.lobbyID
            );
        }

        // ----------------------------------------------
        // 새 후보 Lobby 만들기
        // ----------------------------------------------

        List<LobbyData> replacementLobbies =
            new List<LobbyData>();

        for (int i = 0;
             i < latestAutoSearchResults.Count;
             i++)
        {
            LobbyData lobby =
                latestAutoSearchResults[i];

            if (lobby == null)
                continue;

            // 이미 화면에 있는 Lobby는 제외
            if (displayedLobbyIDs.Contains(
                    lobby.lobbyID))
            {
                continue;
            }

            // 선택된 Lobby도 제외
            if (selectedLobbyData != null &&
                lobby.lobbyID ==
                selectedLobbyData.lobbyID)
            {
                continue;
            }

            // 혹시 검색 결과가 이미 full이라면 제외
            if (lobby.currentPlayers >=
                lobby.maxPlayers)
            {
                continue;
            }

            replacementLobbies.Add(lobby);
        }

        // ----------------------------------------------
        // 빈 Row에만 새 Lobby 삽입
        // ----------------------------------------------

        int replacementIndex = 0;
        int filledCount = 0;

        for (int i = 0; i < roomRows.Length; i++)
        {
            RoomRow row = roomRows[i];

            if (row == null)
                continue;

            // 선택된 Row는 절대 건드리지 않음
            if (row == selectedRoomRow)
                continue;

            // 이미 살아있는 Lobby가 있는 Row도 건드리지 않음
            if (row.GetLobbyData() != null)
                continue;

            if (replacementIndex <
                replacementLobbies.Count)
            {
                row.SetData(
                    replacementLobbies[
                        replacementIndex
                    ]
                );

                replacementIndex++;
                filledCount++;
            }
        }

        Debug.Log(
            $"Public Room 자동 Refresh 완료 | " +
            $"검색 결과: {latestAutoSearchResults.Count}개 | " +
            $"새 방 후보: {replacementLobbies.Count}개 | " +
            $"빈 Row 보충: {filledCount}개"
        );
    }

    // ==================================================
    // Lobby Data 생성
    // ==================================================

    private bool TryCreateLobbyData(
        CSteamID lobbyID,
        out LobbyData lobbyData)
    {
        lobbyData = null;

        string roomCode =
            SteamMatchmaking.GetLobbyData(
                lobbyID,
                "room_code"
            );

        string roomTypeString =
            SteamMatchmaking.GetLobbyData(
                lobbyID,
                "room_type"
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

            return false;
        }

        if (!System.Enum.TryParse(
                gameModeString,
                out GameMode gameMode))
        {
            Debug.LogWarning(
                $"Lobby {lobbyID}: " +
                "Game Mode 데이터를 읽을 수 없습니다."
            );

            return false;
        }

        if (!System.Enum.TryParse(
                stateString,
                out LobbyState state))
        {
            Debug.LogWarning(
                $"Lobby {lobbyID}: " +
                "State 데이터를 읽을 수 없습니다."
            );

            return false;
        }

        int currentPlayers =
            SteamMatchmaking.GetNumLobbyMembers(
                lobbyID
            );

        int maxPlayers =
            SteamMatchmaking.GetLobbyMemberLimit(
                lobbyID
            );

        bool isPublic =
            roomTypeString == "Public";

        lobbyData =
            new LobbyData(
                lobbyID,
                roomCode,
                language,
                gameMode,
                currentPlayers,
                maxPlayers,
                state
            );

        // LobbyData에는 아직 roomType이 없기 때문에
        // 아래 별도 확장 프로퍼티를 사용할 수 있도록
        // 현재 Public 여부를 저장
        lobbyData.roomTypeIsPublic = isPublic;

        return true;
    }

    // ==================================================
    // 검색 결과에서 Lobby 찾기
    // ==================================================

    private LobbyData FindLobbyByID(
        IReadOnlyList<LobbyData> lobbyList,
        CSteamID lobbyID)
    {
        for (int i = 0;
             i < lobbyList.Count;
             i++)
        {
            if (lobbyList[i] == null)
                continue;

            if (lobbyList[i].lobbyID ==
                lobbyID)
            {
                return lobbyList[i];
            }
        }

        return null;
    }

    // ==================================================
    // 현재 Row에서 Lobby 찾기
    // ==================================================

    private RoomRow FindRowByLobbyID(
        CSteamID lobbyID)
    {
        if (roomRows == null)
            return null;

        for (int i = 0; i < roomRows.Length; i++)
        {
            RoomRow row = roomRows[i];

            if (row == null)
                continue;

            LobbyData data =
                row.GetLobbyData();

            if (data == null)
                continue;

            if (data.lobbyID == lobbyID)
            {
                return row;
            }
        }

        return null;
    }

    // ==================================================
    // 수동 Refresh
    // ==================================================

    private void DisplayManualRefresh(
        IReadOnlyList<LobbyData> lobbyList)
    {
        if (roomRows == null ||
            roomRows.Length == 0)
        {
            return;
        }

        // 현재 화면에 표시되고 있는 Lobby들을 기록
        List<LobbyData> currentLobbies =
            new List<LobbyData>();

        for (int i = 0; i < roomRows.Length; i++)
        {
            if (roomRows[i] == null)
                continue;

            LobbyData data =
                roomRows[i].GetLobbyData();

            if (data != null)
            {
                currentLobbies.Add(data);
            }
        }

        // 새로운 방
        List<LobbyData> newLobbies =
            new List<LobbyData>();

        // 기존 방
        List<LobbyData> oldLobbies =
            new List<LobbyData>();

        for (int i = 0; i < lobbyList.Count; i++)
        {
            LobbyData lobby =
                lobbyList[i];

            // 선택된 방은 별도로 유지
            if (selectedLobbyData != null &&
                lobby.lobbyID ==
                selectedLobbyData.lobbyID)
            {
                continue;
            }

            bool wasAlreadyDisplayed = false;

            for (int j = 0;
                 j < currentLobbies.Count;
                 j++)
            {
                if (currentLobbies[j].lobbyID ==
                    lobby.lobbyID)
                {
                    wasAlreadyDisplayed = true;
                    break;
                }
            }

            if (wasAlreadyDisplayed)
            {
                oldLobbies.Add(lobby);
            }
            else
            {
                newLobbies.Add(lobby);
            }
        }

        // 새 방들을 랜덤하게 섞음
        Shuffle(newLobbies);

        // 기존 방들도 랜덤하게 섞음
        Shuffle(oldLobbies);

        // 새로운 방을 최우선으로 채움
        List<LobbyData> resultLobbies =
            new List<LobbyData>();

        for (int i = 0;
             i < newLobbies.Count &&
             resultLobbies.Count <
             GetAvailableRowCount();
             i++)
        {
            resultLobbies.Add(
                newLobbies[i]
            );
        }

        // 새 방이 부족하면 기존 방 사용
        for (int i = 0;
             i < oldLobbies.Count &&
             resultLobbies.Count <
             GetAvailableRowCount();
             i++)
        {
            resultLobbies.Add(
                oldLobbies[i]
            );
        }

        // 실제 Row에 적용
        int resultIndex = 0;

        for (int i = 0; i < roomRows.Length; i++)
        {
            RoomRow row = roomRows[i];

            if (row == null)
                continue;

            // 선택된 Row는 완전히 유지
            if (row == selectedRoomRow)
                continue;

            if (resultIndex < resultLobbies.Count)
            {
                row.SetData(
                    resultLobbies[resultIndex]
                );

                resultIndex++;
            }
            else
            {
                row.Clear();
            }
        }

        Debug.Log(
            $"Public Room List 수동 Refresh | " +
            $"검색 결과: {lobbyList.Count}개 | " +
            $"새로운 방: {newLobbies.Count}개 | " +
            $"기존 방: {oldLobbies.Count}개 | " +
            $"표시: {resultIndex}개"
        );
    }

    private int GetAvailableRowCount()
    {
        int count = 0;

        if (roomRows == null)
            return 0;

        for (int i = 0; i < roomRows.Length; i++)
        {
            if (roomRows[i] != null)
            {
                count++;
            }
        }

        if (selectedRoomRow != null)
        {
            count--;
        }

        return Mathf.Max(0, count);
    }

    private void Shuffle(List<LobbyData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex =
                UnityEngine.Random.Range(
                    0,
                    i + 1
                );

            LobbyData temp = list[i];

            list[i] = list[randomIndex];

            list[randomIndex] = temp;
        }
    }

    // ==================================================
    // Row 선택
    // ==================================================

    private void HandleRowClicked(
        RoomRow clickedRow)
    {
        if (clickedRow == null)
            return;

        if (clickedRow.GetLobbyData() == null)
            return;

        if (selectedRoomRow != null &&
            selectedRoomRow != clickedRow)
        {
            selectedRoomRow.SetSelected(false);
        }

        selectedRoomRow = clickedRow;

        selectedLobbyData =
            clickedRow.GetLobbyData();

        selectedRoomRow.SetSelected(true);

        Debug.Log(
            $"Room 선택 | " +
            $"Room Code: " +
            $"{selectedLobbyData.roomCode}"
        );
    }

    // ==================================================
    // 초기화
    // ==================================================

    private void ClearRows()
    {
        if (selectedRoomRow != null)
        {
            selectedRoomRow.SetSelected(false);
        }

        selectedRoomRow = null;
        selectedLobbyData = null;

        if (roomRows == null)
            return;

        for (int i = 0; i < roomRows.Length; i++)
        {
            if (roomRows[i] != null)
            {
                roomRows[i].Clear();
            }
        }
    }
    // ==================================================
    // 선택한 Public Room Join
    // ==================================================

    public void JoinSelectedRoom()
    {
        if (selectedRoomRow == null || selectedLobbyData == null)
        {
            WarningPanelController warningController =
                FindFirstObjectByType<WarningPanelController>();

            if (warningController != null)
            {
                warningController.ShowSelectWarning();
            }
            else
            {
                Debug.LogWarning("SELECT A ROOM");
            }

            return;
        }

        if (selectedLobbyData == null)
        {
            Debug.LogWarning(
                "Join할 Public Room이 선택되지 않았습니다."
            );

            return;
        }

        if (SteamLobby.Instance == null)
        {
            Debug.LogError(
                "SteamLobby를 찾을 수 없습니다."
            );

            return;
        }

        string roomCode =
            selectedLobbyData.roomCode;

        Debug.Log(
            $"Public Room Join 요청 | " +
            $"Room Code: {roomCode}"
        );

        if (joinWaitingUI == null)
        {
            Debug.LogError("JoinWaitingUI가 연결되지 않았습니다.");
            return;
        }

        joinWaitingUI.BeginJoin(roomCode);
    }
}