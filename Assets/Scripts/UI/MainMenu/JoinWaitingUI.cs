
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class JoinWaitingUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject publicLobbyPanel;
    [SerializeField] private GameObject roomCodePanel;
    [SerializeField] private GameObject waitingPanel;

    [Header("Progress")]
    [SerializeField] private Slider progressBar;

    [Header("Pre-Join Delay")]
    [SerializeField] private float preJoinDelay = 5f;

    private SteamLobby subscribedLobby;
    private Coroutine subscribeCoroutine;
    private Coroutine preJoinCoroutine;

    private void Awake()
    {
        if (waitingPanel != null)
            waitingPanel.SetActive(false);

        if (progressBar != null)
            progressBar.value = 0f;
    }

    private void OnEnable()
    {
        subscribeCoroutine = StartCoroutine(WaitForSteamLobby());
    }

    private IEnumerator WaitForSteamLobby()
    {
        while (SteamLobby.Instance == null)
            yield return null;

        subscribedLobby = SteamLobby.Instance;

        subscribedLobby.OnLobbyJoinStarted += ShowWaiting;
        subscribedLobby.OnLobbyJoinProgressChanged += UpdateProgress;
        subscribedLobby.OnLobbyJoinFinished += HideWaiting;

        Debug.Log("[JoinWaitingUI] SteamLobby 이벤트 구독 완료");

        subscribeCoroutine = null;
    }

    private void OnDisable()
    {
        if (subscribeCoroutine != null)
        {
            StopCoroutine(subscribeCoroutine);
            subscribeCoroutine = null;
        }

        // UI가 비활성화되면 사전 대기도 중단
        if (preJoinCoroutine != null)
        {
            StopCoroutine(preJoinCoroutine);
            preJoinCoroutine = null;
        }

        UnsubscribeEvents();
    }

    private void UnsubscribeEvents()
    {
        if (subscribedLobby == null)
            return;

        subscribedLobby.OnLobbyJoinStarted -= ShowWaiting;
        subscribedLobby.OnLobbyJoinProgressChanged -= UpdateProgress;
        subscribedLobby.OnLobbyJoinFinished -= HideWaiting;

        subscribedLobby = null;
    }

    // Join 버튼들이 공통으로 호출하는 진입점
    public void BeginJoin(string roomCode)
    {
        if (preJoinCoroutine != null)
        {
            Debug.Log("[JoinWaitingUI] 이미 사전 대기 중입니다.");
            return;
        }

        if (SteamLobby.Instance == null)
        {
            Debug.LogError("[JoinWaitingUI] SteamLobby를 찾을 수 없습니다.");
            return;
        }

        if (string.IsNullOrEmpty(roomCode) || roomCode.Length != 6)
        {
            Debug.LogWarning("[JoinWaitingUI] Room Code가 유효하지 않습니다.");
            return;
        }

        // 검색을 시작하기 전에 먼저 대기 패널 표시
        ShowWaiting();

        // 5초 대기 후 기존 접속 로직 실행
        preJoinCoroutine = StartCoroutine(PreJoinDelay(roomCode));
    }

    private IEnumerator PreJoinDelay(string roomCode)
    {
        yield return new WaitForSeconds(preJoinDelay);

        // 이제부터는 기존 SteamLobby 접속 로직에 맡긴다.
        preJoinCoroutine = null;

        if (SteamLobby.Instance == null)
        {
            Debug.LogError("[JoinWaitingUI] 대기 중 SteamLobby가 사라졌습니다.");
            HideWaiting();
            yield break;
        }

        Debug.Log(
            $"[JoinWaitingUI] 사전 대기 완료. 접속 시도 시작 | Room Code: {roomCode}"
        );

        bool success = SteamLobby.Instance.JoinLobbyByRoomCode(roomCode);

        if (!success)
        {
            Debug.LogWarning("[JoinWaitingUI] 접속 요청을 시작하지 못했습니다.");
            HideWaiting();
        }
    }

    public void ShowWaiting()
    {
        Debug.Log("[JoinWaitingUI] ShowWaiting 호출");

        if (publicLobbyPanel != null)
            publicLobbyPanel.SetActive(false);

        if (roomCodePanel != null)
            roomCodePanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (waitingPanel != null)
            waitingPanel.SetActive(true);

        if (progressBar != null)
            progressBar.value = 0f;
    }

    private void UpdateProgress(float progress)
    {
        if (progressBar != null)
            progressBar.value = Mathf.Clamp01(progress);
    }

    public void HideWaiting()
    {
        Debug.Log("[JoinWaitingUI] HideWaiting 호출");

        if (waitingPanel != null)
            waitingPanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (progressBar != null)
            progressBar.value = 0f;
    }

    public void CancelJoin()
    {
        // 아직 5초 대기 중이면 실제 접속 요청을 보내지 않고 취소
        if (preJoinCoroutine != null)
        {
            StopCoroutine(preJoinCoroutine);
            preJoinCoroutine = null;

            Debug.Log("[JoinWaitingUI] 사전 대기 중 취소됨");

            HideWaiting();
            return;
        }

        // 실제 접속 시도가 시작된 뒤라면 기존 취소 로직 사용
        if (subscribedLobby != null)
            subscribedLobby.CancelLobbyJoinRetry();
    }
}
