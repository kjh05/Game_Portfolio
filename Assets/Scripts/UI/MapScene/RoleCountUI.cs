using TMPro;
using UnityEngine;

public class RoleCountUI : MonoBehaviour
{
    [Header("Role Count UI")]
    [SerializeField] private TMP_Text hunterCountText;
    [SerializeField] private TMP_Text chickenCountText;

    private void Update()
    {
        if (GameManager.Instance == null)
        {
            SetCounts(0, 0);
            return;
        }

        GameManager.Instance.GetRoleCounts(
            out int hunterCount,
            out int runnerCount
        );

        SetCounts(hunterCount, runnerCount);
    }

    private void SetCounts(int hunterCount, int runnerCount)
    {
        if (hunterCountText != null)
            hunterCountText.text = hunterCount.ToString();

        if (chickenCountText != null)
            chickenCountText.text = runnerCount.ToString();
    }   
}
