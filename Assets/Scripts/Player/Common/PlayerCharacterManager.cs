using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어별 캐릭터와 소유권을 관리한다.
/// 최초 닭 생성은 ChickenSpawner가 담당한다.
/// 실제 캐릭터 전환과 네트워크 동기화는 별도 시스템이 담당한다.
/// </summary>
public class PlayerCharacterManager : MonoBehaviour
{
    public static PlayerCharacterManager Instance { get; private set; }

    [System.Serializable]
    public class PlayerCharacterData
    {
        public ulong SteamId { get; private set; }
        public bool IsLocalPlayer { get; private set; }

        public ChickenNetworkIdentity Chicken { get; private set; }

        // FarmerNetworkIdentity 구현 전까지 GameObject로 보관한다.
        public GameObject Farmer { get; private set; }

        // 현재 플레이어가 사용 중인 캐릭터
        public GameObject CurrentCharacter { get; private set; }

        public PlayerCharacterData(
            ulong steamId,
            bool isLocalPlayer,
            ChickenNetworkIdentity chicken)
        {
            SteamId = steamId;
            IsLocalPlayer = isLocalPlayer;
            SetChicken(chicken);
        }

        public void SetChicken(ChickenNetworkIdentity chicken)
        {
            Chicken = chicken;

            if (chicken != null)
                CurrentCharacter = chicken.gameObject;
            else if (CurrentCharacter == null)
                CurrentCharacter = null;
        }

        public void SetFarmer(GameObject farmer)
        {
            Farmer = farmer;
        }

        public void SetCurrentCharacter(GameObject character)
        {
            CurrentCharacter = character;
        }
    }

    private readonly Dictionary<ulong, PlayerCharacterData> players =
        new Dictionary<ulong, PlayerCharacterData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[PlayerCharacterManager] 중복된 매니저가 있습니다.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// ChickenSpawner가 생성한 닭을 등록한다.
    /// </summary>
    public void RegisterChicken(
        ulong steamId,
        bool isLocalPlayer,
        ChickenNetworkIdentity chicken)
    {
        if (steamId == 0)
        {
            Debug.LogWarning(
                "[PlayerCharacterManager] 유효하지 않은 Steam ID입니다."
            );
            return;
        }

        if (chicken == null)
        {
            Debug.LogWarning(
                $"[PlayerCharacterManager] 닭이 null입니다. SteamID: {steamId}"
            );
            return;
        }

        if (players.TryGetValue(steamId, out PlayerCharacterData existing))
        {
            existing.SetChicken(chicken);

            Debug.Log(
                $"[PlayerCharacterManager] 닭 캐릭터 갱신: {steamId}"
            );

            return;
        }

        players.Add(
            steamId,
            new PlayerCharacterData(steamId, isLocalPlayer, chicken)
        );

        Debug.Log(
            $"[PlayerCharacterManager] 플레이어 등록: {steamId}"
        );
    }

    /// <summary>
    /// 해당 플레이어의 농부 캐릭터를 등록한다.
    /// 여기서는 생성이나 전환을 수행하지 않는다.
    /// </summary>
    public bool RegisterFarmer(ulong steamId, GameObject farmer)
    {
        if (farmer == null)
        {
            Debug.LogWarning(
                $"[PlayerCharacterManager] 농부가 null입니다. SteamID: {steamId}"
            );
            return false;
        }

        if (!players.TryGetValue(steamId, out PlayerCharacterData player))
        {
            Debug.LogWarning(
                $"[PlayerCharacterManager] 등록되지 않은 플레이어입니다: {steamId}"
            );
            return false;
        }

        player.SetFarmer(farmer);

        Debug.Log(
            $"[PlayerCharacterManager] 농부 캐릭터 등록: {steamId}"
        );

        return true;
    }

    /// <summary>
    /// 플레이어의 현재 캐릭터 참조를 갱신한다.
    /// 실제 생성, 파괴, 입력 및 카메라 전환은 수행하지 않는다.
    /// </summary>
    public bool SetCurrentCharacter(ulong steamId, GameObject character)
    {
        if (character == null)
        {
            Debug.LogWarning(
                $"[PlayerCharacterManager] 현재 캐릭터가 null입니다: {steamId}"
            );
            return false;
        }

        if (!players.TryGetValue(steamId, out PlayerCharacterData player))
        {
            Debug.LogWarning(
                $"[PlayerCharacterManager] 등록되지 않은 플레이어입니다: {steamId}"
            );
            return false;
        }

        player.SetCurrentCharacter(character);

        Debug.Log(
            $"[PlayerCharacterManager] 현재 캐릭터 변경: {steamId} → {character.name}"
        );

        return true;
    }

    public bool TryGetPlayer(
        ulong steamId,
        out PlayerCharacterData player)
    {
        if (players.TryGetValue(steamId, out player) && player != null)
            return true;

        player = null;
        return false;
    }

    public bool TryGetChicken(
        ulong steamId,
        out ChickenNetworkIdentity chicken)
    {
        chicken = null;

        if (!TryGetPlayer(steamId, out PlayerCharacterData player))
            return false;

        chicken = player.Chicken;
        return chicken != null;
    }

    public bool TryGetCurrentCharacter(
        ulong steamId,
        out GameObject character)
    {
        character = null;

        if (!TryGetPlayer(steamId, out PlayerCharacterData player))
            return false;

        character = player.CurrentCharacter;
        return character != null;
    }

    public bool TryGetFarmer(
        ulong steamId,
        out GameObject farmer)
    {
        farmer = null;

        if (!TryGetPlayer(steamId, out PlayerCharacterData player))
            return false;

        farmer = player.Farmer;
        return farmer != null;
    }

    public void UnregisterPlayer(ulong steamId)
    {
        if (players.Remove(steamId))
        {
            Debug.Log(
                $"[PlayerCharacterManager] 플레이어 등록 해제: {steamId}"
            );
        }
    }
    /*
    public bool TryGetFarmerOwner(
        GameObject hitObject,
        out ulong ownerSteamId)
    {
        ownerSteamId = 0;

        if (hitObject == null)
            return false;

        Transform hitTransform = hitObject.transform;

        foreach (KeyValuePair<ulong, PlayerCharacterData> entry in players)
        {
            GameObject farmer = entry.Value.Farmer;

            if (farmer == null)
                continue;

            Transform farmerTransform = farmer.transform;

            if (hitTransform == farmerTransform ||
                hitTransform.IsChildOf(farmerTransform))
            {
                ownerSteamId = entry.Key;
                return true;
            }
        }
        return false;
    }
    */
    public bool TryGetFarmerOwner(
        GameObject hitObject,
        out ulong farmerOwnerId)
    {
        farmerOwnerId = 0;

        if (hitObject == null)
            return false;

        Transform current = hitObject.transform;

        while (current != null)
        {
            foreach (var entry in players)
            {
                if (entry.Value.Farmer == current.gameObject)
                {
                    farmerOwnerId = entry.Key;
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }
}
