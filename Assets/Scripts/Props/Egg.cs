using UnityEngine;
using System.Collections;

public class Egg : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string shakeAnimationName = "Armature|Side_Shake";

    [Header("Timing")]
    [SerializeField] private float shakeToJumpDelay = 0.8f;
    [SerializeField] private float rotationToShakeDelay = 0.8f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1f;
    [SerializeField] private float jumpDuration = 0.7f;

    [Header("Rotation")]
    [SerializeField] private float rotationAngle = 90f;
    [SerializeField] private float rotationDuration = 0.1f;

    [Header("Respawn")]
    [SerializeField] private float respawnTime = 5f;

    [Header("References")]
    [SerializeField] private GameObject eggVisual;
    [SerializeField] private Collider eggCollider;

    private Vector3 startLocalPosition;
    private bool isCollected;

    private void Start()
    {
        startLocalPosition = transform.localPosition;

        StartCoroutine(EggAnimationLoop());
    }

    private IEnumerator EggAnimationLoop()
    {
        while (true)
        {
            yield return PlayShake();
            yield return new WaitForSeconds(shakeToJumpDelay);
            yield return Jump();
            yield return RotateEgg();
            yield return new WaitForSeconds(rotationToShakeDelay);
        }
    }

    private IEnumerator PlayShake()
    {
        animator.Play(shakeAnimationName, 0, 0f);

        yield return null;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        float duration = stateInfo.length / animator.speed;

        yield return new WaitForSeconds(duration);
    }

    private IEnumerator Jump()
    {
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / jumpDuration);

            float height = Mathf.Sin(t * Mathf.PI) * jumpHeight;

            transform.localPosition =
                startLocalPosition + Vector3.up * height;

            yield return null;
        }

        transform.localPosition = startLocalPosition;
    }

    private IEnumerator RotateEgg()
    {
        Quaternion startRotation = transform.localRotation;

        Quaternion targetRotation =
            startRotation * Quaternion.Euler(0f, rotationAngle, 0f);

        float elapsed = 0f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / rotationDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.localRotation =
                Quaternion.Slerp(startRotation, targetRotation, t);

            yield return null;
        }

        transform.localRotation = targetRotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected)
            return;

        ChickenStatus status =
            other.GetComponentInParent<ChickenStatus>();

        if (status == null)
            return;

        if (status.EggAmmo >= status.MaxEggAmmo)
            return;

        status.AddEgg(1);

        CollectEgg();
    }

    private void CollectEgg()
    {
        isCollected = true;

        eggVisual.SetActive(false);
        eggCollider.enabled = false;

        StartCoroutine(RespawnEgg());
    }

    private IEnumerator RespawnEgg()
    {
        yield return new WaitForSeconds(respawnTime);

        eggVisual.SetActive(true);
        eggCollider.enabled = true;

        isCollected = false;
    }
}