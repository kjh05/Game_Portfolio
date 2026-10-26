
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EggProjectileMotion : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 gravity;
    private bool initialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(Vector3 projectileGravity)
    {
        gravity = projectileGravity;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || rb == null)
            return;

        // EggTrajectory에서 계산한 중력과 동일하게 적용한다.
        rb.AddForce(gravity, ForceMode.Acceleration);
    }
}
