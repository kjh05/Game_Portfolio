using UnityEngine;

public class FarmerLook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform rightArmBone;

    [Header("Look Settings")]
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Right Arm Pitch")]
    [SerializeField] private float armPitchMultiplier = 1f;

    private float pitch;
    private Vector2 lookInput;

    private Quaternion initialHeadRotation;
    private Vector3 initialArmEulerAngles;

    private bool hasLocalControl;

    public float CurrentPitch => pitch;

    private void Start()
    {
        if (headBone != null)
            initialHeadRotation = headBone.localRotation;
        else
            Debug.LogWarning(
                "[FarmerLook] Head Bone이 연결되지 않았습니다.",
                this
            );

        if (rightArmBone != null)
            initialArmEulerAngles = rightArmBone.localEulerAngles;
        else
            Debug.LogWarning(
                "[FarmerLook] Right Arm Bone이 연결되지 않았습니다.",
                this
            );
    }

    private void Update()
    {
        if (!hasLocalControl)
            return;

        ApplyLook();
    }

    public void SetLookInput(Vector2 input)
    {
        lookInput = input;
    }

    private void ApplyLook()
    {
        float mouseX = lookInput.x * sensitivity;
        float mouseY = lookInput.y * sensitivity;

        // 좌우 회전은 농부 루트에 적용한다.
        transform.Rotate(Vector3.up * mouseX);

        // 상하 각도를 계산한다.
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        ApplyPitchToBones(pitch);
    }

    public void ApplyRemotePitch(float remotePitch)
    {
        pitch = Mathf.Clamp(remotePitch, minPitch, maxPitch);
        ApplyPitchToBones(pitch);
    }

    private void ApplyPitchToBones(float appliedPitch)
    {
        if (headBone != null)
        {
            headBone.localRotation =
                initialHeadRotation *
                Quaternion.Euler(appliedPitch, 0f, 0f);
        }

        if (rightArmBone != null)
        {
            Vector3 armRotation = initialArmEulerAngles;
            armRotation.x -= appliedPitch * armPitchMultiplier;

            rightArmBone.localRotation =
                Quaternion.Euler(armRotation);
        }
    }

    public void SetLocalControl(bool isLocalPlayer)
    {
        hasLocalControl = isLocalPlayer;

        if (!hasLocalControl)
        {
            lookInput = Vector2.zero;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
