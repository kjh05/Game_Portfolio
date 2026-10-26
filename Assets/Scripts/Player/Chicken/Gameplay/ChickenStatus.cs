using UnityEngine;

public class ChickenStatus : MonoBehaviour
{
    [SerializeField] private int eggAmmo = 0;
    [SerializeField] private int maxEggAmmo = 5;

    public int EggAmmo => eggAmmo;
    public int MaxEggAmmo => maxEggAmmo;
    
    public void AddEgg(int amount)
    {
        eggAmmo = Mathf.Clamp(eggAmmo + amount, 0, maxEggAmmo);
    }
}
