using TMPro;
using UnityEngine;

public class MaxPlayerInputHandler : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;

    private int currentValue = 10;

    private void Awake()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }

        currentValue = 10;
        inputField.text = currentValue.ToString();
    }

    public void ValidateInput()
    {
        string input = inputField.text;

        // 숫자만 추출
        string numbersOnly = "";

        foreach (char character in input)
        {
            if (char.IsDigit(character))
            {
                numbersOnly += character;
            }
        }

        // 숫자가 하나도 없으면 최저값
        if (numbersOnly.Length == 0)
        {
            currentValue = 2;
            inputField.text = "2";
            return;
        }

        // 숫자를 정수로 변환
        if (!int.TryParse(numbersOnly, out int value))
        {
            currentValue = 2;
            inputField.text = "2";
            return;
        }

        // 2 ~ 20으로 제한
        value = Mathf.Clamp(value, 2, 20);

        currentValue = value;
        inputField.text = currentValue.ToString();
    }

    public int GetValue()
    {
        return currentValue;
    }
}