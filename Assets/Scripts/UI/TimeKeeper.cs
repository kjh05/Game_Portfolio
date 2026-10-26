
using System.Collections;
using TMPro;
using UnityEngine;

public class TimeKeeper : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text phaseText;

    [Header("Warning")]
    [SerializeField] private float warningTime = 5f;
    [SerializeField] private Color warningColor = Color.red;

    [Tooltip("1 = 원래 크기, 1.5 = 50% 확대")]
    [SerializeField] private float warningScale = 1.5f;

    [Tooltip("커졌다가 돌아오는 데 걸리는 시간")]
    [SerializeField] private float punchDuration = 0.2f;

    private Color normalColor;
    private Vector3 normalScale;

    private int lastDisplayedSecond = -1;
    private Coroutine punchCoroutine;

    private GameManager.GamePhase lastPhase;
    private bool hasLastPhase;

    private void Awake()
    {
        if (timerText == null)
        {
            Debug.LogError(
                "[TimeKeeper] Timer Text가 연결되지 않았습니다.",
                this
            );
            enabled = false;
            return;
        }

        normalColor = timerText.color;
        normalScale = timerText.transform.localScale;
    }

    private void Update()
    {
        GameManager manager = GameManager.Instance;

        if (manager == null)
        {
            if (phaseText != null)
                phaseText.text = "Waiting for Game";

            return;
        }

        GameManager.GamePhase phase = manager.CurrentPhase;
        float remainingTime = manager.RemainingPhaseTime;

        // 페이즈 이름 표시
        if (phaseText != null)
            phaseText.text = GetPhaseDisplayName(phase);

        // 페이즈가 변경되면 경고 효과 상태를 초기화한다.
        if (!hasLastPhase || phase != lastPhase)
        {
            lastPhase = phase;
            hasLastPhase = true;
            ResetWarningEffect();
        }

        UpdateTimerText(remainingTime);
    }

    private void UpdateTimerText(float currentTime)
    {
        currentTime = Mathf.Max(0f, currentTime);

        int totalSeconds = Mathf.CeilToInt(currentTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{seconds:00}";

        if (currentTime <= warningTime && currentTime > 0f)
        {
            timerText.color = warningColor;

            // 표시되는 초가 바뀔 때만 펀치 효과를 실행한다.
            if (totalSeconds != lastDisplayedSecond)
            {
                lastDisplayedSecond = totalSeconds;
                PlayWarningPunch();
            }
        }
        else
        {
            ResetWarningEffect();
        }
    }

    private string GetPhaseDisplayName(GameManager.GamePhase phase)
    {
        switch (phase)
        {
            case GameManager.GamePhase.Waiting:
                return "INTERMISSION";

            case GameManager.GamePhase.AssigningRoles:
                return "ASSIGNING ROLES";

            case GameManager.GamePhase.MovingChickens:
                return "TELEPORTING CLUCKS..";

            case GameManager.GamePhase.Hiding:
                return "CLCUKS HIDE!";

            case GameManager.GamePhase.MovingFarmers:
                return "HUNTERS ENTERING..";

            case GameManager.GamePhase.Playing:
                return "IN ROUND";

            case GameManager.GamePhase.RoundEnded:
                return "ROUND ENDED";

            default:
                return phase.ToString();
        }
    }

    private void PlayWarningPunch()
    {
        if (punchCoroutine != null)
            StopCoroutine(punchCoroutine);

        punchCoroutine = StartCoroutine(PunchScale());
    }

    private IEnumerator PunchScale()
    {
        float halfDuration = Mathf.Max(0.01f, punchDuration / 2f);
        float elapsed = 0f;

        Vector3 targetScale = normalScale * warningScale;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / halfDuration)
            );

            timerText.transform.localScale =
                Vector3.Lerp(normalScale, targetScale, t);

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / halfDuration)
            );

            timerText.transform.localScale =
                Vector3.Lerp(targetScale, normalScale, t);

            yield return null;
        }

        timerText.transform.localScale = normalScale;
        punchCoroutine = null;
    }

    private void ResetWarningEffect()
    {
        timerText.color = normalColor;
        timerText.transform.localScale = normalScale;
        lastDisplayedSecond = -1;

        if (punchCoroutine != null)
        {
            StopCoroutine(punchCoroutine);
            punchCoroutine = null;
        }
    }

    private void OnDisable()
    {
        if (timerText == null)
            return;

        if (punchCoroutine != null)
        {
            StopCoroutine(punchCoroutine);
            punchCoroutine = null;
        }

        timerText.color = normalColor;
        timerText.transform.localScale = normalScale;
    }
}
