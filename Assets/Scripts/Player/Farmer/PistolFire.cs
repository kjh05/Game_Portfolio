
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PistolFire : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform pistolBody;
    [SerializeField] private Transform slide;
    [SerializeField] private Light muzzleLight;
    [SerializeField] private FarmerHitDetector hitDetector;

    [Header("Fire Cooldown")]
    [SerializeField, Min(0f)] private float fireCooldown = 0.3f;

    [Header("Slide Settings")]
    [SerializeField] private float slideBackZ = -17.2f;
    [SerializeField, Min(0.01f)] private float slideCycleDuration = 0.12f;

    [Header("Muzzle Flash")]
    [SerializeField, Min(0.01f)] private float muzzleLightDuration = 0.05f;

    [Header("Recoil")]
    [SerializeField] private float recoilAngle = -20f;
    [SerializeField, Min(0.01f)] private float recoilDuration = 0.12f;

    private Vector3 initialSlidePosition;
    private Quaternion initialBodyRotation;

    private Coroutine slideCoroutine;
    private Coroutine muzzleCoroutine;
    private Coroutine recoilCoroutine;

    private float nextFireTime;
    private bool hasLocalControl;

    public int FireSequence { get; private set; }

    private void Awake()
    {
        if (slide != null)
            initialSlidePosition = slide.localPosition;

        if (pistolBody != null)
            initialBodyRotation = pistolBody.localRotation;

        if (hitDetector == null)
            hitDetector = GetComponentInParent<FarmerHitDetector>();

        if (muzzleLight != null)
            muzzleLight.enabled = false;
    }

    public void SetLocalControl(bool isLocalPlayer)
    {
        hasLocalControl = isLocalPlayer;
    }

    /// <summary>
    /// FarmerLocalInput의 Fire 액션에서 호출한다.
    /// </summary>
    public void Fire()
    {
        if (!hasLocalControl)
            return;

        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + fireCooldown;
        FireSequence++;

        PlayFireEffects();
        hitDetector?.DetectHit(FireSequence);
    }

    public void PlayRemoteFireEffects()
    {
        // 원격 플레이어의 시각 효과 전용.
        // 로컬 입력 및 쿨타임 검사는 하지 않는다.
        PlayFireEffects();
    }

    private void PlayFireEffects()
    {
        if (slide != null)
        {
            if (slideCoroutine != null)
                StopCoroutine(slideCoroutine);

            slideCoroutine = StartCoroutine(AnimateSlide());
        }

        if (muzzleLight != null)
        {
            if (muzzleCoroutine != null)
                StopCoroutine(muzzleCoroutine);

            muzzleCoroutine = StartCoroutine(ShowMuzzleFlash());
        }

        if (pistolBody != null)
        {
            if (recoilCoroutine != null)
                StopCoroutine(recoilCoroutine);

            recoilCoroutine = StartCoroutine(AnimateRecoil());
        }
    }

    private IEnumerator AnimateSlide()
    {
        Vector3 backPosition = initialSlidePosition;
        backPosition.z = slideBackZ;

        float halfDuration = slideCycleDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);

            slide.localPosition = Vector3.Lerp(
                initialSlidePosition,
                backPosition,
                t
            );

            yield return null;
        }

        slide.localPosition = backPosition;
        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);

            slide.localPosition = Vector3.Lerp(
                backPosition,
                initialSlidePosition,
                t
            );

            yield return null;
        }

        slide.localPosition = initialSlidePosition;
        slideCoroutine = null;
    }

    private IEnumerator ShowMuzzleFlash()
    {
        muzzleLight.enabled = true;

        yield return new WaitForSeconds(muzzleLightDuration);

        muzzleLight.enabled = false;
        muzzleCoroutine = null;
    }

    private IEnumerator AnimateRecoil()
    {
        float halfDuration = recoilDuration * 0.5f;
        float elapsed = 0f;

        Quaternion recoilRotation =
            initialBodyRotation *
            Quaternion.Euler(recoilAngle, 0f, 0f);

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);

            pistolBody.localRotation = Quaternion.Slerp(
                initialBodyRotation,
                recoilRotation,
                t
            );

            yield return null;
        }

        pistolBody.localRotation = recoilRotation;
        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);

            pistolBody.localRotation = Quaternion.Slerp(
                recoilRotation,
                initialBodyRotation,
                t
            );

            yield return null;
        }

        pistolBody.localRotation = initialBodyRotation;
        recoilCoroutine = null;
    }

    private void OnDisable()
    {
        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        if (muzzleCoroutine != null)
            StopCoroutine(muzzleCoroutine);

        if (recoilCoroutine != null)
            StopCoroutine(recoilCoroutine);

        if (slide != null)
            slide.localPosition = initialSlidePosition;

        if (pistolBody != null)
            pistolBody.localRotation = initialBodyRotation;

        if (muzzleLight != null)
            muzzleLight.enabled = false;

        slideCoroutine = null;
        muzzleCoroutine = null;
        recoilCoroutine = null;
    }
}
