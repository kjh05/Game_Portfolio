using UnityEngine;

public class CreateRoomManager : MonoBehaviour
{
    [Header("Room Settings")]
    [SerializeField] private RoomSettings roomSettings = new RoomSettings();

    [Header("UI")]
    [SerializeField] private MaxPlayerInputHandler maxPlayerInputHandler;
    [SerializeField] private RoomTypeSelector roomTypeSelector;
    [SerializeField] private LanguageTagSelector languageTagSelector;
    [SerializeField] private GameModeSelector gameModeSelector;

    private void Awake()
    {
        // 기본값
        roomSettings.maxPlayers = 10;
        roomSettings.roomType = RoomType.Private;
        roomSettings.languageTag = LanguageTag.NA;
        roomSettings.gameMode = GameMode.Classic;
    }

    public void ApplyRoomSettings()
    {
        // Max Players
        if (maxPlayerInputHandler != null)
        {
            roomSettings.maxPlayers =
                maxPlayerInputHandler.GetValue();
        }

        // Room Type
        if (roomTypeSelector != null)
        {
            roomSettings.roomType =
                roomTypeSelector.GetValue();
        }

        // Language Tag
        if (languageTagSelector != null)
        {
            roomSettings.languageTag =
                languageTagSelector.GetValue();
        }

        if (gameModeSelector != null)
        {
            roomSettings.gameMode =
                gameModeSelector.GetValue();
        }

        Debug.Log(
            $"Room Settings | " +
            $"Max Players: {roomSettings.maxPlayers}, " +
            $"Room Type: {roomSettings.roomType}, " +
            $"Language: {roomSettings.languageTag}, " +
            $"Game Mode: {roomSettings.gameMode}"
        );
    }

    public void CreateRoom()
    {
        ApplyRoomSettings();

        if (SteamLobby.Instance == null)
        {
            Debug.LogError("SteamLobby를 찾을 수 없습니다.");
            return;
        }

        SteamLobby.Instance.CreateLobby(roomSettings);
    }

    public RoomSettings GetRoomSettings()
    {
        return roomSettings;
    }
}