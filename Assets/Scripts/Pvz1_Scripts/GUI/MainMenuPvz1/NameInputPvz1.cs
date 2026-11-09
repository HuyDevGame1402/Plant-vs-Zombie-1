using UnityEngine;
using TMPro;

public class NameInputPvz1 : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInputTMP;
    [SerializeField] private TextMeshProUGUI nameText;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            nameText.text = nameInputTMP.text;
            gameObject.SetActive(false);
        }
    }
}
