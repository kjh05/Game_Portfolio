
using System;
using Steamworks;
using UnityEngine;

[DisallowMultipleComponent]
public class ChickenNetworkSync : MonoBehaviour
{
    private const string MessagePrefix = "CHICKEN_TRANSFORM|";
    private const string EventPrefix = "CHICKEN_ANIMATION_EVENT|";

    [Serializable]
    private class TransformPacket
    {
        public ulong ownerSteamId;

        public Vector3 position;
        public Quaternion modelRotation;
        public bool hasModelRotation;

        public float animationSpeed;
        public bool isGrounded;
        public float verticalVelocity;
        public bool isClimbing;
        public bool isAiming;
        public float climbAnimationSpeed;
    }

    [Serializable]
    private class AnimationEventPacket
    {
        public ulong ownerSteamId;
        public string eventName;
    }

    [Header("References")]
    [SerializeField] private ChickenNetworkIdentity identity;
    [SerializeField] private Transform chickenModel;
    [SerializeField] private ChickenMovement chickenMovement;
    [SerializeField] private ChickenAnimation chickenAnimation;

    [Header("Send Settings")]
    [SerializeField, Min(0.02f)]
    private float sendInterval = 0.05f;

    [Header("Remote Smoothing")]
    [SerializeField, Min(0.01f)]
    private float positionSmoothTime = 0.08f;

    [SerializeField, Min(0.01f)]
    private float rotationSpeed = 15f;

    private float sendTimer;

    private Vector3 targetPosition;
    private Quaternion targetModelRotation;
    private Vector3 positionSmoothVelocity;

    private bool hasReceivedState;

    private void Awake()
    {
        if (identity == null)
            identity = GetComponent<ChickenNetworkIdentity>();

        if (chickenMovement == null)
            chickenMovement = GetComponent<ChickenMovement>();

        if (chickenAnimation == null)
            chickenAnimation = GetComponent<ChickenAnimation>();

        targetPosition = transform.position;

        targetModelRotation = chickenModel != null
            ? chickenModel.rotation
            : transform.rotation;
    }

    private void OnEnable()
    {
        SubscribeToMessages();
    }

    private void Start()
    {
        SubscribeToMessages();
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
        if (identity == null)
            return;

        if (identity.IsLocalPlayer)
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
        return lobbyId != CSteamID.Nil;
    }

    private void SendToOtherLobbyMembers(string message)
    {
        if (SteamNetworkMessenger.Instance == null)
            return;

        if (!TryGetLobby(out CSteamID lobbyId))
            return;

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
        CSteamID localSteamId = SteamUser.GetSteamID();

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberId =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            if (!memberId.IsValid() || memberId == localSteamId)
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

        float animationSpeed = 0f;
        bool grounded = true;
        float verticalVelocity = 0f;
        bool climbing = false;
        bool aiming = false;

        if (chickenMovement != null)
        {
            if (chickenMovement.IsMoving)
            {
                animationSpeed = chickenMovement.IsSprinting
                    ? 1f
                    : 0.5f;
            }

            grounded = chickenMovement.IsGrounded;
            verticalVelocity = chickenMovement.VerticalVelocity;
            climbing = chickenMovement.IsClimbing;
            aiming = chickenMovement.IsAiming;
        }

        TransformPacket packet = new TransformPacket
        {
            ownerSteamId = identity.OwnerSteamId,

            position = transform.position,

            hasModelRotation = chickenModel != null,
            modelRotation = chickenModel != null
                ? chickenModel.rotation
                : transform.rotation,

            animationSpeed = animationSpeed,
            isGrounded = grounded,
            verticalVelocity = verticalVelocity,
            isClimbing = climbing,
            isAiming = aiming,

            climbAnimationSpeed = chickenAnimation != null
                ? chickenAnimation.ClimbAnimationSpeed
                : 1f
        };

        string message = MessagePrefix + JsonUtility.ToJson(packet);
        SendToOtherLobbyMembers(message);
    }

    // ChickenAnimation이 Peck 또는 Wiggle을 시작할 때 호출한다.
    public void SendAnimationEvent(string eventName)
    {
        if (identity == null || !identity.IsLocalPlayer)
            return;

        if (eventName != "Peck" && eventName != "Wiggle")
            return;

        AnimationEventPacket packet = new AnimationEventPacket
        {
            ownerSteamId = identity.OwnerSteamId,
            eventName = eventName
        };

        string message = EventPrefix + JsonUtility.ToJson(packet);
        SendToOtherLobbyMembers(message);
    }

    private void HandleMessage(CSteamID senderId, string message)
    {
        if (identity == null || identity.IsLocalPlayer)
            return;

        if (string.IsNullOrEmpty(message))
            return;

        if (message.StartsWith(EventPrefix, StringComparison.Ordinal))
        {
            HandleAnimationEvent(senderId, message);
            return;
        }

        if (!message.StartsWith(MessagePrefix, StringComparison.Ordinal))
            return;

        string json = message.Substring(MessagePrefix.Length);

        TransformPacket packet;

        try
        {
            packet = JsonUtility.FromJson<TransformPacket>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[ChickenNetworkSync] 잘못된 상태 패킷: {exception.Message}"
            );
            return;
        }

        if (packet == null || packet.ownerSteamId == 0)
            return;

        if (packet.ownerSteamId != senderId.m_SteamID)
            return;

        if (identity.OwnerSteamId != packet.ownerSteamId)
            return;

        targetPosition = packet.position;

        targetModelRotation = packet.hasModelRotation
            ? packet.modelRotation
            : transform.rotation;

        hasReceivedState = true;

        if (chickenAnimation != null)
        {
            chickenAnimation.ApplyNetworkAnimationState(
                packet.animationSpeed,
                packet.isGrounded,
                packet.verticalVelocity,
                packet.isClimbing,
                packet.isAiming
            );

            if (packet.isClimbing)
            {
                chickenAnimation.ApplyNetworkClimbAnimationSpeed(
                    packet.climbAnimationSpeed
                );
            }
        }
    }

    private void HandleAnimationEvent(CSteamID senderId, string message)
    {
        string json = message.Substring(EventPrefix.Length);

        AnimationEventPacket packet;

        try
        {
            packet = JsonUtility.FromJson<AnimationEventPacket>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[ChickenNetworkSync] 잘못된 애니메이션 이벤트: {exception.Message}"
            );
            return;
        }

        if (packet == null || packet.ownerSteamId == 0)
            return;

        if (packet.ownerSteamId != senderId.m_SteamID)
            return;

        if (identity.OwnerSteamId != packet.ownerSteamId)
            return;

        if (chickenAnimation != null)
            chickenAnimation.ApplyNetworkTrigger(packet.eventName);
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

        if (chickenModel != null)
        {
            chickenModel.rotation = Quaternion.Slerp(
                chickenModel.rotation,
                targetModelRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
}
