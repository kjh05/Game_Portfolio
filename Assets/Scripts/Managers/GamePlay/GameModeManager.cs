
using System;
using Steamworks;
using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    [Header("Game Mode Prefabs")]
    [SerializeField] private GameObject classicPrefab;
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private GameObject passTheTorchPrefab;

    private GameObject spawnedModeInstance;
    private bool hasAttemptedInitialization;

    private void Start()
    {
        InitializeGameMode();
    }

    private void InitializeGameMode()
    {
        // 중복 초기화 방지
        if (hasAttemptedInitialization)
        {
            return;
        }

        hasAttemptedInitialization = true;

        // SteamLobby 확인
        if (SteamLobby.Instance == null)
        {
            Debug.LogError(
                "GameModeManager: SteamLobby.Instance가 없습니다."
            );
            return;
        }

        // 현재 로비 ID 확인
        CSteamID lobbyID = SteamLobby.Instance.CurrentLobbyID;

        if (lobbyID == CSteamID.Nil)
        {
            Debug.LogError(
                "GameModeManager: 유효한 Lobby ID가 없습니다."
            );
            return;
        }

        // Steam Lobby Data에서 게임 모드 읽기
        string gameModeString = SteamMatchmaking.GetLobbyData(
            lobbyID,
            "game_mode"
        );

        if (string.IsNullOrWhiteSpace(gameModeString))
        {
            Debug.LogError(
                "GameModeManager: Lobby Data의 game_mode가 비어 있습니다."
            );
            return;
        }

        // 문자열을 GameMode enum으로 변환
        if (!Enum.TryParse(
                gameModeString,
                true,
                out GameMode gameMode) ||
            !Enum.IsDefined(typeof(GameMode), gameMode))
        {
            Debug.LogError(
                $"GameModeManager: 알 수 없는 게임 모드입니다. " +
                $"값: {gameModeString}"
            );
            return;
        }

        // 모드에 맞는 프리팹 선택
        GameObject selectedPrefab = GetModePrefab(gameMode);

        if (selectedPrefab == null)
        {
            Debug.LogError(
                $"GameModeManager: {gameMode} 프리팹이 연결되지 않았습니다."
            );
            return;
        }

        // GameModeManager의 자식으로 프리팹 생성
        spawnedModeInstance = Instantiate(
            selectedPrefab,
            transform,
            false
        );

        spawnedModeInstance.name = selectedPrefab.name;

        Debug.Log(
            $"GameModeManager: 게임 모드 초기화 완료 | " +
            $"Mode: {gameMode} | " +
            $"Prefab: {selectedPrefab.name}"
        );
    }

    private GameObject GetModePrefab(GameMode gameMode)
    {
        switch (gameMode)
        {
            case GameMode.Classic:
                return classicPrefab;

            case GameMode.Zombie:
                return zombiePrefab;

            case GameMode.PassTheTorch:
                return passTheTorchPrefab;

            default:
                return null;
        }
    }
}
