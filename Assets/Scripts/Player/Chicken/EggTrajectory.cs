
using UnityEngine;

public class EggTrajectory : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform eggSpawnPoint;
    [SerializeField] private Transform aimCamera;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private ChickenNetworkIdentity networkIdentity;

    [Header("Trajectory Settings")]
    [SerializeField, Min(0.1f)] private float launchSpeed = 12f;
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [SerializeField, Min(0.01f)] private float timeStep = 0.08f;

    [Header("Aim Angle")]
    [SerializeField, Range(1f, 15f)]
    private float verticalAimWeight = 1.2f;

    [SerializeField, Min(0f)]
    private float verticalLaunchBoost = 3f;

    private Transform[] dots;

    private void Awake()
    {
        InitializeDots();
        HideTrajectory();
    }

    private void LateUpdate()
    {
        if (eggSpawnPoint == null ||
            aimCamera == null ||
            cameraManager == null ||
            networkIdentity == null)
        {
            HideTrajectory();
            return;
        }

        bool shouldShow =
            networkIdentity.IsLocalPlayer &&
            cameraManager.IsAiming;

        if (shouldShow)
        {
            UpdateTrajectory();
            ShowTrajectory();
        }
        else
        {
            HideTrajectory();
        }
    }

    private void InitializeDots()
    {
        // Trajectory 바로 아래에 미리 배치한 자식 오브젝트를 사용한다.
        int childCount = transform.childCount;
        dots = new Transform[childCount];

        for (int i = 0; i < childCount; i++)
        {
            dots[i] = transform.GetChild(i);

            // 시작 시 모든 궤적 점을 숨긴다.
            dots[i].gameObject.SetActive(false);
        }

        if (dots.Length != 25)
        {
            Debug.LogWarning(
                $"[EggTrajectory] Trajectory 아래 자식 오브젝트가 " +
                $"{dots.Length}개야. 궤적 점 25개를 배치했는지 확인해 줘.",
                this
            );
        }
    }

    private void UpdateTrajectory()
    {
        if (dots == null || eggSpawnPoint == null || aimCamera == null)
            return;

        Vector3 startPosition = eggSpawnPoint.position;
        Vector3 aimDirection = aimCamera.forward;

        // 수직 조준 성분에 가중치를 부여한다.
        aimDirection.y *= verticalAimWeight;
        aimDirection.Normalize();

        Vector3 initialVelocity = aimDirection * launchSpeed;

        // 궤적 계산에만 추가 상승 속도를 부여한다.
        initialVelocity.y += verticalLaunchBoost;

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
                continue;

            float t = (i + 1) * timeStep;

            Vector3 position =
                startPosition +
                initialVelocity * t +
                0.5f * gravity * t * t;

            dots[i].position = position;
        }
    }

    private void ShowTrajectory()
    {
        if (dots == null)
            return;

        foreach (Transform dot in dots)
        {
            if (dot != null && !dot.gameObject.activeSelf)
                dot.gameObject.SetActive(true);
        }
    }

    private void HideTrajectory()
    {
        if (dots == null)
            return;

        foreach (Transform dot in dots)
        {
            if (dot != null && dot.gameObject.activeSelf)
                dot.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        HideTrajectory();
    }
    
    public bool TryGetLaunchVelocity(out Vector3 velocity)
    {
        velocity = Vector3.zero;

        if (eggSpawnPoint == null || aimCamera == null ||
            cameraManager == null || !cameraManager.IsAiming)
        {
            return false;
        }

        Vector3 direction = aimCamera.forward;
        direction.y *= verticalAimWeight;
        direction.Normalize();

        velocity = direction * launchSpeed;
        velocity.y += verticalLaunchBoost;

        return true;
    }
    
    public Vector3 GetGravity()
    {
        return gravity;
    }

    public Transform GetEggSpawnPoint()
    {
        return eggSpawnPoint;
    }
}
