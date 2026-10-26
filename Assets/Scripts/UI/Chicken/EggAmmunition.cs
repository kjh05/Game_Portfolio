using UnityEngine;
using TMPro;

public class EggAmmunition : MonoBehaviour
{
    [SerializeField] private ChickenStatus chickenStatus;
    [SerializeField] private TMP_Text ammoText;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color maxAmmoColor = Color.red;

    private void Awake()
    {
        if (chickenStatus == null)
            chickenStatus = GetComponentInParent<ChickenStatus>();

        if (ammoText == null)
            ammoText = GetComponentInChildren<TMP_Text>(true);
    }

    private void Update()
    {
        if (chickenStatus == null || ammoText == null)
            return;

        int ammo = chickenStatus.EggAmmo;

        ammoText.text = ammo.ToString();
        ammoText.color = ammo >= chickenStatus.MaxEggAmmo
            ? maxAmmoColor
            : normalColor;
    }
}
