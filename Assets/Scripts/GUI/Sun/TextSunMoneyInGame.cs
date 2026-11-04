using UnityEngine;
using TMPro;

public class TextSunMoneyInGame : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI sunMoneyText;

    private void Awake()
    {
        sunMoneyText = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        if(SunMoneyManager.Instance != null)
        {
            SunMoneyManager.Instance.OnSunMoneyChanged += UpdateSunMoneyText;
        }
    }

    private void UpdateSunMoneyText(int sunMoneyCurrent)
    {
        sunMoneyText.text = sunMoneyCurrent.ToString();
    }
}
