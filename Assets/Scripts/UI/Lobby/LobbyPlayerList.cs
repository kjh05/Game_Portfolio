using System.Collections;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class LobbyPlayerList : MonoBehaviour
{
    [Header("Player Slots")]
    [SerializeField] private LobbyPlayerSlot[] playerSlots;

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 1f;

    private readonly List<LobbyPlayerData> players =
        new List<LobbyPlayerData>();

    private Coroutine refreshCoroutine;

    private void OnEnable()
    {
        if (SteamLobby.Instance == null)
        {
            Debug.LogError(
                "SteamLobby를 찾을 수 없습니다."
            );

            return;
        }

        SteamLobby.Instance.OnLobbyPlayersUpdated
            += HandleLobbyPlayersUpdated;

        // 처음 한 번 즉시 갱신
        RefreshPlayerList();

        // 이후 주기적으로 갱신
        refreshCoroutine =
            StartCoroutine(RefreshLoop());
    }

    private void OnDisable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyPlayersUpdated
                -= HandleLobbyPlayersUpdated;
        }

        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                refreshInterval
            );

            RefreshPlayerList();
        }
    }

    private void HandleLobbyPlayersUpdated(
        IReadOnlyList<LobbyPlayerData> updatedPlayers)
    {
        Debug.Log(
            $"LobbyPlayerList 데이터 수신 | " +
            $"Player Count: {updatedPlayers.Count}"
        );

        players.Clear();

        players.AddRange(updatedPlayers);

        DisplayPlayers();
    }

    private void RefreshPlayerList()
    {
        if (SteamLobby.Instance == null)
        {
            return;
        }

        SteamLobby.Instance.RefreshLobbyPlayers();
    }

    private void DisplayPlayers()
    {
        Debug.Log(
            $"LobbyPlayerList UI 갱신 시작 | " +
            $"Players: {players.Count} | " +
            $"Slots: {playerSlots.Length}"
        );

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (i < players.Count)
            {
                Debug.Log(
                    $"Slot [{i}]에 플레이어 배치 | " +
                    $"Name: {players[i].steamName}"
                );

                playerSlots[i].SetPlayer(
                    players[i]
                );
            }
            else
            {
                playerSlots[i].Clear();
            }
        }
    }

    public IReadOnlyList<LobbyPlayerData> GetPlayers()
    {
        return players;
    }
}