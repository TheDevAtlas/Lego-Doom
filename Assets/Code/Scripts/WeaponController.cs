using UnityEngine;
using System.Collections.Generic;

public class WeaponController : MonoBehaviour
{
    [Header("Weapons (4 Guns + 1 Chainsaw)")]
    [SerializeField] private List<Weapon> weapons = new List<Weapon>();
    [SerializeField] private int currentWeaponIndex = 0;

    [System.Serializable]
    public class Weapon
    {
        public string name;
        public GameObject weaponObject;
        [Header("Ammo")]
        public int currentAmmo;
        public int maxAmmo;
    }

    private Weapon currentActiveWeapon;
    private bool chainsawActive = false;

    void Start()
    {
        InitializeWeapons();
        SelectWeapon(currentWeaponIndex);
    }

    void Update()
    {
        HandleWeaponScrolling();
        HandleNumberKeySelection();
        HandleChainsawToggle();
    }

    void InitializeWeapons()
    {
        foreach (Weapon weapon in weapons)
        {
            if (weapon.weaponObject != null)
                weapon.weaponObject.SetActive(false);
        }
    }

    void HandleWeaponScrolling()
    {
        if (chainsawActive) return;

        float scroll = -Input.GetAxis("Mouse ScrollWheel");

        if (scroll > 0f)
        {
            NextWeapon();
        }
        else if (scroll < 0f)
        {
            PreviousWeapon();
        }
    }

    void HandleNumberKeySelection()
    {
        if (chainsawActive) return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectWeapon(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectWeapon(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectWeapon(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SelectWeapon(3);
        }
    }

    void HandleChainsawToggle()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (chainsawActive)
            {
                DeactivateChainsaw();
            }
            else
            {
                ActivateChainsaw();
            }
        }
    }

    void NextWeapon()
    {
        currentWeaponIndex = (currentWeaponIndex + 1) % 4;
        SelectWeapon(currentWeaponIndex);
    }

    void PreviousWeapon()
    {
        currentWeaponIndex = (currentWeaponIndex - 1 + 4) % 4;
        SelectWeapon(currentWeaponIndex);
    }

    void SelectWeapon(int index)
    {
        if (currentActiveWeapon != null)
            currentActiveWeapon.weaponObject.SetActive(false);

        currentWeaponIndex = index;
        currentActiveWeapon = weapons[index];
        currentActiveWeapon.weaponObject.SetActive(true);
    }

    void ActivateChainsaw()
    {
        if (currentActiveWeapon != null)
            currentActiveWeapon.weaponObject.SetActive(false);

        weapons[4].weaponObject.SetActive(true);
        chainsawActive = true;
    }

    void DeactivateChainsaw()
    {
        weapons[4].weaponObject.SetActive(false);
        chainsawActive = false;

        SelectWeapon(currentWeaponIndex);
    }

    public void AddAmmo(int weaponIndex, int amount)
    {
        if (IsValidIndex(weaponIndex))
        {
            weapons[weaponIndex].currentAmmo = Mathf.Min(
                weapons[weaponIndex].currentAmmo + amount,
                weapons[weaponIndex].maxAmmo
            );
        }
    }

    public bool RemoveAmmo(int weaponIndex, int amount)
    {
        if (IsValidIndex(weaponIndex))
        {
            var weapon = weapons[weaponIndex];
            if (weapon.currentAmmo >= amount)
            {
                weapon.currentAmmo -= amount;
                return true;
            }
        }
        return false;
    }

    public bool HasAmmo(int weaponIndex)
    {
        if (IsValidIndex(weaponIndex))
        {
            return weapons[weaponIndex].currentAmmo > 0;
        }
        return false;
    }

    public void UseAmmo(int weaponIndex, int amount)
    {
        RemoveAmmo(weaponIndex, amount);
    }

    bool IsValidIndex(int index)
    {
        return index >= 0 && index < weapons.Count;
    }

    public Weapon GetCurrentWeapon()
    {
        return chainsawActive ? weapons[4] : currentActiveWeapon;
    }

    public bool IsChainsawActive()
    {
        return chainsawActive;
    }
}