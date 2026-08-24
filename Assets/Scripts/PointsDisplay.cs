using UnityEngine;
using TMPro;

// Attach to a TextMeshPro UI text object. Displays the player's current points
// against this night's required points, e.g. "Points: 120 / 500". Unlike gold,
// points never decrease, even if the player spends gold in the shop.
public class PointsDisplay : MonoBehaviour
{
    public string prefix = "Points: ";

    protected TextMeshProUGUI pointsText;

    void Awake()
    {
        pointsText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (pointsText == null) return;

        if (NightManager.Instance != null)
        {
            int required = NightManager.Instance.GetGoldRequirement();
            pointsText.text = $"{prefix}{GameManager.totalPoints} / {required}";
        }
        else
        {
            pointsText.text = $"{prefix}{GameManager.totalPoints}";
        }
    }
}