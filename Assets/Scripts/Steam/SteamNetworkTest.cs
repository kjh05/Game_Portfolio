
using Steamworks;
using UnityEngine;

public class SteamNetworkTest : MonoBehaviour
{
    [Header("상대방 Steam ID")]
    [SerializeField] private string targetSteamID;

    private void OnEnable()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived
                += HandleMessageReceived;
        }
    }

    private void OnDisable()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived
                -= HandleMessageReceived;
        }
    }

    private void HandleMessageReceived(CSteamID senderID, string message)
    {
        Debug.Log(
            $"[Network Test] 받은 메시지 | 보낸 사람: {senderID.m_SteamID} | 내용: {message}"
        );
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 420, 180), GUI.skin.box);

        GUILayout.Label("Steam Network Test");

        GUILayout.Label("상대방 Steam ID:");
        targetSteamID = GUILayout.TextField(targetSteamID);

        if (GUILayout.Button("테스트 메시지 보내기", GUILayout.Height(40)))
        {
            SendTestMessage();
        }

        GUILayout.EndArea();
    }

    private void SendTestMessage()
    {
        if (!ulong.TryParse(targetSteamID, out ulong steamIDValue))
        {
            Debug.LogWarning("[Network Test] Steam ID를 올바르게 입력해 줘.");
            return;
        }

        CSteamID target = new CSteamID(steamIDValue);

        bool success = SteamNetworkMessenger.Instance != null &&
            SteamNetworkMessenger.Instance.SendMessage(
                target,
                "HELLO_FROM_CLUCK"
            );

        Debug.Log(
            success
                ? $"[Network Test] 전송 성공 반환 | 대상: {steamIDValue}"
                : $"[Network Test] 전송 실패 | 대상: {steamIDValue}"
        );
    }
}
