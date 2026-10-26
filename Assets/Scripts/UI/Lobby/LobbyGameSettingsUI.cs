
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

public class LobbyGameSettingsUI : MonoBehaviour
{
    [Header("Settings Data")]
    [SerializeField]
    private LobbyGameSettings settings = new LobbyGameSettings();

    [Header("Settings Panel")]
    [SerializeField]
    private CanvasGroup settingsPanel;

    [Header("Input Fields")]
    [SerializeField] private TMP_InputField hunterRatioInput;
    [SerializeField] private TMP_InputField hidingTimeInput;
    [SerializeField] private TMP_InputField roundNumberInput;
    [SerializeField] private TMP_InputField roundDurationInput;
    [SerializeField] private TMP_InputField forcedTauntIntervalInput;
    [SerializeField] private TMP_InputField freezeTimeInput;

    [Header("Forced Taunt Checkbox")]
    [SerializeField] private Button forcedTauntCheckboxButton;
    [SerializeField] private GameObject checkIndicator;

    [Header("Hunter Number Display")]
    [SerializeField] private TMP_Text hunterNumberPlayersText;
    
    private bool isHost;
    private bool isLoadingSettings;

    private void Awake()
    {
        if (settings == null)
            settings = new LobbyGameSettings();

        InitializeSettings();
        SetupInputFields();
        SetupCheckbox();

        SetHostAuthority(false);
    }

    private void Start()
    {
        if (SteamLobby.Instance == null)
        {
            Debug.LogError("SteamLobby 인스턴스를 찾을 수 없습니다.");
            return;
        }

        SteamLobby.Instance.OnLobbyDataUpdated += HandleLobbyDataUpdated;
        SteamLobby.Instance.OnLobbyPlayersUpdated += HandleLobbyPlayersUpdated;

        SetHostAuthority(SteamLobby.Instance.IsCurrentLobbyHost());

        if (SteamLobby.Instance.TryLoadGameSettings(out LobbyGameSettings loadedSettings))
        {
            ApplySettings(loadedSettings);
        }
        else if (isHost)
        {
            SteamLobby.Instance.SaveGameSettings(settings);
        }
        else
        {
            SteamLobby.Instance.RequestLobbyData(
                SteamLobby.Instance.CurrentLobbyID
            );
        }

        SteamLobby.Instance.RefreshLobbyPlayers();
    }

