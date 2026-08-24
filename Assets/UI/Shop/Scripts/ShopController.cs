using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShopController : MonoBehaviour
{
    private const int ExpectedProductionProductCount = 6;
    private const string UpgradeTemplateId = "upgrade_template";

    [Header("Shop Content")]
    [SerializeField] private ShopItemView itemPrefab;
    [SerializeField] private Transform itemContainer;

    [Header("Shop UI")]
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button backButton;
    [SerializeField] private Button continueButton;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string aquariumSceneName = "Aquarium";

    private UpgradeDatabase upgradeDatabase;

    private readonly Dictionary<string, ShopItemView> itemViews =
        new Dictionary<string, ShopItemView>();

    private void Awake()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(Back);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(Continue);
        }
    }

    private void Start()
    {
        InitializeShop();
    }

    private void OnDestroy()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(Back);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Continue);
        }
    }

    public void Back()
    {
        LoadSceneIfAvailable(mainMenuSceneName);
    }

    public void Continue()
    {
        LoadSceneIfAvailable(aquariumSceneName);
    }

    private void InitializeShop()
    {
        RefreshGoldDisplay();
        SetFeedback(string.Empty);

        upgradeDatabase = UpgradeDatabase.Instance;

        if (upgradeDatabase == null)
        {
            upgradeDatabase = FindFirstObjectByType<UpgradeDatabase>();
        }

        if (upgradeDatabase == null)
        {
            Debug.LogError(
                "ShopController: No UpgradeDatabase exists in the Shop scene."
            );

            SetFeedback("Shop data unavailable");
            return;
        }

        BuildProductViews();
    }

    private void BuildProductViews()
    {
        if (itemPrefab == null)
        {
            Debug.LogError(
                "ShopController: Item Prefab has not been assigned."
            );

            SetFeedback("Shop UI unavailable");
            return;
        }

        if (itemContainer == null)
        {
            Debug.LogError(
                "ShopController: Item Container has not been assigned."
            );

            SetFeedback("Shop UI unavailable");
            return;
        }

        IReadOnlyList<UpgradeDatabase.UpgradeEntry> entries =
            upgradeDatabase.Entries;

        if (entries == null)
        {
            Debug.LogError(
                "ShopController: UpgradeDatabase entries are unavailable."
            );

            SetFeedback("Shop data unavailable");
            return;
        }

        int visibleProductCount = 0;

        foreach (UpgradeDatabase.UpgradeEntry entry in entries)
        {
            if (entry == null || !entry.showInShop)
            {
                continue;
            }

            if (string.Equals(
                    entry.upgradeId,
                    UpgradeTemplateId,
                    StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    "ShopController: 'upgrade_template' is marked " +
                    "showInShop and was excluded."
                );

                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.upgradeId))
            {
                Debug.LogWarning(
                    "ShopController: A showInShop entry has an empty " +
                    "upgradeId and was excluded."
                );

                continue;
            }

            if (itemViews.ContainsKey(entry.upgradeId))
            {
                Debug.LogWarning(
                    $"ShopController: Duplicate Shop upgradeId " +
                    $"'{entry.upgradeId}' was excluded."
                );

                continue;
            }

            ShopItemView view =
                Instantiate(itemPrefab, itemContainer);

            view.Bind(
                entry,
                GetOwnedCount(entry.upgradeId)
            );

            view.PurchaseRequested += HandlePurchaseRequested;

            itemViews.Add(entry.upgradeId, view);

            visibleProductCount++;
        }

        if (visibleProductCount != ExpectedProductionProductCount)
        {
            Debug.LogWarning(
                $"ShopController: Expected " +
                $"{ExpectedProductionProductCount} production Shop " +
                $"products, but generated {visibleProductCount}."
            );
        }
    }

    private void HandlePurchaseRequested(
        UpgradeDatabase.UpgradeEntry entry)
    {
        if (!IsValidPurchaseEntry(entry))
        {
            SetFeedback("Purchase unavailable");
            RefreshGoldDisplay();
            return;
        }

        if (!GameManager.TrySpendGold(entry.cost))
        {
            SetFeedback("Not enough gold");
            RefreshGoldDisplay();
            return;
        }

        GameManager.AddUpgrade(entry.upgradeId);

        RefreshGoldDisplay();
        RefreshOwnedCount(entry.upgradeId);

        string productName =
            string.IsNullOrWhiteSpace(entry.displayName)
                ? entry.upgradeId
                : entry.displayName;

        SetFeedback($"Purchased {productName}");
    }

    private static bool IsValidPurchaseEntry(
        UpgradeDatabase.UpgradeEntry entry)
    {
        return entry != null &&
               !string.IsNullOrWhiteSpace(entry.upgradeId) &&
               !string.Equals(
                   entry.upgradeId,
                   UpgradeTemplateId,
                   StringComparison.Ordinal) &&
               entry.showInShop &&
               entry.cost > 0;
    }

    private void RefreshGoldDisplay()
    {
        if (goldText != null)
        {
            goldText.text = $"Gold: {GameManager.totalGold}";
        }
    }

    private void RefreshOwnedCount(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            return;
        }

        if (itemViews.TryGetValue(
                upgradeId,
                out ShopItemView view) &&
            view != null)
        {
            view.UpdateOwnedCount(
                GetOwnedCount(upgradeId)
            );
        }
    }

    private static int GetOwnedCount(string upgradeId)
    {
        if (!string.IsNullOrWhiteSpace(upgradeId) &&
            GameManager.ownedUpgrades.TryGetValue(
                upgradeId,
                out int count))
        {
            return Mathf.Max(0, count);
        }

        return 0;
    }

    private void LoadSceneIfAvailable(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                "ShopController: Destination scene name has not been configured."
            );

            SetFeedback("Destination unavailable");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"ShopController: Scene '{sceneName}' cannot be loaded. " +
                "Add it to the active Build Profile / Scene List, " +
                "or change the scene name in the Inspector."
            );

            SetFeedback("Destination unavailable");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
        }
    }
}