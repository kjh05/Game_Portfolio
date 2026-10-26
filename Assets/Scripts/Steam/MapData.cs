using UnityEngine;

[System.Serializable]
public class MapData
{
    [Header("Scene")]
    [SerializeField] private string sceneName;

    [Header("Display")]
    [SerializeField] private string mapName;
    [SerializeField] private Sprite mapImage;
    [TextArea(3, 6)]
    [SerializeField] private string description;

    public string SceneName => sceneName;
    public string MapName => mapName;
    public Sprite MapImage => mapImage;
    public string Description => description;
}