
using System;
using Steamworks;
using UnityEngine;

public class CommonScoring : MonoBehaviour
{
    private const string ScorePrefix = "COMMON_SCORE|";

    public static CommonScoring Instance { get; private set; }
    private const string ChickenHitPrefix = "CHICKEN_HIT_REQUEST|";
    private readonly System.Collections.Generic.Dictionary<ulong, int>
        lastProcessedFireSequence =
            new System.Collections.Generic.Dictionary<ulong, int>();

    [Header("Common Scoring Rules")]
    [SerializeField] private int EggHitPoint = 1;
    [SerializeField, Min(0)] private int ChickenKillPoint = 1;

    [Header("Survive Time Bonus")]
    [SerializeField, Min(0f)] private float MaxDist = 20f;
    [SerializeField, Min(0)] private int SurviveTimePoint = 1;
    [SerializeField, Min(0.1f)] private float BonusPointInterval = 5f;    

    private float surviveBonusTimer;

    [Serializable]
    private class ScorePacket
    {
        public string lobbyId;
        public string playerSteamId;
        public int score;
    }

    private void OnEnable()
    {
        SubscribeToMessenger();
    }

    private void Start()
    {
        // Messenger의 실행 순서와 관계없이 구독을 보장한다.
        SubscribeToMessenger();
    }

    private void OnDisable()
    {
        if (SteamNetworkMessenger.Instance != null)
        {
            SteamNetworkMessenger.Instance.OnMessageReceived
                -= HandleNetworkMessage;
        }
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

    private void Update()
    {
        // 점수 판정은 호스트만 수행한다.
        if (SteamLobby.Instance == null ||
            !SteamLobby.Instance.IsCurrentLobbyHost())
        {
            return;
        }

        // 본 게임 중에만 생존 보너스를 계산한다.
        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentPhase != GameManager.GamePhase.Playing)
        {
            surviveBonusTimer = 0f;
            return;
        }

        surviveBonusTimer += Time.deltaTime;

        if (surviveBonusTimer < BonusPointInterval)
            return;

        // 프레임 지연이 있더라도 지급 간격의 누적 오차를 줄인다.
        surviveBonusTimer -= BonusPointInterval;

        AwardSurviveTimeBonus();
    }

    private void SubscribeToMessenger()
    {
        if (SteamNetworkMessenger.Instance == null)
            return;

        SteamNetworkMessenger.Instance.OnMessageReceived
            -= HandleNetworkMessage;

        SteamNetworkMessenger.Instance.OnMessageReceived
            += HandleNetworkMessage;
    }

    public bool AwardEggHitScore(ulong shooterSteamId)
    {
        if (SteamLobby.Instance == null ||
            !SteamLobby.Instance.IsCurrentLobbyHost())
        {
            return false;
        }

        if (!SteamLobby.Instance.TryGetLobbyPlayerData(
                shooterSteamId,
                out LobbyPlayerData playerData))
        {
            Debug.LogWarning(
                $"[Scoring] 플레이어를 찾을 수 없습니다: {shooterSteamId}"
            );
            return false;
        }

        int newScore = playerData.score + EggHitPoint;

        if (!SteamLobby.Instance.SetPlayerScore(
                shooterSteamId,
                newScore))
        {
            return false;
        }

        BroadcastScore(shooterSteamId, newScore);
        return true;
    }

    public bool AwardChickenKillScore(ulong farmerSteamId)
    {
        if (SteamLobby.Instance == null ||
            !SteamLobby.Instance.IsCurrentLobbyHost())
        {
            return false;
        }

        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentPhase !=
                GameManager.GamePhase.Playing)
        {
            return false;
        }

        // 점수를 받을 플레이어가 실제 농부(Hunter)인지 확인한다.
        if (!GameManager.Instance.TryGetRole(
                farmerSteamId,
                out GameManager.PlayerRole role) ||
            role != GameManager.PlayerRole.Hunter)
        {
            Debug.LogWarning(
                $"[Scoring] 농부가 아닌 플레이어의 처치 점수를 거부했습니다: " +
                farmerSteamId
            );
            return false;
        }

        if (!SteamLobby.Instance.TryGetLobbyPlayerData(
                farmerSteamId,
                out LobbyPlayerData playerData))
        {
            Debug.LogWarning(
                $"[Scoring] 플레이어를 찾을 수 없습니다: {farmerSteamId}"
            );
            return false;
        }

