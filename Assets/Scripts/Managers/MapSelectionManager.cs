
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapSelectionManager : MonoBehaviour
{
    public static MapSelectionManager Instance { get; private set; }

    [Header("Map Selection")]
    [SerializeField] private MapListInnerPanel mapListInnerPanel;

    private const string StartScenePrefix = "CC_MAP_START";

    private bool isLoadingScene;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (SteamNetworkMessenger.Instance != null)
            SteamNetworkMessenger.Instance.OnMessageReceived += HandleMessage;
        else
            Debug.LogError("MapSelectionManager: SteamNetworkMessenger가 없습니다.");
    }

    private void OnDestroy()
    {
        if (SteamNetworkMessenger.Instance != null)
            SteamNetworkMessenger.Instance.OnMessageReceived -= HandleMessage;

        if (Instance == this)
            Instance = null;
    }

    public void StartGame()
    {
        if (isLoadingScene)
            return;

        if (SteamLobby.Instance == null ||
            !SteamLobby.Instance.IsCurrentLobbyHost())
        {
            Debug.LogWarning("맵 선택은 로비 호스트만 실행할 수 있습니다.");
            return;
        }

        if (mapListInnerPanel == null)
        {
            Debug.LogError("MapSelectionManager: MapListInnerPanel이 연결되지 않았습니다.");
            return;
        }

        if (!TryChooseMap(out string sceneName))
        {
            Debug.LogError(
                "시작할 맵을 결정하지 못했습니다. 등록된 맵과 Scene 이름을 확인하세요."
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"맵 씬 '{sceneName}'을 로드할 수 없습니다. " +
                "Build Settings에 씬이 등록되어 있는지 확인하세요."
            );
            return;
        }

        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        string message =
            $"{StartScenePrefix}|{lobbyID.m_SteamID}|{sceneName}";

        BroadcastToLobby(message);

        LoadMapScene(sceneName);
    }

    private bool TryChooseMap(out string sceneName)
    {
        sceneName = null;

        if (mapListInnerPanel == null)
            return false;

        // UI 생성 여부와 관계없이 등록된 맵 원본 목록을 가져온다.
        List<string> allSceneNames =
            mapListInnerPanel.GetRegisteredMapSceneNames();

        if (allSceneNames.Count == 0)
        {
            Debug.LogError(
                "MapSelectionManager: 등록된 맵이 없습니다. " +
                "MapListInnerPanel의 Maps 목록을 확인하세요."
            );
            return false;
        }

        List<string> selectedSceneNames = new List<string>();

        if (SteamLobby.Instance.TryLoadSelectedMaps(out List<string> savedNames)
            && savedNames != null)
        {
            foreach (string savedName in savedNames)
            {
                if (!string.IsNullOrWhiteSpace(savedName)
                    && allSceneNames.Contains(savedName)
                    && !selectedSceneNames.Contains(savedName))
                {
                    selectedSceneNames.Add(savedName);
                }
            }
        }

        // 선택된 맵이 있으면 그중에서, 아니면 전체 맵 중에서 랜덤 선택.
        List<string> candidates = selectedSceneNames.Count > 0
            ? selectedSceneNames
            : allSceneNames;

        // 빌드 설정에 없는 씬은 후보에서 제외한다.
        candidates.RemoveAll(
            name => !Application.CanStreamedLevelBeLoaded(name)
        );

        if (candidates.Count == 0)
        {
            Debug.LogError(
                "MapSelectionManager: 로드 가능한 맵 씬이 없습니다. " +
                "MapData의 Scene Name과 Build Settings를 확인하세요."
            );
            return false;
        }

        sceneName = candidates[Random.Range(0, candidates.Count)];

        Debug.Log(
            $"맵 결정: " +
            $"{(selectedSceneNames.Count > 0 ? "선택 맵 중 랜덤" : "전체 맵 중 랜덤")} / " +
            $"{sceneName}"
        );

        return true;
    }

    private void HandleMessage(CSteamID sender, string message)
    {
        if (isLoadingScene ||
            string.IsNullOrEmpty(message) ||
            SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            return;
        }

        string[] parts = message.Split('|');

        if (parts.Length != 3 || parts[0] != StartScenePrefix)
            return;

        if (!ulong.TryParse(parts[1], out ulong lobbyID) ||
            lobbyID != SteamLobby.Instance.CurrentLobbyID.m_SteamID)
        {
            return;
        }

        // 씬 시작 메시지는 현재 로비 호스트에게서 온 것만 수락한다.
        CSteamID currentHost = SteamMatchmaking.GetLobbyOwner(
            SteamLobby.Instance.CurrentLobbyID
        );

        if (sender != currentHost)
            return;

        string sceneName = parts[2];

        if (string.IsNullOrWhiteSpace(sceneName) ||
            !IsRegisteredMap(sceneName))
        {
            Debug.LogError($"호스트가 보낸 맵이 등록된 맵 목록에 없습니다: {sceneName}");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"맵 씬 '{sceneName}'을 로드할 수 없습니다. " +
                "Build Settings를 확인하세요."
            );
            return;
        }

        LoadMapScene(sceneName);
    }

    private bool IsRegisteredMap(string sceneName)
    {
        if (mapListInnerPanel == null)
            return false;

        return mapListInnerPanel
            .GetRegisteredMapSceneNames()
            .Contains(sceneName);
    }

    private void BroadcastToLobby(string message)
    {
        if (SteamNetworkMessenger.Instance == null ||
            SteamLobby.Instance == null)
        {
            Debug.LogError("맵 시작 메시지를 보낼 네트워크 매니저가 없습니다.");
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

    private void LoadMapScene(string sceneName)
    {
        if (isLoadingScene)
            return;

        isLoadingScene = true;
        Debug.Log($"게임 씬으로 이동: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}
