using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameModeSelector : MonoBehaviour
{
    [Header("Game Mode")]
    [SerializeField] private GameMode currentGameMode = GameMode.Classic;

    [Header("UI")]
    [SerializeField] private TMP_Text gameModeText;
    [SerializeField] private Image gameModeImage;

    [Header("Game Mode Images")]
    [SerializeField] private Sprite classicImage;
    [SerializeField] private Sprite zombieImage;
    [SerializeField] private Sprite passTheTorchImage;

    public GameMode CurrentGameMode => currentGameMode;

    private void Start()
    {
        UpdateUI();
    }

    public void NextGameMode()
    {
        currentGameMode++;

        if (currentGameMode > GameMode.PassTheTorch)
        {
            currentGameMode = GameMode.Classic;
        }

        UpdateUI();
    }

    public void PreviousGameMode()
    {
        currentGameMode--;

        if (currentGameMode < GameMode.Classic)
        {
            currentGameMode = GameMode.PassTheTorch;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        switch (currentGameMode)
        {
            case GameMode.Classic:
                gameModeText.text = "Classic";
                gameModeImage.sprite = classicImage;
                break;

            case GameMode.Zombie:
                gameModeText.text = "Zombie";
                gameModeImage.sprite = zombieImage;
                break;

            case GameMode.PassTheTorch:
                gameModeText.text = "Pass The Torch";
                gameModeImage.sprite = passTheTorchImage;
                break;
        }
    }

    public GameMode GetValue()
    {
        return currentGameMode;
    }
}