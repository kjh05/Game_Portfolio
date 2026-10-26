using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapListInnerPanel : MonoBehaviour
{
    [Header("Map List")]
    [SerializeField] private List<MapData> maps = new List<MapData>();

    [Header("UI")]
    [SerializeField] private Transform content;
    [SerializeField] private GameObject mapBoxPrefab;
    [SerializeField] private GameObject randomBox;

    [Header("Random Selection UI")]
    [SerializeField] private Image randomSelectionBoxImage;
    [SerializeField] private TMPro.TMP_Text randomSelectionText;

    [Header("Selection Colors")]
    [SerializeField] private Color randomNormalColor = Color.black;
    [SerializeField] private Color randomSelectedColor = Color.blue;

    private readonly List<MapBox> generatedMapBoxes =
        new List<MapBox>();

    private bool isRandomSelected = true;

    public IReadOnlyList<MapBox> GeneratedMapBoxes =>
        generatedMapBoxes;

    public bool IsRandomSelected =>
        isRandomSelected;

    private void Start()
    {
        GenerateMapList();
        ApplySelectionFromLobby();
    }

    private void OnEnable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyDataUpdated +=
                HandleLobbyDataUpdated;
        }
    }

    private void OnDisable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyDataUpdated -=
                HandleLobbyDataUpdated;
        }
    }

    private void HandleLobbyDataUpdated(
        Steamworks.LobbyDataUpdate_t callback)
    {
        if (SteamLobby.Instance == null)
        {
            return;
        }

        if (callback.m_ulSteamIDLobby !=
            SteamLobby.Instance.CurrentLobbyID.m_SteamID)
        {
            return;
        }

        if (callback.m_bSuccess == 0)
        {
            return;
        }

        ApplySelectionFromLobby();
    }

    private void ApplySelectionFromLobby()
    {
        if (SteamLobby.Instance == null)
        {
            return;
        }

        LobbyGameSettings settings;

        if (!SteamLobby.Instance.TryLoadGameSettings(
                out settings))
        {
            return;
        }

        // 먼저 모든 실제 맵 선택 해제
        foreach (MapBox mapBox in generatedMapBoxes)
        {
            if (mapBox != null)
            {
                mapBox.SetSelected(false);
            }
        }

        // 저장된 선택이 없다면 RANDOM
        if (settings.selectedMapSceneNames == null ||
            settings.selectedMapSceneNames.Count == 0)
        {
            isRandomSelected = true;
            UpdateRandomSelectionUI();
            return;
        }

        // 저장된 SceneName과 현재 MapData의 SceneName 비교
        foreach (MapBox mapBox in generatedMapBoxes)
        {
            if (mapBox == null ||
                mapBox.Data == null)
            {
                continue;
            }

            if (settings.selectedMapSceneNames.Contains(
                    mapBox.Data.SceneName))
            {
                mapBox.SetSelected(true);
            }
        }

        isRandomSelected = false;

        UpdateRandomSelectionUI();
    }

    private void GenerateMapList()
    {
        ClearMapList();

        generatedMapBoxes.Clear();

        if (randomBox != null)
        {
            randomBox.SetActive(true);
            randomBox.transform.SetAsLastSibling();
            Transform selectionBox =
                randomBox.transform.Find("SelectionBox");

            if (selectionBox != null)
            {
                Button randomSelectionButton =
                    selectionBox.GetComponent<Button>();

                if (randomSelectionButton != null)
                {
                    randomSelectionButton.onClick.RemoveListener(
                        SelectRandom
                    );

                    randomSelectionButton.onClick.AddListener(
                        SelectRandom
                    );
                }
            }
        }

        foreach (MapData map in maps)
        {
            if (mapBoxPrefab == null || content == null)
            {
                Debug.LogError(
                    "MapListInnerPanel: " +
                    "MapBox Prefab 또는 Content가 연결되지 않았습니다."
                );

                return;
            }

            GameObject mapBoxObject =
                Instantiate(
                    mapBoxPrefab,
                    content
                );

            MapBox mapBox =
                mapBoxObject.GetComponent<MapBox>();

            if (mapBox == null)
            {
                Debug.LogError(
                    "MapListInnerPanel: " +
                    "MapBox Prefab에 MapBox 컴포넌트가 없습니다."
                );

                continue;
            }

            mapBox.Initialize(map);

            generatedMapBoxes.Add(mapBox);
        }
    }

    private void ClearMapList()
    {
        if (content == null)
        {
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);

            if (randomBox != null &&
                child.gameObject == randomBox)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    public void SelectRandom()
    {
        if (!CanChangeSelection())
        {
            return;
        }

        foreach (MapBox mapBox in generatedMapBoxes)
        {
            if (mapBox != null)
            {
                mapBox.SetSelected(false);
            }
        }

        isRandomSelected = true;

        UpdateRandomSelectionUI();

        Debug.Log("Map Selection: RANDOM 선택");

        SaveCurrentSelection();
    }

    public void ToggleMapSelection(MapBox mapBox)
    {
        if (!CanChangeSelection())
        {
            return;
        }

        if (mapBox == null)
        {
            return;
        }

        if (mapBox.IsSelected)
        {
            mapBox.SetSelected(false);
        }
        else
        {
            mapBox.SetSelected(true);
        }

        bool hasSelectedMap = false;

        foreach (MapBox box in generatedMapBoxes)
        {
            if (box != null && box.IsSelected)
            {
                hasSelectedMap = true;
                break;
            }
        }

        if (!hasSelectedMap)
        {
            SelectRandom();
            return;
        }

        isRandomSelected = false;

        UpdateRandomSelectionUI();

        Debug.Log(
            $"Map Selection 변경 | " +
            $"Map: {mapBox.Data?.MapName}"
        );

        SaveCurrentSelection();
    }

    public void SelectAllMaps()
    {
        if (!CanChangeSelection())
        {
            return;
        }

        // Random 해제
        isRandomSelected = false;

        // 모든 실제 Map 선택
        foreach (MapBox mapBox in generatedMapBoxes)
        {
            if (mapBox != null)
            {
                mapBox.SetSelected(true);
            }
        }

        UpdateRandomSelectionUI();

        SaveCurrentSelection();

        Debug.Log("Map Selection: SELECT ALL");
    }

    public void ClearAllMaps()
    {
        if (!CanChangeSelection())
        {
            return;
        }   

        // 실제 Map 전부 해제
        foreach (MapBox mapBox in generatedMapBoxes)
        {
            if (mapBox != null)
            {
                mapBox.SetSelected(false);
            }
        }

        // Random 선택
        isRandomSelected = true;

        UpdateRandomSelectionUI();

        SaveCurrentSelection();

        Debug.Log("Map Selection: CLEAR ALL → RANDOM");
    }

    public void ShuffleMaps()
    {
        if (!CanChangeSelection())
        {
            return;
        }

        if (SteamLobby.Instance == null)
        {
            return;
        }

        LobbyGameSettings settings;

        if (!SteamLobby.Instance.TryLoadGameSettings(
                out settings))
        {
            Debug.LogWarning(
                "MapListInnerPanel: " +
                "Game Settings를 불러오지 못했습니다."
            );

            return;
        }

        int roundNumber =
            Mathf.Max(1, settings.roundNumber);

        // 실제 Map만 별도로 수집
        List<MapBox> availableMaps =
            new List<MapBox>();

        foreach (MapBox mapBox in generatedMapBoxes)
        {
            if (mapBox != null &&
                mapBox.Data != null)
            {
                availableMaps.Add(mapBox);
            }
        }

        if (availableMaps.Count == 0)
        {
            ClearAllMaps();
            return;
        }

        // 실제 Map 전체를 먼저 해제
        foreach (MapBox mapBox in availableMaps)
        {
            mapBox.SetSelected(false);
        }

        // Random 해제
        isRandomSelected = false;

        // 실제 Map들을 랜덤하게 섞음
        for (int i = availableMaps.Count - 1; i > 0; i--)
        {
            int randomIndex =
                UnityEngine.Random.Range(
                    0,
                    i + 1
                );

            MapBox temp =
                availableMaps[i];

            availableMaps[i] =
                availableMaps[randomIndex];

            availableMaps[randomIndex] =
                temp;
        }

        // Round Number만큼 선택
        int selectCount =
            Mathf.Min(
                roundNumber,
                availableMaps.Count
            );

        for (int i = 0; i < selectCount; i++)
        {
            availableMaps[i].SetSelected(true);
        }

        UpdateRandomSelectionUI();

        SaveCurrentSelection();

        Debug.Log(
            $"Map Selection: SHUFFLE | " +
            $"Round Number: {roundNumber} | " +
            $"Selected: {selectCount}"
        );
    }

    private void UpdateRandomSelectionUI()
    {
        if (randomSelectionText != null)
        {
            randomSelectionText.text =
                isRandomSelected
                    ? "SELECTED!"
                    : "SELECT";
        }

        if (randomSelectionBoxImage != null)
        {
            randomSelectionBoxImage.color =
                isRandomSelected
                    ? randomSelectedColor
                    : randomNormalColor;
        }
    }

    public List<string> GetSelectedMapSceneNames()
    {
        List<string> selectedMapSceneNames =
            new List<string>();

        if (isRandomSelected)
        {
            return selectedMapSceneNames;
        }

        foreach (MapBox mapBox in generatedMapBoxes)
        {
            if (mapBox == null ||
                !mapBox.IsSelected ||
                mapBox.Data == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(
                    mapBox.Data.SceneName))
            {
                continue;
            }

            if (!selectedMapSceneNames.Contains(
                    mapBox.Data.SceneName))
            {
                selectedMapSceneNames.Add(
                    mapBox.Data.SceneName
                );
            }
        }

        if (selectedMapSceneNames.Count == 0)
        {
            isRandomSelected = true;
            UpdateRandomSelectionUI();
        }

        return selectedMapSceneNames;
    }

    private void SaveCurrentSelection()
    {
        if (SteamLobby.Instance == null)
        {
            Debug.LogWarning(
                "MapListInnerPanel: SteamLobby를 찾을 수 없습니다."
            );

            return;
        }

        LobbyGameSettings settings;

        if (!SteamLobby.Instance.TryLoadGameSettings(
                out settings))
        {
            Debug.LogWarning(
                "MapListInnerPanel: " +
                "현재 Lobby Game Settings를 불러오지 못했습니다."
            );

            return;
        }

        settings.selectedMapSceneNames =
            GetSelectedMapSceneNames();

        if (!SteamLobby.Instance.SaveGameSettings(settings))
        {
            Debug.LogWarning(
                "MapListInnerPanel: " +
                "Map Selection 저장에 실패했습니다."
            );
        }
    }

    private bool CanChangeSelection()
    {
        if (SteamLobby.Instance == null)
        {
            Debug.LogWarning(
                "MapListInnerPanel: SteamLobby를 찾을 수 없습니다."
            );

            return false;
        }

        if (!SteamLobby.Instance.IsCurrentLobbyHost())
        {
            Debug.LogWarning(
                "Host만 Map Selection을 변경할 수 있습니다."
            );

            return false;
        }

        return true;
    }

    public List<string> GetRegisteredMapSceneNames()
    {
        List<string> sceneNames = new List<string>();

        foreach (MapData map in maps)
        {
            if (map == null || string.IsNullOrWhiteSpace(map.SceneName))
                continue;

            if (!sceneNames.Contains(map.SceneName))
                sceneNames.Add(map.SceneName);
        }

        return sceneNames;
    }
}