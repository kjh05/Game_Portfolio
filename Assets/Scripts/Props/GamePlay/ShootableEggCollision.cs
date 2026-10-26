
using UnityEngine;

public class ShootableEggCollision : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private LayerMask targetLayers = ~0;

    private ulong networkEggId;
    private bool isAuthoritative;
    private bool hasCollided;

    public void InitializeNetworkEgg(ulong eggId, bool authority)
    {
        networkEggId = eggId;
        isAuthoritative = authority;
        hasCollided = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    private void HandleCollision(GameObject other)
    {
        // 클라이언트는 충돌을 최종 판정하지 않는다.
        if (!isAuthoritative || hasCollided || other == null)
            return;

        if ((targetLayers.value & (1 << other.layer)) == 0)
            return;

        hasCollided = true;

        Debug.Log(
            $"[ShootableEgg] 호스트 충돌 판정: {other.name}, " +
            $"Layer: {LayerMask.LayerToName(other.layer)}",
            this
        );

        if (EggNetworkManager.Instance != null)
        {
            EggNetworkManager.Instance.ReportCollision(
                networkEggId,
                other
            );
        }
    }
}
