
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;
using UnityEngine;

public class SteamNetworkMessenger : MonoBehaviour
{
    public static SteamNetworkMessenger Instance { get; private set; }

    // 메시지 수신 시: 보낸 사람의 Steam ID, 메시지 내용
    public event Action<CSteamID, string> OnMessageReceived;

    [Header("Network Settings")]
    [SerializeField] private int channel = 0;
    [SerializeField] private int maxMessagesPerFrame = 32;

    private Callback<SteamNetworkingMessagesSessionRequest_t>
        sessionRequestCallback;

    private readonly HashSet<ulong> acceptedSessions = new HashSet<ulong>();

    private const int MaxMessageBytes = 32768;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!SteamManagerIsAvailable())
        {
            Debug.LogError(
                "[SteamNetworkMessenger] Steam 초기화가 필요합니다."
            );
            enabled = false;
            return;
        }

        sessionRequestCallback =
            Callback<SteamNetworkingMessagesSessionRequest_t>.Create(
                OnSessionRequest
            );

        Debug.Log("[SteamNetworkMessenger] 초기화 완료");
    }

    private bool SteamManagerIsAvailable()
    {
        // SteamManager가 SteamAPI.Init()을 담당합니다.
        // 별도의 SteamAPI.Init() 호출은 하지 않습니다.
        return SteamAPI.IsSteamRunning() &&
               SteamUser.GetSteamID().IsValid();
    }

    private void OnSessionRequest(
        SteamNetworkingMessagesSessionRequest_t callback)
    {
        SteamNetworkingIdentity remoteIdentity =
            callback.m_identityRemote;

        CSteamID remoteSteamID = remoteIdentity.GetSteamID();

        if (!remoteSteamID.IsValid())
        {
            Debug.LogWarning(
                "[SteamNetworkMessenger] 유효하지 않은 세션 요청"
            );
            return;
        }

        if (SteamNetworkingMessages.AcceptSessionWithUser(
                ref remoteIdentity))
        {
            acceptedSessions.Add(remoteSteamID.m_SteamID);

            Debug.Log(
                $"[SteamNetworkMessenger] 세션 수락: {remoteSteamID}"
            );
        }
    }

    /// <summary>
    /// 지정한 Steam 사용자에게 문자열 메시지를 전송합니다.
    /// </summary>
    public bool SendMessage(CSteamID targetSteamID, string message)
    {
        if (!enabled || !targetSteamID.IsValid())
        {
            Debug.LogWarning(
                "[SteamNetworkMessenger] 전송 대상이 유효하지 않습니다."
            );
            return false;
        }

        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning(
                "[SteamNetworkMessenger] 빈 메시지는 전송하지 않습니다."
            );
            return false;
        }

        byte[] data = Encoding.UTF8.GetBytes(message);

        if (data.Length > MaxMessageBytes)
        {
            Debug.LogWarning(
                "[SteamNetworkMessenger] 메시지가 허용 크기를 초과했습니다."
            );
            return false;
        }

        SteamNetworkingIdentity identity =
            new SteamNetworkingIdentity();

        identity.SetSteamID(targetSteamID);

        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

        try
        {
            EResult result = SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                handle.AddrOfPinnedObject(),
                (uint)data.Length,
                Constants.k_nSteamNetworkingSend_Reliable,
                channel
            );

            if (result != EResult.k_EResultOK)
            {
                Debug.LogWarning(
                    $"[SteamNetworkMessenger] 전송 실패: {result}"
                );
                return false;
            }

            return true;
        }
        finally
        {
            handle.Free();
        }
    }

    private void Update()
    {
        if (!enabled)
            return;

        ReceiveMessages();
    }

    private void ReceiveMessages()
    {
        int bufferSize = Mathf.Clamp(maxMessagesPerFrame, 1, 256);
        IntPtr[] messages = new IntPtr[bufferSize];

        int receivedCount =
            SteamNetworkingMessages.ReceiveMessagesOnChannel(
                channel,
                messages,
                messages.Length
            );

        for (int i = 0; i < receivedCount; i++)
        {
            IntPtr messagePtr = messages[i];

            if (messagePtr == IntPtr.Zero)
                continue;

            try
            {
                SteamNetworkingMessage_t message =
                    Marshal.PtrToStructure<SteamNetworkingMessage_t>(
                        messagePtr
                    );

                CSteamID senderID =
                    message.m_identityPeer.GetSteamID();

                int size = message.m_cbSize;

                if (!senderID.IsValid() ||
                    message.m_pData == IntPtr.Zero ||
                    size <= 0 ||
                    size > MaxMessageBytes)
                {
                    Debug.LogWarning(
                        "[SteamNetworkMessenger] 잘못된 메시지를 무시했습니다."
                    );
                    continue;
                }

                byte[] data = new byte[size];
                Marshal.Copy(message.m_pData, data, 0, size);

                string text = Encoding.UTF8.GetString(data);

                OnMessageReceived?.Invoke(senderID, text);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[SteamNetworkMessenger] 수신 처리 오류: {exception}"
                );
            }
            finally
            {
                // Steam이 전달한 메시지 객체는 반드시 해제합니다.
                SteamNetworkingMessage_t.Release(messagePtr);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (sessionRequestCallback != null)
        {
            sessionRequestCallback.Dispose();
            sessionRequestCallback = null;
        }

        Instance = null;
    }
}
