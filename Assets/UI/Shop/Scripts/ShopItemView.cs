using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemView : MonoBehaviour
{
    [Header("Product UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text ownedCountText;
    [SerializeField] private Button purchaseButton;

    private UpgradeDatabase.UpgradeEntry boundEntry;

    public event Action<UpgradeDatabase.UpgradeEntry> PurchaseRequested;

    private void Awake()
    {
        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(HandlePurchaseClicked);
        }
    }

    private void OnDestroy()
    {
        if (purchaseButton != null)
        {
            purchaseButton.onClick.RemoveListener(HandlePurchaseClicked);
        }
    }

    public void Bind(UpgradeDatabase.UpgradeEntry entry, int ownedCount)
    {
        boundEntry = entry;

        if (entry == null)
        {
            ClearView();
            return;
        }

        if (icon != null)
        {
            icon.sprite = entry.icon;
            icon.enabled = entry.icon != null;
        }

        if (displayNameText != null)
        {
            displayNameText.text = string.IsNullOrWhiteSpace(entry.displayName)
                ? entry.upgradeId
                : entry.displayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = entry.description ?? string.Empty;
        }

        if (costText != null)
        {
            costText.text = $"Cost: {entry.cost}";
        }

        UpdateOwnedCount(ownedCount);

        if (purchaseButton != null)
        {
            purchaseButton.interactable =
                !string.IsNullOrWhiteSpace(entry.upgradeId) &&
                entry.showInShop &&
                entry.cost > 0;
        }
    }

    public void UpdateOwnedCount(int ownedCount)
    {
        if (ownedCountText != null)
        {
            ownedCountText.text = $"Owned: {Mathf.Max(0, ownedCount)}";
        }
    }

    private void HandlePurchaseClicked()
    {
        if (boundEntry != null)
        {
            PurchaseRequested?.Invoke(boundEntry);
        }
    }

    private void ClearView()
    {
        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (displayNameText != null)
        {
            displayNameText.text = string.Empty;
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.Empty;
        }

        if (costText != null)
        {
            costText.text = string.Empty;
        }

        UpdateOwnedCount(0);

        if (purchaseButton != null)
        {
            purchaseButton.interactable = false;
        }
    }
}