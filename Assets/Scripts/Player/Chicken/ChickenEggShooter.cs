
using UnityEngine;
using UnityEngine.InputSystem;

public class ChickenEggShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChickenNetworkIdentity networkIdentity;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private EggTrajectory eggTrajectory;
    [SerializeField] private ChickenStatus chickenStatus;
    [SerializeField] private GameObject shootableEggPrefab;

    [Header("Launch Settings")]
    [SerializeField] private float eggLifetime = 8f;

    private PlayerControls controls;
    private bool isLocalControl;

    private void Awake()
    {
        if (networkIdentity == null)
            networkIdentity = GetComponent<ChickenNetworkIdentity>();

        if (cameraManager == null)
            cameraManager = GetComponentInChildren<CameraManager>(true);

        if (eggTrajectory == null)
            eggTrajectory = GetComponentInChildren<EggTrajectory>(true);

        if (chickenStatus == null)
            chickenStatus = GetComponent<ChickenStatus>();

        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Chicken.EggFire.performed += OnEggFire;

        if (networkIdentity != null)
            SetLocalControl(networkIdentity.IsLocalPlayer);
    }

    private void OnDisable()
    {
        if (controls == null)
            return;

        controls.Chicken.EggFire.performed -= OnEggFire;
        controls.Disable();
        isLocalControl = false;
    }

    public void SetLocalControl(bool isLocal)
    {
        isLocalControl = isLocal;

        if (controls == null)
            return;

        if (isLocalControl && isActiveAndEnabled)
            controls.Enable();
        else
            controls.Disable();
    }

    private void OnEggFire(InputAction.CallbackContext context)
    {
        if (!isLocalControl)
            return;

        if (networkIdentity == null || !networkIdentity.IsLocalPlayer)
            return;

        if (cameraManager == null || !cameraManager.IsAiming)
            return;

        if (eggTrajectory == null ||
            !eggTrajectory.TryGetLaunchVelocity(out Vector3 velocity))
            return;

        if (chickenStatus == null || chickenStatus.EggAmmo <= 0)
            return;

        if (shootableEggPrefab == null)
        {
            Debug.LogError("[ChickenEggShooter] 투사체 프리팹이 없습니다.");
            return;
        }

        if (EggNetworkManager.Instance == null)
        {
            Debug.LogError("[ChickenEggShooter] EggNetworkManager가 없습니다.");
            return;
        }

        Transform spawnPoint = eggTrajectory.GetEggSpawnPoint();

        if (spawnPoint == null)
        {
            Debug.LogError("[ChickenEggShooter] EggSpawnPoint가 없습니다.");
            return;
        }

        EggNetworkManager.Instance.RequestShot(
            networkIdentity.OwnerSteamId,
            spawnPoint.position,
            velocity,
            eggTrajectory.GetGravity()
        );

        chickenStatus.AddEgg(-1);

        Debug.Log("[ChickenEggShooter] 네트워크 발사 요청!");
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }
}
