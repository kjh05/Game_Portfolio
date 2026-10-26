using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WarningPanelController : MonoBehaviour
{
    [Header("Warning Boxes")]
    [SerializeField] private GameObject enterWarningBox;
    [SerializeField] private GameObject selectWarningBox;
    [SerializeField] private GameObject kickedWarningBox;

    [Header("Display Duration")]
    [SerializeField] private float displayDuration = 2f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        // WarningPanel 자체는 활성화 상태로 유지한다.
        // 내부 경고 상자만 처음에 숨긴다.
        if (enterWarningBox != null)
            enterWarningBox.SetActive(false);

        if (selectWarningBox != null)
            selectWarningBox.SetActive(false);

        if (kickedWarningBox != null)
            kickedWarningBox.SetActive(false);
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
            return;

        const string pendingKickedWarningKey = "pending_kicked_warning";

        if (PlayerPrefs.GetInt(pendingKickedWarningKey, 0) == 1)
        {
            PlayerPrefs.DeleteKey(pendingKickedWarningKey);
            PlayerPrefs.Save();

            ShowKickedWarning();
        }
    }

    public void ShowEnterWarning()
    {
        ShowWarning(enterWarningBox);
    }

    public void ShowSelectWarning()
    {
        ShowWarning(selectWarningBox);
    }

    public void ShowKickedWarning()
    {
        ShowWarning(kickedWarningBox);
    }

    private void ShowWarning(GameObject warningBox)
    {
        if (warningBox == null)
        {
            Debug.LogError(
                "WarningBox가 Inspector에 연결되지 않았습니다."
            );
            return;
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        // 모든 경고 상자를 숨긴 뒤 요청한 경고만 표시한다.
        if (enterWarningBox != null)
            enterWarningBox.SetActive(false);

        if (selectWarningBox != null)
            selectWarningBox.SetActive(false);

        if (kickedWarningBox != null)
            kickedWarningBox.SetActive(false);

        warningBox.SetActive(true);
        hideCoroutine = StartCoroutine(
            HideWarningAfterDelay(warningBox)
        );
    }

    private IEnumerator HideWarningAfterDelay(GameObject warningBox)
    {
        yield return new WaitForSeconds(displayDuration);

        if (warningBox != null)
            warningBox.SetActive(false);

        hideCoroutine = null;
    }
}
