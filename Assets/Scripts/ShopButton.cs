using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to a UI button in the Aquarium scene. Opens the Shop scene additively
// on top of Aquarium (Aquarium stays loaded, so GameManager's night-increment
// logic on scene unload never fires) and pauses gameplay via Time.timeScale
// while the Shop is open.
public class ShopButton : MonoBehaviour
{
    public string shopSceneName = "Shop";

    // Wire this to the button's OnClick() in the Inspector
    public virtual void OpenShop()
    {
        Time.timeScale = 0f; // freeze Aquarium gameplay while Shop is open
        SceneManager.LoadScene(shopSceneName, LoadSceneMode.Additive);
    }
}