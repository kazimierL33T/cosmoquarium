using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Connects the Fishing scene's Canvas UI elements to the underlying game logic.
/// Subscribes to events from FishingManager and FishingMinigameController rather than
/// those scripts needing to know anything about UI directly.
public class FishingUIController : MonoBehaviour
{
    [Header("Core References")]
    public FishingManager fishingManager;
    public FishingMinigameController minigameController;

    [Header("Cast Counter")]
    public TMP_Text castCounterText;

    [Header("Currency")]
    public TMP_Text sandDollarText;
    public SandDollarWallet sandDollarWallet;

    [Header("Shop")]
    public GameObject shopPanel;
    public Button startFishingButton;

    public Button baitQualityButton;
    public FishingUpgradeData baitQualityUpgrade;

    public Button castRangeButton;
    public FishingUpgradeData castRangeUpgrade;

    public Button lineStrengthButton;
    public FishingUpgradeData lineStrengthUpgrade;

    [Header("Minigame")]
    public Slider minigameBar;
    public RectTransform hitZoneImage;

    [Header("Catch Result")]
    public TMP_Text catchResultText;
    public Image caughtFishImage;

    private void OnEnable()
    {
        if (fishingManager != null)
        {
            fishingManager.OnCastsRemainingChanged += UpdateCastCounter;
            fishingManager.OnFishCaught += ShowCatchResult;
            fishingManager.OnShopEntered += ShowShop;
        }

        if (minigameController != null)
        {
            minigameController.OnPositionUpdated += UpdateMinigameBar;
        }

        if (sandDollarWallet != null)
        {
            sandDollarWallet.OnBalanceChanged += UpdateSandDollarText;
        }
    }

    private void OnDisable()
    {
        // Always unsubscribe to avoid errors/leaks if this object is disabled or destroyed
        // while the events it's listening to still exist.
        if (fishingManager != null)
        {
            fishingManager.OnCastsRemainingChanged -= UpdateCastCounter;
            fishingManager.OnFishCaught -= ShowCatchResult;
            fishingManager.OnShopEntered -= ShowShop;
        }

        if (minigameController != null)
        {
            minigameController.OnPositionUpdated -= UpdateMinigameBar;
        }

        if (sandDollarWallet != null)
        {
            sandDollarWallet.OnBalanceChanged -= UpdateSandDollarText;
        }
    }

    private void Start()
    {
        catchResultText.gameObject.SetActive(false);
        caughtFishImage.gameObject.SetActive(false);

        startFishingButton.onClick.AddListener(OnStartFishingClicked);
        baitQualityButton.onClick.AddListener(() => OnUpgradeButtonClicked(baitQualityUpgrade));
        castRangeButton.onClick.AddListener(() => OnUpgradeButtonClicked(castRangeUpgrade));
        lineStrengthButton.onClick.AddListener(() => OnUpgradeButtonClicked(lineStrengthUpgrade));
    }

    private void ShowShop()
    {
        shopPanel.SetActive(true);
        RefreshShopButtons();
        UpdateHitZoneVisual();
    }

    /// Sizes and positions the green hit zone image based on the minigame's current
    /// hit zone width (which already factors in the Line Strength upgrade level).
    /// Called once when the shop opens, since that's guaranteed to happen after
    /// upgrades have been applied for this scene load.
    private void UpdateHitZoneVisual()
    {
        if (hitZoneImage == null || minigameController == null) return;

        float halfWidth = minigameController.GetCurrentHitZoneWidth() / 2f;
        float center = minigameController.hitZoneCenter;

        float min = Mathf.Clamp01(center - halfWidth);
        float max = Mathf.Clamp01(center + halfWidth);

        // Only touch the X anchors here - leave Y anchors/offsets exactly as configured
        // in the Editor (e.g. copied from Background), so height stays untouched.
        Vector2 anchorMin = hitZoneImage.anchorMin;
        Vector2 anchorMax = hitZoneImage.anchorMax;
        hitZoneImage.anchorMin = new Vector2(min, anchorMin.y);
        hitZoneImage.anchorMax = new Vector2(max, anchorMax.y);
    }

    private void OnStartFishingClicked()
    {
        shopPanel.SetActive(false);
        fishingManager.ConfirmShopAndStartFishing();
    }

    private void OnUpgradeButtonClicked(FishingUpgradeData upgrade)
    {
        if (FishingUpgradeManager.Instance == null || upgrade == null) return;

        bool purchased = FishingUpgradeManager.Instance.TryPurchase(upgrade);

        if (purchased)
        {
            fishingManager.ApplyPurchasedUpgrades();
            UpdateHitZoneVisual();
        }

        RefreshShopButtons();
    }

    private void RefreshShopButtons()
    {
        RefreshSingleButton(baitQualityButton, baitQualityUpgrade);
        RefreshSingleButton(castRangeButton, castRangeUpgrade);
        RefreshSingleButton(lineStrengthButton, lineStrengthUpgrade);
    }

    private void RefreshSingleButton(Button button, FishingUpgradeData upgrade)
    {
        if (button == null || upgrade == null || FishingUpgradeManager.Instance == null) return;

        int level = FishingUpgradeManager.Instance.GetLevel(upgrade);
        int cost = FishingUpgradeManager.Instance.GetNextCost(upgrade);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = $"{upgrade.upgradeName}\nLv. {level} - {cost} SD";
        }

        // Disable the button if the player can't afford it, so it's visually clear.
        bool canAfford = SandDollarWallet.Instance != null && SandDollarWallet.Instance.CurrentBalance >= cost;
        button.interactable = canAfford;
    }

    private void UpdateCastCounter(int remaining)
    {
        castCounterText.text = $"Casts: {remaining}";
    }

    private void UpdateSandDollarText(int balance)
    {
        sandDollarText.text = $"Sand Dollars: {balance}";
    }

    private void UpdateMinigameBar(float position)
    {
        minigameBar.value = position;
    }

    private void ShowCatchResult(FishData fish)
    {
        if (fish == null)
        {
            catchResultText.text = "No catch this time...";
            caughtFishImage.gameObject.SetActive(false);
        }
        else
        {
            catchResultText.text = $"You caught a {fish.speciesName}!";

            if (fish.sprite != null)
            {
                caughtFishImage.sprite = fish.sprite;
                caughtFishImage.gameObject.SetActive(true);
            }
            else
            {
                // No sprite assigned on this FishData yet - skip showing the image
                // rather than showing a blank/broken one.
                caughtFishImage.gameObject.SetActive(false);
            }
        }

        catchResultText.gameObject.SetActive(true);
        Invoke(nameof(HideCatchResult), fishingManager.catchResultDisplayDuration - 0.2f);
    }

    private void HideCatchResult()
    {
        catchResultText.gameObject.SetActive(false);
        caughtFishImage.gameObject.SetActive(false);
    }
}
