using UnityEngine;

public class FPSLook : MonoBehaviour
{
    [Header("Rotation Source")]
    [SerializeField] private Transform rotationSource;

    [Header("Model")]
    [SerializeField] private Transform model;

    private Quaternion modelInitialRotation;

    private void Awake()
    {
        if (model != null)
        {
            modelInitialRotation = model.localRotation;
        }
    }

    private void LateUpdate()
    {
        if (rotationSource == null || model == null)
            return;
    }
}