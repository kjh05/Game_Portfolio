using UnityEngine;
using Steamworks;

public class SteamManager : MonoBehaviour
{
    private static SteamManager instance;

    private bool initialized;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);

        if (!SteamAPI.IsSteamRunning())
        {
            Debug.LogError("Steam이 실행되고 있지 않습니다.");
            return;
        }

        initialized = SteamAPI.Init();

        if (!initialized)
        {
            Debug.LogError("Steamworks 초기화 실패!");
            return;
        }

        Debug.Log("Steamworks 초기화 성공!");
        Debug.Log($"Steam 이름: {SteamFriends.GetPersonaName()}");
        Debug.Log($"Steam ID: {SteamUser.GetSteamID()}");
    }

    private void Update()
    {
        if (initialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        if (initialized)
        {
            SteamAPI.Shutdown();
            initialized = false;
        }

        instance = null;
    }
}