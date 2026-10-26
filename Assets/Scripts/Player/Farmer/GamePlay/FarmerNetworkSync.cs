using System;
using Steamworks;
using UnityEngine;

[DisallowMultipleComponent]
public class FarmerNetworkSync : MonoBehaviour
{
    private const string MessagePrefix = "FARMER_STATE|";

    [Serializable]
    private class FarmerStatePacket
    {
        public ulong ownerSteamId;
        public Vector3 position;
        public Quaternion rotation;
        public bool isWalking;
        public float animationSpeed;
        public float pitch;
        public int fireSequence;
    }

    [Header("References")]
    [SerializeField] private FarmerLocalInput localInput;
    [SerializeField] private FarmerMovement farmerMovement;
    [SerializeField] private Animator farmerAnimator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private FarmerLook farmerLook;
    [SerializeField] private PistolFire pistolFire;

    [Header("Send Settings")]
    [SerializeField, Min(0.02f)]
    private float sendInterval = 0.05f;

    [Header("Remote Smoothing")]
    [SerializeField, Min(0.01f)]
    private float positionSmoothTime = 0.08f;

    [SerializeField, Min(0.01f)]
    private float rotationSpeed = 15f;

    private ulong ownerSteamId;
    private bool isInitialized;
    private float sendTimer;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 positionSmoothVelocity;

    private int lastReceivedFireSequence;
    private bool hasReceivedFireSequence;

    private bool hasReceivedState;

    private static readonly int FarmerWalkHash =
        Animator.StringToHash("FarmerWalk");

    private void Awake()
    {
        if (localInput == null)
            localInput = GetComponent<FarmerLocalInput>();

        if (farmerMovement == null)
            farmerMovement = GetComponent<FarmerMovement>();

        if (farmerAnimator == null)
            farmerAnimator = GetComponentInChildren<Animator>();
        
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (farmerLook == null)
            farmerLook = GetComponentInChildren<FarmerLook>(true);

        if (pistolFire == null)
            pistolFire = GetComponentInChildren<PistolFire>(true);

        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    private void OnEnable()
    {
        SubscribeToMessages();
    }

    private void Start()
    {
        SubscribeToMessages();
    }

    public void Initialize(ulong steamId, bool isLocalPlayer)
    {
        ownerSteamId = steamId;
        isInitialized = steamId != 0;

        if (localInput != null)
            localInput.Initialize(isLocalPlayer);

        // 로컬 농부만 CharacterController로 이동한다.
        // 원격 농부는 네트워크 상태를 따라간다.
        if (characterController != null)
            characterController.enabled = isLocalPlayer;

        targetPosition = transform.position;
        targetRotation = transform.rotation;
        positionSmoothVelocity = Vector3.zero;
        hasReceivedState = false;
        sendTimer = 0f;
        lastReceivedFireSequence = 0;
        hasReceivedFireSequence = false;

        Debug.Log(
            $"[FarmerNetworkSync] 초기화 | SteamID: {steamId} | " +
            $"Local: {isLocalPlayer}",
            this
        );
    }

    private void SubscribeToMessages()
    {
        if (SteamNetworkMessenger.Instance == null)
            return;

        SteamNetworkMessenger.Instance.OnMessageReceived -= HandleMessage;
        SteamNetworkMessenger.Instance.OnMessageReceived += HandleMessage;
    }

    private void OnDisable()
    {
        if (SteamNetworkMessenger.Instance != null)
            SteamNetworkMessenger.Instance.OnMessageReceived -= HandleMessage;
    }

    private void Update()
    {
        if (!isInitialized || localInput == null)
            return;

        if (localInput.IsLocalPlayer)
            SendStatePeriodically();
        else
            ApplyRemoteState();
    }

    private bool TryGetLobby(out CSteamID lobbyId)
    {
        lobbyId = CSteamID.Nil;

        if (SteamLobby.Instance == null)
            return false;

        lobbyId = SteamLobby.Instance.CurrentLobbyID;
        return lobbyId.IsValid();
    }

    private void SendToOtherLobbyMembers(string message)
    {
        if (SteamNetworkMessenger.Instance == null)
            return;

        if (!TryGetLobby(out CSteamID lobbyId))
            return;

        CSteamID localId = SteamUser.GetSteamID();
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberId =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            if (!memberId.IsValid() || memberId == localId)
                continue;

            SteamNetworkMessenger.Instance.SendMessage(memberId, message);
        }
    }

    private void SendStatePeriodically()
    {
        sendTimer += Time.deltaTime;

        if (sendTimer < sendInterval)
            return;

        sendTimer = 0f;

        bool isWalking = false;
        float animationSpeed = 1f;

        if (farmerAnimator != null)
        {
            isWalking = farmerAnimator.GetBool(FarmerWalkHash);
            animationSpeed = farmerAnimator.speed;
        }

        FarmerStatePacket packet = new FarmerStatePacket
        {
            ownerSteamId = ownerSteamId,
            position = transform.position,
            rotation = transform.rotation,
            isWalking = isWalking,
            animationSpeed = animationSpeed,

            pitch = farmerLook != null
                ? farmerLook.CurrentPitch                    : 0f,

            fireSequence = pistolFire != null
                ? pistolFire.FireSequence
                : 0
        };

        SendToOtherLobbyMembers(
            MessagePrefix + JsonUtility.ToJson(packet)
        );
    }

    private void HandleMessage(CSteamID senderId, string message)
    {
        if (!isInitialized || localInput == null)
            return;

        if (localInput.IsLocalPlayer ||
            string.IsNullOrEmpty(message) ||
            !message.StartsWith(MessagePrefix, StringComparison.Ordinal))
            return;

        FarmerStatePacket packet;

        try
        {
            packet = JsonUtility.FromJson<FarmerStatePacket>(
                message.Substring(MessagePrefix.Length)
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[FarmerNetworkSync] 잘못된 상태 패킷: {exception.Message}"
            );
            return;
        }

        if (packet == null ||
            packet.ownerSteamId == 0 ||
            packet.ownerSteamId != senderId.m_SteamID ||
            packet.ownerSteamId != ownerSteamId)
            return;

        targetPosition = packet.position;
        targetRotation = packet.rotation;
        hasReceivedState = true;

        if (farmerAnimator != null)
        {
            farmerAnimator.SetBool(FarmerWalkHash, packet.isWalking);
            farmerAnimator.speed = packet.animationSpeed;
        }

        if (farmerLook != null)
            farmerLook.ApplyRemotePitch(packet.pitch);

        if (pistolFire != null)
        {
            if (!hasReceivedFireSequence)
            {
                // 최초 수신 시에는 현재 발사 횟수만 기록한다.
                // 접속 직후 과거 발사 효과가 재생되는 것을 방지한다.
                lastReceivedFireSequence = packet.fireSequence;
                hasReceivedFireSequence = true;
            }
            else if (packet.fireSequence != lastReceivedFireSequence)
            {
                lastReceivedFireSequence = packet.fireSequence;
                pistolFire.PlayRemoteFireEffects();
            }
        }
    }

    private void ApplyRemoteState()
    {
        if (!hasReceivedState)
            return;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref positionSmoothVelocity,
            positionSmoothTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}