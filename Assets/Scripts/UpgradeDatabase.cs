using UnityEngine;
using System.Collections.Generic;

// Singleton that survives across scene loads. Only ONE instance is ever active -
// if a second UpgradeDatabase loads (e.g. Shop's copy, loaded additively on top
// of Aquarium), it detects the existing Instance and destroys itself instead of
// overwriting it. This prevents Instance from ever pointing at a destroyed object
// when an additively-loaded scene (like Shop) is later unloaded.
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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public UpgradeEntry GetEntry(string upgradeId)
    {
        return entries.Find(e => e.upgradeId == upgradeId);
    }
}