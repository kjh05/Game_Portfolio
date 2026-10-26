using Steamworks;

public enum LobbyState
{
    InLobby,
    InGame
}

public class LobbyData
{
    public CSteamID lobbyID;

    public string roomCode;

    public LanguageTag language;

    public GameMode gameMode;

    public int currentPlayers;

    public int maxPlayers;

    public LobbyState state;

    public bool roomTypeIsPublic;

    public LobbyData(
        CSteamID lobbyID,
        string roomCode,
        LanguageTag language,
        GameMode gameMode,
        int currentPlayers,
        int maxPlayers,
        LobbyState state)
    {
        this.lobbyID = lobbyID;
        this.roomCode = roomCode;
        this.language = language;
        this.gameMode = gameMode;
        this.currentPlayers = currentPlayers;
        this.maxPlayers = maxPlayers;
        this.state = state;
    }
}