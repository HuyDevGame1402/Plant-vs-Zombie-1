using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SunMoneyManager : MonoBehaviour
{
    public static SunMoneyManager Instance { get; private set; }

    [SerializeField] private int sunMoney = 0;

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

    public bool CheckEnoughSunMoney(int cost)
    {
        return sunMoney >= cost;
    }
}
