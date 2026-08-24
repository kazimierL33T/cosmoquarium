using UnityEngine;

public class DebugGoldGrant : MonoBehaviour
{
    [SerializeField, Min(0)]
    private int startingGold = 100;

    private void Start()
    {
#if UNITY_EDITOR
        GameManager.AddGold(startingGold);
#endif
    }
}