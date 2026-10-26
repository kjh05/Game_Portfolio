using Steamworks;

public enum PlayerLobbyStatus
{
    InLobby,
    Hunter,
    Hider,
    Spectator
}

public class LobbyPlayerData
{
    // Steam Player 정보
    public CSteamID steamID;
    public string steamName;

    // Lobby 상태
    public bool isReady;
    public bool isHost;

    // 플레이어 상태
    public PlayerLobbyStatus status = PlayerLobbyStatus.InLobby;

    // 현재 게임 점수: 음수 허용
    public int score = 0;

    public LobbyPlayerData(
        CSteamID steamID,
        string steamName,
        bool isReady,
        bool isHost)
    {
        this.steamID = steamID;
        this.steamName = steamName;
        this.isReady = isReady;
        this.isHost = isHost;

        status = PlayerLobbyStatus.InLobby;
        score = 0;
    }
}