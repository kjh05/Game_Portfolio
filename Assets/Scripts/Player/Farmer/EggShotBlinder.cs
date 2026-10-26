
using System.Collections;
using UnityEngine;

public class EggShotBlinder : MonoBehaviour
{
    [Header("Blind Effect")]
    [SerializeField] private GameObject blindOverlay;
    [SerializeField] private float duration = 2f;

    private Coroutine blindCoroutine;

    private void Awake()
    {
        if (blindOverlay == null)
        {
            Debug.LogError(
                "[EggShotBlinder] Blind Overlay가 지정되지 않았습니다.",
                this
            );
            return;
        }

        blindOverlay.SetActive(false);
    }

    public void ApplyBlind()
    {
        if (blindOverlay == null)
            return;

        if (blindCoroutine != null)
            StopCoroutine(blindCoroutine);

        blindOverlay.SetActive(true);
        blindCoroutine = StartCoroutine(BlindTimer());
    }

    private IEnumerator BlindTimer()
    {
        yield return new WaitForSeconds(duration);

        if (blindOverlay != null)
            blindOverlay.SetActive(false);

        blindCoroutine = null;
    }

    private void OnDisable()
    {
        if (blindCoroutine != null)
        {
            StopCoroutine(blindCoroutine);
            blindCoroutine = null;
        }

        if (blindOverlay != null)
            blindOverlay.SetActive(false);
    }
}
