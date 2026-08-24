using UnityEngine;
using System.Collections.Generic;

// Lives once in each scene that needs upgrade data (Aquarium, Shop).
// Maps each upgradeId to its prefab and icon, since GameManager (static)
// can't hold serialized Inspector references directly.
public class UpgradeDatabase : MonoBehaviour
{
    [System.Serializable]
    public class UpgradeEntry
    {
        public string upgradeId;
        public GameObject prefab;
        public Sprite icon;

        public string displayName;

        [TextArea]
        public string description;

        [Min(0)]
        public int cost;

        public bool showInShop;
    }

    public List<UpgradeEntry> entries;

    public IReadOnlyList<UpgradeEntry> Entries => entries;

    public static UpgradeDatabase Instance;

    void Awake()
    {
        Instance = this;
    }

    public UpgradeEntry GetEntry(string upgradeId)
    {
        return entries.Find(e => e.upgradeId == upgradeId);
    }
}