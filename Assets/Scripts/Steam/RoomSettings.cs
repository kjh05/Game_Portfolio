using System;

public enum RoomType
{
    Public,
    Private
}

public enum LanguageTag
{
    NA,
    English,
    Spanish,
    German,
    French,
    Portuguese,
    Ukrainian,
    Russian,
    Turkish,
    Polish,
    Czech,
    Hungarian,
    Vietnamese,
    Japanese,
    Thai,
    Korean,
    TraditionalChinese,
    SimplifiedChinese
}

public enum GameMode
{
    Classic,
    Zombie,
    PassTheTorch
}

[Serializable]
public class RoomSettings
{
    public int maxPlayers = 10;
    public RoomType roomType = RoomType.Private;
    public LanguageTag languageTag = LanguageTag.NA;
    public GameMode gameMode = GameMode.Classic;
}