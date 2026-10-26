using UnityEngine;

public class FarmerHeadVisibility : MonoBehaviour
{
    [Header("Local Player Head")]
    [SerializeField] private Renderer headRenderer;

    public void SetLocalPlayer(bool isLocalPlayer)
    {
        if (headRenderer == null)
        {
            Debug.LogWarning(
                "[FarmerHeadVisibility] Head Renderer가 연결되지 않았습니다.",
                this
            );
            return;
        }

        // 로컬 플레이어의 머리를 숨긴다.
        headRenderer.enabled = !isLocalPlayer;
    }
}