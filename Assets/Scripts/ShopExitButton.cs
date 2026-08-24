using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to a "Close Shop" / "Back to Aquarium" button inside the Shop scene.
// Unloads the Shop scene (Aquarium was never unloaded, so it resumes exactly
// where it left off) and restores normal time flow.
public class ShopExitButton : MonoBehaviour
{
    public string shopSceneName = "Shop";

    // Wire this to the button's OnClick() in the Inspector
    public virtual void CloseShop()
    {
        Time.timeScale = 1f; // resume Aquarium gameplay
        SceneManager.UnloadSceneAsync(shopSceneName);
    }
}