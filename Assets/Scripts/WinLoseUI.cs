using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to a GameObject under your Canvas. Shows either the win button
// (leads to the fishing/shop scene) or a game over panel with a menu button,
// depending on whether the player met the night's gold requirement.
public class WinLoseUI : MonoBehaviour
{
    public GameObject winPanel;
    public GameObject gameOverPanel;
    public string fishingSceneName = "Shop";
    public string mainMenuSceneName = "MainMenu"; // set this to your actual main menu scene's name

    void Awake()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    public virtual void ShowWin()
    {
        if (winPanel != null) winPanel.SetActive(true);
    }

    public virtual void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // Wire this up to the win button's OnClick() in the Inspector
    public virtual void GoToFishingScene()
    {
        SceneManager.LoadScene(fishingSceneName);
    }

    // Wire this up to the game over screen's menu button OnClick() in the Inspector
    public virtual void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}