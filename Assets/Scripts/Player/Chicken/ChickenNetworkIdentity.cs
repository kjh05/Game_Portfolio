
using System.Collections.Generic;
using UnityEngine;

public class ChickenNetworkIdentity : MonoBehaviour
{
    public ulong OwnerSteamId { get; private set; }
    public bool IsLocalPlayer { get; private set; }

    private static readonly Dictionary<ulong, ChickenNetworkIdentity>
        ChickenRegistry = new Dictionary<ulong, ChickenNetworkIdentity>();
    
    [SerializeField] private ChickenEggShooter chickenEggShooter;

    [Header("Input and Movement")]
    [SerializeField] private ChickenLocalInput chickenLocalInput;
    [SerializeField] private ChickenMovement chickenMovement;
    [SerializeField] private ChickenAnimation chickenAnimation;

    [Header("Local-only Components")]
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Camera playerCameraComponent;
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private Canvas ChickenCanvas; // TMP Addition

    private void Awake()
    {
        if (chickenLocalInput == null)
            chickenLocalInput = GetComponent<ChickenLocalInput>();

        if (chickenMovement == null)
            chickenMovement = GetComponent<ChickenMovement>();

        if (chickenAnimation == null)
            chickenAnimation = GetComponent<ChickenAnimation>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<PlayerCamera>(true);

        if (cameraManager == null)
            cameraManager = GetComponentInChildren<CameraManager>(true);

        if (playerCameraComponent == null && playerCamera != null)
            playerCameraComponent =
                playerCamera.GetComponentInChildren<Camera>(true);

        if (audioListener == null && playerCameraComponent != null)
            audioListener =
                playerCameraComponent.GetComponent<AudioListener>();
        
        if (chickenEggShooter == null)
            chickenEggShooter = GetComponent<ChickenEggShooter>();

        if (ChickenCanvas == null)                                  // TMP Addition
            ChickenCanvas = GetComponentInChildren<Canvas>(true);
    }

    public void Initialize(ulong ownerSteamId, bool isLocalPlayer)
    {
        if (OwnerSteamId != 0 &&
            ChickenRegistry.TryGetValue(OwnerSteamId, out var previous) &&
            previous == this)
        {
            ChickenRegistry.Remove(OwnerSteamId);
        }

        OwnerSteamId = ownerSteamId;
        ChickenRegistry[ownerSteamId] = this;

        SetLocalPlayer(isLocalPlayer);
    }

    public static bool TryGetChicken(
        ulong steamId,
        out ChickenNetworkIdentity identity)
    {
        if (ChickenRegistry.TryGetValue(steamId, out identity) &&
            identity != null)
        {
            return true;
        }

        ChickenRegistry.Remove(steamId);
        identity = null;
        return false;
    }

    public void SetLocalPlayer(bool isLocalPlayer)
    {
        IsLocalPlayer = isLocalPlayer;

        if (chickenMovement != null)
            chickenMovement.SetLocalControl(isLocalPlayer);

        if (chickenLocalInput != null)
            chickenLocalInput.SetLocalControl(isLocalPlayer);

        if (chickenAnimation != null)
            chickenAnimation.SetLocalControl(isLocalPlayer);

        if (cameraManager != null)
            cameraManager.SetLocalControl(isLocalPlayer);

        if (playerCamera != null)
        {
            playerCamera.SetLocalControl(isLocalPlayer);
            playerCamera.enabled = isLocalPlayer;
        }

        if (cameraManager != null)
            cameraManager.enabled = isLocalPlayer;

        if (playerCameraComponent != null)
            playerCameraComponent.enabled = isLocalPlayer;

        if (audioListener != null)
            audioListener.enabled = isLocalPlayer;
        
        if (chickenEggShooter != null)
            chickenEggShooter.SetLocalControl(isLocalPlayer);
        
        if (ChickenCanvas != null) // TMP Addition
            ChickenCanvas.enabled = isLocalPlayer;
    }

    private void OnDestroy()
    {
        if (OwnerSteamId != 0 &&
            ChickenRegistry.TryGetValue(OwnerSteamId, out var registered) &&
            registered == this)
        {
            ChickenRegistry.Remove(OwnerSteamId);
        }
    }
}
