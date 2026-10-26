
using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class EggNetworkManager : MonoBehaviour
{
    public static EggNetworkManager Instance { get; private set; }

    private const string Prefix = "EGGNET|";

    [Header("Projectile")]
    [SerializeField] private GameObject shootableEggPrefab;
    [SerializeField] private float maxProjectileLifetime = 8f;

    private ulong nextEggId = 1;

    private readonly Dictionary<ulong, GameObject> activeEggs =
        new Dictionary<ulong, GameObject>();

    private readonly Dictionary<ulong, ulong> eggOwners =
        new Dictionary<ulong, ulong>();

    [Serializable]
    private class EggPacket
    {
        public string type;
        public ulong eggId;
        public ulong ownerSteamId;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 gravity;
        public string hitObjectName;
        public ulong targetSteamId;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived
                += HandleNetworkMessage;
        }
    }

    private void Start()
    {
        // 이 매니저가 Messenger보다 나중에 활성화되는 경우도 처리
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived
                -= HandleNetworkMessage;

            SteamNetworkMessenger.Instance.OnMessageReceived
                += HandleNetworkMessage;
        }
    }

    private bool IsHost()
    {
        return SteamLobby.Instance != null &&
               SteamLobby.Instance.IsCurrentLobbyHost();
    }

    /// <summary>
    /// 로컬 플레이어가 발사한 알을 호스트에 요청한다.
    /// 호스트 자신이 발사한 경우에도 같은 승인 경로를 사용한다.
    /// </summary>
    public void RequestShot(
        ulong ownerSteamId,
        Vector3 position,
        Vector3 velocity,
        Vector3 gravity)
    {
        if (SteamNetworkMessenger.Instance == null)
        {
            Debug.LogError("[EggNetwork] Messenger가 없습니다.");
            return;
        }

        EggPacket packet = new EggPacket
        {
            type = "SHOT_REQUEST",
            ownerSteamId = ownerSteamId,
            position = position,
            velocity = velocity,
            gravity = gravity
        };

        if (IsHost())
        {
            ProcessShotRequest(
                SteamUser.GetSteamID(),
                packet
            );

            return;
        }

        CSteamID hostId = GetHostSteamId();

        if (!hostId.IsValid())
        {
            Debug.LogWarning("[EggNetwork] 호스트를 찾을 수 없습니다.");
            return;
        }

        Send(hostId, packet);
    }

    private void HandleNetworkMessage(CSteamID sender, string message)
    {
        if (string.IsNullOrEmpty(message) ||
            !message.StartsWith(Prefix))
        {
            return;
        }

        string json = message.Substring(Prefix.Length);
        EggPacket packet;

        try
        {
            packet = JsonUtility.FromJson<EggPacket>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[EggNetwork] 패킷 파싱 실패: {exception.Message}");
            return;
        }

        if (packet == null || string.IsNullOrEmpty(packet.type))
            return;

        switch (packet.type)
        {
            case "SHOT_REQUEST":
                if (IsHost())
                    ProcessShotRequest(sender, packet);
                break;

            case "SPAWN":
                // 생성 명령은 현재 호스트에게서 온 것만 허용한다.
                if (IsMessageFromHost(sender))
                    SpawnEgg(packet);
                break;

            case "DESTROY":
                if (IsMessageFromHost(sender))
                    DestroyEgg(packet.eggId);
                break;
            
            case "BLIND":
                // 호스트가 보낸 피격 효과 명령만 허용한다.
                if (IsMessageFromHost(sender) &&
                    packet.ownerSteamId == SteamUser.GetSteamID().m_SteamID)
                {
                    ApplyBlindToLocalFarmer();
                }
                break;
            
            case "HIT_CONFIRM":
                if (IsMessageFromHost(sender) &&
                    packet.ownerSteamId == SteamUser.GetSteamID().m_SteamID)
                    {
                    PlayHitConfirm(packet.targetSteamId);
                }
                break;
        }
    }

    private void ProcessShotRequest(CSteamID sender, EggPacket request)
    {
        if (!IsHost() || request == null)
            return;

        // 요청에 적힌 소유자 ID와 실제 메시지 발신자가 일치해야 한다.
        if (request.ownerSteamId != sender.m_SteamID)
        {
            Debug.LogWarning("[EggNetwork] 발신자와 소유자 ID 불일치.");
            return;
        }

        if (!IsLobbyMember(sender))
        {
            Debug.LogWarning("[EggNetwork] 로비 멤버가 아닌 발신자.");
            return;
        }

        if (shootableEggPrefab == null)
        {
            Debug.LogError("[EggNetwork] 투사체 프리팹이 지정되지 않았습니다.");
            return;
        }

        // 잘못된 숫자나 비정상적으로 큰 속도 값은 거부한다.
        if (!IsFinite(request.position) ||
            !IsFinite(request.velocity) ||
            !IsFinite(request.gravity) ||
            request.velocity.sqrMagnitude > 1000000f ||
            request.gravity.sqrMagnitude > 1000000f)
        {
            Debug.LogWarning("[EggNetwork] 잘못된 발사 데이터.");
            return;
        }

        ulong id = nextEggId++;

        EggPacket spawnPacket = new EggPacket
        {
            type = "SPAWN",
            eggId = id,
            ownerSteamId = sender.m_SteamID,
            position = request.position,
            velocity = request.velocity,
            gravity = request.gravity
        };

        // 호스트는 직접 생성하고, 나머지 클라이언트에만 브로드캐스트한다.
        eggOwners[id] = sender.m_SteamID;
        SpawnEgg(spawnPacket);
        BroadcastToOthers(spawnPacket);
    }

    private void SpawnEgg(EggPacket packet)
    {
        if (packet == null || packet.eggId == 0)
            return;

        // 중복 생성 방지
        if (activeEggs.ContainsKey(packet.eggId))
            return;

        if (shootableEggPrefab == null)
        {
            Debug.LogError("[EggNetwork] 투사체 프리팹이 없습니다.");
            return;
        }

        GameObject egg = Instantiate(
            shootableEggPrefab,
            packet.position,
            Quaternion.identity
        );

        Rigidbody rb = egg.GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("[EggNetwork] 투사체에 Rigidbody가 없습니다.");
            Destroy(egg);
            return;
        }

        rb.useGravity = false;
        rb.linearVelocity = packet.velocity;

        EggProjectileMotion motion =
            egg.GetComponent<EggProjectileMotion>();

        if (motion == null)
            motion = egg.AddComponent<EggProjectileMotion>();

        motion.Initialize(packet.gravity);

        ShootableEggCollision collision =
            egg.GetComponent<ShootableEggCollision>();

        if (collision != null)
        {
            collision.InitializeNetworkEgg(packet.eggId, IsHost());
        }

        activeEggs.Add(packet.eggId, egg);

        // 호스트가 최종 제거 명령을 내리므로 클라이언트 타임아웃은
        // 호스트와 동일하게 맞춰 둔다.
        Destroy(egg, maxProjectileLifetime);
    }

    /// <summary>
    /// 호스트에서만 호출한다. 충돌한 알의 제거를 전파한다.
    /// </summary>
    public void ReportCollision(ulong eggId, GameObject hitObject)
    {
        if (!IsHost() || eggId == 0 || hitObject == null)
            return;

        if (!activeEggs.ContainsKey(eggId))
            return;

        if (PlayerCharacterManager.Instance != null &&
            PlayerCharacterManager.Instance.TryGetFarmerOwner(
                hitObject, out ulong farmerOwnerId))
        {
            // 피격된 농부의 화면 가림 효과
            SendBlindToPlayer(farmerOwnerId);

            // 알을 쏜 플레이어의 Steam ID를 한 번만 선언한다.
            if (eggOwners.TryGetValue(eggId, out ulong shooterId))
            {
                // 알 명중 점수 처리
                CommonScoring scoring =
                    FindFirstObjectByType<CommonScoring>();

                if (scoring != null)
                {
                    scoring.AwardEggHitScore(shooterId);
                }

                // 기존 명중 확인 효과 유지
                SendHitConfirm(shooterId, farmerOwnerId);
            }
        }

        EggPacket packet = new EggPacket
        {
            type = "DESTROY",
            eggId = eggId,
            hitObjectName = hitObject.name
        };

        DestroyEgg(eggId);
        BroadcastToOthers(packet);
    }

    private void DestroyEgg(ulong eggId)
    {
        eggOwners.Remove(eggId);
        
        if (!activeEggs.TryGetValue(eggId, out GameObject egg))
            return;

        activeEggs.Remove(eggId);

        if (egg != null)
            Destroy(egg);
    }

    private void BroadcastToOthers(EggPacket packet)
    {
        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            return;
        }

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;
        CSteamID localId = SteamUser.GetSteamID();
        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        string message = Prefix + JsonUtility.ToJson(packet);

        for (int i = 0; i < count; i++)
        {
            CSteamID member =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            if (member == localId)
                continue;

            SteamNetworkMessenger.Instance.SendMessage(member, message);
        }
    }

    private void Send(CSteamID target, EggPacket packet)
    {
        string message = Prefix + JsonUtility.ToJson(packet);

        SteamNetworkMessenger.Instance.SendMessage(target, message);
    }

    private CSteamID GetHostSteamId()
    {
        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            return CSteamID.Nil;
        }

        return SteamMatchmaking.GetLobbyOwner(
            SteamLobby.Instance.CurrentLobbyID
        );
    }

    private bool IsMessageFromHost(CSteamID sender)
    {
        return GetHostSteamId() == sender;
    }

    private bool IsLobbyMember(CSteamID playerId)
    {
        if (SteamLobby.Instance == null ||
            SteamLobby.Instance.CurrentLobbyID == CSteamID.Nil)
        {
            return false;
        }

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;
        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        for (int i = 0; i < count; i++)
        {
            if (SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i) == playerId)
                return true;
        }

        return false;
    }

    private bool IsFinite(Vector3 v)
    {
        return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    }

    private bool IsFinite(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    private void OnDisable()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived
                -= HandleNetworkMessage;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void SendBlindToPlayer(ulong targetSteamId)
    {
        if (!IsHost() || targetSteamId == 0)
            return;

        // 호스트 자신이 맞은 경우
        if (targetSteamId == SteamUser.GetSteamID().m_SteamID)
        {
            ApplyBlindToLocalFarmer();
            return;
        }

        if (SteamNetworkMessenger.Instance == null)
            return;

        EggPacket packet = new EggPacket
        {
            type = "BLIND",
            ownerSteamId = targetSteamId
        };

        Send(new CSteamID(targetSteamId), packet);
    }

    private void ApplyBlindToLocalFarmer()
    {
        if (PlayerCharacterManager.Instance == null)
            return;

        ulong localSteamId = SteamUser.GetSteamID().m_SteamID;

        if (!PlayerCharacterManager.Instance.TryGetFarmer(
            localSteamId,
            out GameObject farmer))
        {
            Debug.LogWarning(
                "[EggNetwork] 로컬 플레이어의 농부를 찾을 수 없습니다."
            );
            return;
        }

        EggShotBlinder blinder =
            farmer.GetComponentInChildren<EggShotBlinder>(true);

        if (blinder == null)
        {
            Debug.LogWarning(
                "[EggNetwork] 농부에게 EggShotBlinder가 없습니다."
            );
            return;
        }   

        blinder.ApplyBlind();
    }

    private void SendHitConfirm(ulong shooterId, ulong farmerId)
    {
        if (!IsHost() || shooterId == 0)
            return;

        // 호스트 자신이 발사자라면 직접 재생
        if (shooterId == SteamUser.GetSteamID().m_SteamID)
        {
            PlayHitConfirm(farmerId);
            return;
        }

        EggPacket packet = new EggPacket
        {
            type = "HIT_CONFIRM",
            ownerSteamId = shooterId,
            targetSteamId = farmerId
        };

        Send(new CSteamID(shooterId), packet);
    }

    private void PlayHitConfirm(ulong farmerSteamId)
    {
        if (PlayerCharacterManager.Instance == null)
            return;

        if (!PlayerCharacterManager.Instance.TryGetFarmer(
            farmerSteamId, out GameObject farmer))
        {
            Debug.LogWarning(
                $"[EggNetwork] 명중한 농부를 찾을 수 없습니다: {farmerSteamId}"
            );
            return;
        }

        Transform[] children =
            farmer.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name != "EggShotBlindParticle")
                continue;

            ParticleSystem particle =
                child.GetComponent<ParticleSystem>();

            if (particle == null)
            {
                Debug.LogWarning(
                    "[EggNetwork] EggShotBlindParticle에 ParticleSystem이 없습니다."
                );
                return;
            }

            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            particle.Play(true);
            return;
        }

        Debug.LogWarning(
            "[EggNetwork] 농부 프리팹에서 EggShotBlindParticle을 찾지 못했습니다."
        );
    }
}