    private void OnDestroy()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyDataUpdated -= HandleLobbyDataUpdated;
            SteamLobby.Instance.OnLobbyPlayersUpdated -= HandleLobbyPlayersUpdated;
        }

        if (forcedTauntCheckboxButton != null)
        {
            forcedTauntCheckboxButton.onClick.RemoveListener(
                ToggleForcedTaunt
            );
        }
    }

    private void InitializeSettings()
    {
        SetInputValue(hunterRatioInput, settings.hunterRatio);
        SetInputValue(hidingTimeInput, settings.hidingTime);
        SetInputValue(roundNumberInput, settings.roundNumber);
        SetInputValue(roundDurationInput, settings.roundDuration);
        SetInputValue(
            forcedTauntIntervalInput,
            settings.forcedTauntInterval
        );
        SetInputValue(freezeTimeInput, settings.freezeTime);

        UpdateCheckboxVisual();
    }

    private void SetupInputFields()
    {
        SetupInputField(hunterRatioInput, 2, 1, 99,
            value =>
            {
                settings.hunterRatio = value;

                if (SteamLobby.Instance != null)
                {
                    SteamLobby.Instance.RefreshLobbyPlayers();
                }
            });

        SetupInputField(hidingTimeInput, 2, 1, 99,
            value => settings.hidingTime = value);

        SetupInputField(roundNumberInput, 2, 1, 99,
            value => settings.roundNumber = value);

        SetupInputField(roundDurationInput, 3, 1, 999,
            value => settings.roundDuration = value);

        SetupInputField(forcedTauntIntervalInput, 2, 1, 99,
            value => settings.forcedTauntInterval = value);

        SetupInputField(freezeTimeInput, 2, 1, 99,
            value => settings.freezeTime = value);
    }

    private void SetupInputField(
        TMP_InputField input,
        int characterLimit,
        int min,
        int max,
        System.Action<int> applyValue)
    {
        if (input == null)
            return;

        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = characterLimit;

        input.onEndEdit.AddListener(value =>
        {
            if (!isHost || isLoadingSettings)
                return;

            if (!int.TryParse(value, out int number))
                number = min;

            number = Mathf.Clamp(number, min, max);

            input.SetTextWithoutNotify(number.ToString());
            applyValue(number);

            SaveSettingsToLobby();
        });
    }

    private void SetupCheckbox()
    {
        if (forcedTauntCheckboxButton == null)
            return;

        forcedTauntCheckboxButton.onClick.AddListener(
            ToggleForcedTaunt
        );
    }

    private void ToggleForcedTaunt()
    {
        if (!isHost || isLoadingSettings)
            return;

        settings.forcedTauntEnabled =
            !settings.forcedTauntEnabled;

        UpdateCheckboxVisual();
        SaveSettingsToLobby();
    }

    private void UpdateCheckboxVisual()
    {
        if (checkIndicator != null)
        {
            checkIndicator.SetActive(
                settings.forcedTauntEnabled
            );
        }
    }

    private void SetInputValue(
        TMP_InputField input,
        int value)
    {
        if (input != null)
        {
            input.SetTextWithoutNotify(value.ToString());
        }
    }

    private void ApplySettings(LobbyGameSettings loadedSettings)
    {
        if (loadedSettings == null)
            return;

        isLoadingSettings = true;

        settings.hunterRatio = Mathf.Clamp(loadedSettings.hunterRatio, 1, 99);
        settings.hidingTime = Mathf.Clamp(loadedSettings.hidingTime, 1, 99);
        settings.roundNumber = Mathf.Clamp(loadedSettings.roundNumber, 1, 99);
        settings.roundDuration = Mathf.Clamp(loadedSettings.roundDuration, 1, 999);
        settings.forcedTauntEnabled = loadedSettings.forcedTauntEnabled;
        settings.forcedTauntInterval = Mathf.Clamp(loadedSettings.forcedTauntInterval, 1, 99);
        settings.freezeTime = Mathf.Clamp(loadedSettings.freezeTime, 1, 99);

        settings.selectedMapSceneNames = new System.Collections.Generic.List<string>(
            loadedSettings.selectedMapSceneNames
        );

        InitializeSettings();

        isLoadingSettings = false;

        Debug.Log("Lobby Game Settings를 UI에 적용했습니다.");
    }

    private void SaveSettingsToLobby()
    {
        if (!isHost || isLoadingSettings)
            return;

        if (SteamLobby.Instance == null)
            return;

        SteamLobby.Instance.SaveGameSettings(settings);
    }

    private void HandleLobbyDataUpdated(LobbyDataUpdate_t callback)
    {
        if (SteamLobby.Instance == null)
            return;

        if (callback.m_ulSteamIDLobby !=
            SteamLobby.Instance.CurrentLobbyID.m_SteamID)
            return;

        if (callback.m_bSuccess == 0)
        {
            Debug.LogWarning("Lobby Data 갱신에 실패했습니다.");
            return;
        }

        if (SteamLobby.Instance.TryLoadGameSettings(out LobbyGameSettings loadedSettings))
        {
            ApplySettings(loadedSettings);
        }
    }

    public void SetHostAuthority(bool value)
    {
        isHost = value;

        if (settingsPanel != null)
        {
            settingsPanel.interactable = isHost;
            settingsPanel.blocksRaycasts = isHost;
        }
    }

    public LobbyGameSettings GetSettings()
    {
        return settings;
    }

    private void HandleLobbyPlayersUpdated(
    System.Collections.Generic.IReadOnlyList<LobbyPlayerData> players)
    {
        if (players == null)
            return;

        // 호스트가 바뀌었는지 다시 확인
        if (SteamLobby.Instance != null)
        {
            SetHostAuthority(
                SteamLobby.Instance.IsCurrentLobbyHost()
            );
        }

        UpdateHunterNumberDisplay(players.Count);
    }

    private void UpdateHunterNumberDisplay(int playerCount)
    {
        if (hunterNumberPlayersText == null)
            return;

        if (playerCount <= 0)
        {
            hunterNumberPlayersText.text = "(0/0)";
            return;
        }

        int hunterCount;

        if (playerCount == 1)
        {
            hunterCount = 1;
        }
        else
        {
            // 일반적인 반올림: 소수점 .5부터 올림
            hunterCount = (settings.hunterRatio * playerCount + 50) / 100;

            // 최소 1명, 최대 전체 인원 - 1명
            hunterCount = Mathf.Clamp(
                hunterCount,
                1,
                playerCount - 1
            );
        }

        hunterNumberPlayersText.text =
            $"({hunterCount}/{playerCount})";
    }
}