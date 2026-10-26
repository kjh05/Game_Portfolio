using Steamworks;
using UnityEngine;

[DisallowMultipleComponent]
public class FarmerHitDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera farmerCamera;

    [Header("Hit Detection")]
    [SerializeField, Min(0.1f)] private float hitDistance = 100f;
    [SerializeField] private LayerMask hitLayers = ~0;

    /// <summary>
    /// 로컬 플레이어가 발사한 경우 호스트에 발사 번호를 요청한다.
    /// 명중 여부는 호스트에서 다시 판정한다.
    /// </summary>
    public void DetectHit(int fireSequence)
    {
        if (SteamLobby.Instance == null ||
            SteamNetworkMessenger.Instance == null)
            return;

        CSteamID lobbyId = SteamLobby.Instance.CurrentLobbyID;
        if (lobbyId == CSteamID.Nil)
            return;

        CSteamID hostId = SteamMatchmaking.GetLobbyOwner(lobbyId);

        string message = "CHICKEN_HIT_REQUEST|" + fireSequence;

        if (hostId == SteamUser.GetSteamID())
        {
            CommonScoring.Instance?.ProcessChickenHitRequest(
                SteamUser.GetSteamID().m_SteamID,
                fireSequence
            );
        }
        else
        {
            SteamNetworkMessenger.Instance.SendMessage(hostId, message);
        }
    }

    /// <summary>
    /// 호스트에서 호출한다. 이 농부의 현재 조준 방향으로 명중을 검증한다.
    /// </summary>
    public bool TryValidateHit(out ulong chickenSteamId)
    {
        chickenSteamId = 0;

        if (farmerCamera == null)
        {
            Debug.LogWarning(
                "[FarmerHitDetector] 호스트 검증용 카메라가 없습니다.",
                this
            );
            return false;
        }

        Ray ray = farmerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                hitDistance,
                hitLayers,
                QueryTriggerInteraction.Ignore))
            return false;

        ChickenNetworkIdentity chicken =
            hit.collider.GetComponentInParent<ChickenNetworkIdentity>();

        if (chicken == null || chicken.OwnerSteamId == 0)
            return false;

        chickenSteamId = chicken.OwnerSteamId;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (farmerCamera == null)
            return;

        Ray ray = farmerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Gizmos.color = Color.red;
        Gizmos.DrawRay(ray.origin, ray.direction * hitDistance);
    }
}