        int newScore = playerData.score + ChickenKillPoint;

        if (!SteamLobby.Instance.SetPlayerScore(
                farmerSteamId,
                newScore))
        {
            return false;
        }

        BroadcastScore(farmerSteamId, newScore);

        Debug.Log(
            $"[Scoring] 닭 처치 | {playerData.steamName} | " +
            $"획득 점수: {ChickenKillPoint} | 총점: {newScore}"
        );

        return true;
    }

    public void ProcessChickenHitRequest(
        ulong farmerSteamId,
        int fireSequence)
    {
        if (SteamLobby.Instance == null ||
            !SteamLobby.Instance.IsCurrentLobbyHost())
            return;

        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentPhase != GameManager.GamePhase.Playing)
            return;

        if (!GameManager.Instance.TryGetRole(
                farmerSteamId, out GameManager.PlayerRole farmerRole) ||
            farmerRole != GameManager.PlayerRole.Hunter)
            return;

        // 발사 번호 중복 또는 역순 요청 차단
        if (fireSequence <= 0)
            return;

        if (lastProcessedFireSequence.TryGetValue(
                farmerSteamId, out int lastSequence) &&
            fireSequence <= lastSequence)
            return;

        // 발사 번호는 명중 여부와 관계없이 한 번만 처리한다.
        lastProcessedFireSequence[farmerSteamId] = fireSequence;

        if (PlayerCharacterManager.Instance == null ||
            !PlayerCharacterManager.Instance.TryGetFarmer(
                farmerSteamId, out GameObject farmer) ||
            farmer == null)
            return;

        FarmerHitDetector detector =
            farmer.GetComponentInChildren<FarmerHitDetector>(true);

        if (detector == null ||
            !detector.TryValidateHit(out ulong chickenSteamId))
        {
            Debug.Log("[Scoring] 호스트 명중 검증 실패");
            return;
        }

        if (!GameManager.Instance.TryGetRole(
                chickenSteamId, out GameManager.PlayerRole chickenRole) ||
            chickenRole != GameManager.PlayerRole.Runner)
            return;

        // 실제 Raycast 명중이 확인된 경우에만 점수를 지급한다.
        AwardChickenKillScore(farmerSteamId);
    }

    private void AwardSurviveTimeBonus()
    {
        if (SteamLobby.Instance == null ||
            GameManager.Instance == null ||
            PlayerCharacterManager.Instance == null)
        {
            return;
        }

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;

        if (lobbyId == CSteamID.Nil)
            return;

        int memberCount =
            SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        // 현재 로비에 있는 모든 플레이어를 확인한다.
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID member =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            ulong steamId = member.m_SteamID;

            // 닭(Runner)에게만 생존 보너스를 지급한다.
            if (!GameManager.Instance.TryGetRole(
                    steamId, out GameManager.PlayerRole role) ||
                role != GameManager.PlayerRole.Runner)
            {
                continue;
            }

            // 해당 플레이어의 닭을 찾는다.
            if (!PlayerCharacterManager.Instance.TryGetPlayer(
                    steamId, out var player) ||
                player == null ||
                player.Chicken == null ||
                !player.Chicken.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 chickenPosition = player.Chicken.transform.position;

            float nearestFarmerDist = float.PositiveInfinity;

            // 모든 Hunter(Farmer)를 확인해 가장 가까운 농부를 찾는다.
            for (int j = 0; j < memberCount; j++)
            {
                CSteamID farmerMember =
                    SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, j);

                ulong farmerSteamId = farmerMember.m_SteamID;

                if (!GameManager.Instance.TryGetRole(
                        farmerSteamId, out GameManager.PlayerRole farmerRole) ||
                    farmerRole != GameManager.PlayerRole.Hunter)
                {
                    continue;
                }

                if (!PlayerCharacterManager.Instance.TryGetFarmer(
                        farmerSteamId, out GameObject farmer) ||
                    farmer == null ||
                    !farmer.activeInHierarchy)
                {
                    continue;
                }

                float distance = Vector3.Distance(
                    chickenPosition,
                    farmer.transform.position
                );

                if (distance < nearestFarmerDist)
                    nearestFarmerDist = distance;
            }

            // 유효한 농부를 찾지 못했다면 이번 지급은 건너뛴다.
            if (float.IsPositiveInfinity(nearestFarmerDist))
                continue;

            float distanceFactor = Mathf.Max(
                MaxDist - nearestFarmerDist,
                0f
            );

            int bonus = Mathf.RoundToInt(
                SurviveTimePoint * distanceFactor
            );

            // 최대 거리 이상이면 0점이므로 갱신하지 않는다.
            if (bonus <= 0)
                continue;

            if (!SteamLobby.Instance.TryGetLobbyPlayerData(
                    steamId, out LobbyPlayerData playerData))
            {
                continue;
            }

            int newScore = playerData.score + bonus;

            if (!SteamLobby.Instance.SetPlayerScore(steamId, newScore))
                continue;

            // 기존 점수 전송 경로를 그대로 사용한다.
            BroadcastScore(steamId, newScore);

            Debug.Log(
                $"[Scoring] 생존 보너스 | {playerData.steamName} | " +
                $"최근접 농부 거리: {nearestFarmerDist:F2} | " +
                $"획득 점수: {bonus} | 총점: {newScore}"
            );
        }
    }

    private void BroadcastScore(ulong playerSteamId, int score)
    {
        if (SteamLobby.Instance == null ||
            SteamNetworkMessenger.Instance == null)
        {
            return;
        }

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;

        if (lobbyId == CSteamID.Nil ||
            !SteamLobby.Instance.IsCurrentLobbyHost())
        {
            return;
        }

        ScorePacket packet = new ScorePacket
        {
            lobbyId = lobbyId.m_SteamID.ToString(),
            playerSteamId = playerSteamId.ToString(),
            score = score
        };

        string message =
            ScorePrefix + JsonUtility.ToJson(packet);

        int memberCount =
            SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        CSteamID localId = SteamUser.GetSteamID();

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID member =
                SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            if (member == localId)
                continue;

            SteamNetworkMessenger.Instance.SendMessage(
                member,
                message
            );
        }
    }

    private void HandleNetworkMessage(
        CSteamID sender,
        string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (message.StartsWith(ChickenHitPrefix))
        {
            if (SteamLobby.Instance == null ||
                !SteamLobby.Instance.IsCurrentLobbyHost())
                return;

            CSteamID hitLobbyId = SteamLobby.Instance.CurrentLobbyID;
            if (hitLobbyId == CSteamID.Nil)
                return;

            if (SteamMatchmaking.GetLobbyOwner(hitLobbyId) !=
                SteamUser.GetSteamID())
                return;

            bool isLobbyMember = false;
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(hitLobbyId);

            for (int i = 0; i < memberCount; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(hitLobbyId, i) == sender)
                {
                    isLobbyMember = true;
                    break;
                }
            }

            if (!isLobbyMember)
                return;

            if (!int.TryParse(
                    message.Substring(ChickenHitPrefix.Length),
                    out int fireSequence))
                return;

            ProcessChickenHitRequest(sender.m_SteamID, fireSequence);
            return;
        }

        if (!message.StartsWith(ScorePrefix))
            return;

        if (SteamLobby.Instance == null)
            return;

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;

        if (lobbyId == CSteamID.Nil)
            return;

        // 점수 메시지는 현재 로비의 호스트만 보낼 수 있다.
        CSteamID hostId =
            SteamMatchmaking.GetLobbyOwner(lobbyId);

        if (sender != hostId)
        {
            Debug.LogWarning(
                "[Scoring] 호스트가 아닌 발신자의 점수 메시지를 거부했습니다."
            );
            return;
        }

        string json = message.Substring(ScorePrefix.Length);
        ScorePacket packet;

        try
        {
            packet = JsonUtility.FromJson<ScorePacket>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[Scoring] 점수 패킷 파싱 실패: {exception.Message}"
            );
            return;
        }

        if (packet == null ||
            packet.lobbyId != lobbyId.m_SteamID.ToString() ||
            !ulong.TryParse(packet.playerSteamId, out ulong playerSteamId))
        {
            return;
        }

        if (!SteamLobby.Instance.TryGetLobbyPlayerData(
                playerSteamId,
                out LobbyPlayerData playerData))
        {
            Debug.LogWarning(
                $"[Scoring] 점수 대상이 현재 로비에 없습니다: {playerSteamId}"
            );
            return;
        }

        if (!SteamLobby.Instance.SetPlayerScore(
                playerSteamId,
                packet.score))
        {
            return;
        }

        Debug.Log(
            $"[Scoring] 점수 동기화 완료 | " +
            $"{playerData.steamName}: {packet.score}"
        );
    }
}
