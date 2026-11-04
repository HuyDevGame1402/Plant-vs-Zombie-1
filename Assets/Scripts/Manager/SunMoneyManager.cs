using System;
using UnityEngine;

public class SunMoneyManager : MonoBehaviour
{
    public static SunMoneyManager Instance { get; private set; }

    [SerializeField] private int sunMoney = 0;

    public event Action<int> OnSunMoneyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void SetupSunMoneyInitGame(int initSunMoney)
    {
        sunMoney = initSunMoney;
        OnSunMoneyChanged?.Invoke(sunMoney);
    }

    public bool CheckEnoughSunMoney(int cost)
    {
        return sunMoney >= cost;
    }

    public void AddSunMoney(int amount)
    {
        sunMoney += amount;
        OnSunMoneyChanged?.Invoke(sunMoney);
    }

    public void SpendSunMoney(int amount)
    {
        if (CheckEnoughSunMoney(amount))
        {
            sunMoney -= amount;
            OnSunMoneyChanged?.Invoke(sunMoney);
        }
    }
}
