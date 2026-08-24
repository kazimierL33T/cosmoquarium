using UnityEngine;
using TMPro;

// Attach to a TextMeshPro UI text object. Displays the player's current
// SPENDABLE gold (used in the shop) - separate from points, which track
// progress toward the night's win requirement and never decrease.
public class GoldDisplay : MonoBehaviour
{
    public string prefix = "Gold: ";

    protected TextMeshProUGUI goldText;

    void Awake()
    {
        goldText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (goldText != null)
        {
            goldText.text = prefix + GameManager.totalGold;
        }
    }
}