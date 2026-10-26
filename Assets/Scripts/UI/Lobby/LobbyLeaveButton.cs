using UnityEngine;

public class LobbyLeaveButton : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public void LeaveLobby()
    {
        if (SteamLobby.Instance == null)
        {
            Debug.LogError("SteamLobby 인스턴스를 찾을 수 없습니다.");
            return;
        }

        SteamLobby.Instance.LeaveCurrentLobby(mainMenuSceneName);
    }
}