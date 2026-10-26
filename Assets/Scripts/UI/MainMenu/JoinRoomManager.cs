
using TMPro;
using UnityEngine;

public class JoinRoomManager : MonoBehaviour
{
    [Header("Room Code")]
    [SerializeField] private TMP_InputField roomCodeInputField;

    [Header("Warning")]
    [SerializeField] private WarningPanelController warningPanelController;

    [Header("Join Waiting UI")]
    [SerializeField] private JoinWaitingUI joinWaitingUI;

    public void JoinRoom()
    {
        if (roomCodeInputField == null)
        {
            Debug.LogError("Room Code Input Field가 연결되지 않았습니다.");
            return;
        }

        string roomCode = roomCodeInputField.text;

        if (string.IsNullOrEmpty(roomCode) || roomCode.Length != 6)
        {
            ShowInvalidRoomCodeWarning();
            return;
        }

        if (SteamLobby.Instance == null)
        {
            Debug.LogError("SteamLobby를 찾을 수 없습니다.");
            return;
        }

        if (joinWaitingUI == null)
        {
            Debug.LogError("JoinWaitingUI가 연결되지 않았습니다.");
            return;
        }

        Debug.Log($"Room Code Join 요청 | Room Code: {roomCode}");

        joinWaitingUI.BeginJoin(roomCode);
    }

    private void ShowInvalidRoomCodeWarning()
    {
        if (warningPanelController != null)
        {
            warningPanelController.ShowEnterWarning();
        }
        else
        {
            Debug.LogWarning(
                "WarningPanelController가 연결되지 않았습니다. " +
                "ENTER A VALID ROOM CODE"
            );
        }
    }
}
