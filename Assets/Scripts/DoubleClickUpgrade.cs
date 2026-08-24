using UnityEngine;

// While the fish that eats this stays alive, ALL predator clicks anywhere deal
// double damage instances. The buff expires automatically the moment this fish dies.
// This upgrade should NOT have Attract Predators checked - only regular fish can eat it.
public class DoubleClickUpgrade : Upgrade
{
    protected override void ApplyUpgradeEffect(Fish eater)
    {
        base.ApplyUpgradeEffect(eater); // still applies the fishColorOnConsume tint

        eater.ActivateDoubleClickSource();
        Debug.Log($"[DoubleClickUpgrade] {eater.gameObject.name} is now a Double Click source - all predator clicks deal 2x while it's alive.");
    }
}