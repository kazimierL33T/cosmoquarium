using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Small, dependency-light main menu controller intended as a student-project starter.
/// Generated UI uses Unity uGUI so the project does not depend on third-party assets.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Controls")]
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Dropdown displayModeDropdown;

    private readonly List<Vector2Int> availableResolutions = new();

    private const string HasSavedSettingsKey = "menu.hasSavedSettings";
    private const string WidthKey = "menu.resolution.width";
    private const string HeightKey = "menu.resolution.height";
    private const string DisplayModeKey = "menu.displayMode";

    private void Start()
    {
        BuildResolutionOptions();
        BuildDisplayModeOptions();

        ShowMainPanel();

        if (PlayerPrefs.GetInt(HasSavedSettingsKey, 0) == 1)
        {
            ApplySavedSettings();
        }

        SyncDropdownsToCurrentScreen();
    }

    public void StartGame()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("MainMenuController: Game Scene Name has not been configured.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError(
                $"MainMenuController: Scene '{gameSceneName}' cannot be loaded. " +
                "Add it to the active Build Profile / Scene List, or change Game Scene Name in the Inspector."
            );
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        ShowMainPanel();
    }

    public void OnResolutionSelected(int index)
{
    if (resolutionDropdown == null || availableResolutions.Count == 0)
    {
        return;
    }

    int clampedIndex = Mathf.Clamp(index, 0, availableResolutions.Count - 1);

    if (resolutionDropdown.value != clampedIndex)
    {
        resolutionDropdown.SetValueWithoutNotify(clampedIndex);
    }

    resolutionDropdown.RefreshShownValue();
}

public void OnDisplayModeSelected(int index)
{
    if (displayModeDropdown == null)
    {
        return;
    }

    int clampedIndex = Mathf.Clamp(index, 0, 2);

    if (displayModeDropdown.value != clampedIndex)
    {
        displayModeDropdown.SetValueWithoutNotify(clampedIndex);
    }

    displayModeDropdown.RefreshShownValue();
}

public void ApplyDisplaySettings()
{
    ApplySettings();
}

    public void ApplySettings()
    {
        if (availableResolutions.Count == 0)
        {
            Debug.LogWarning("MainMenuController: No resolution options are available.");
            return;
        }

        int resolutionIndex = Mathf.Clamp(
            resolutionDropdown != null ? resolutionDropdown.value : 0,
            0,
            availableResolutions.Count - 1
        );

        Vector2Int resolution = availableResolutions[resolutionIndex];
        FullScreenMode mode = DropdownIndexToMode(
            displayModeDropdown != null ? displayModeDropdown.value : 0
        );

        Screen.SetResolution(resolution.x, resolution.y, mode);

        PlayerPrefs.SetInt(WidthKey, resolution.x);
        PlayerPrefs.SetInt(HeightKey, resolution.y);
        PlayerPrefs.SetInt(DisplayModeKey, (int)mode);
        PlayerPrefs.SetInt(HasSavedSettingsKey, 1);
        PlayerPrefs.Save();

        Debug.Log($"Applied display settings: {resolution.x}x{resolution.y}, {mode}");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowMainPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    private void BuildResolutionOptions()
    {
        availableResolutions.Clear();

        if (resolutionDropdown == null)
        {
            return;
        }

        resolutionDropdown.ClearOptions();

        Resolution[] unityResolutions = Screen.resolutions;
        HashSet<string> seen = new();
        List<string> labels = new();

        // Screen.resolutions often contains the same width/height at multiple refresh rates.
        // This starter intentionally exposes resolution only, so duplicate dimensions are removed.
        foreach (Resolution resolution in unityResolutions)
        {
            string key = $"{resolution.width}x{resolution.height}";
            if (!seen.Add(key))
            {
                continue;
            }

            availableResolutions.Add(new Vector2Int(resolution.width, resolution.height));
            labels.Add($"{resolution.width} x {resolution.height}");
        }

        // Defensive fallback for unusual editor/platform configurations.
        if (availableResolutions.Count == 0)
        {
            availableResolutions.Add(new Vector2Int(Screen.width, Screen.height));
            labels.Add($"{Screen.width} x {Screen.height}");
        }

        resolutionDropdown.AddOptions(labels);
    }

    private void BuildDisplayModeOptions()
    {
        if (displayModeDropdown == null)
        {
            return;
        }

        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(new List<string>
        {
            "Borderless Fullscreen",
            "Exclusive Fullscreen",
            "Windowed"
        });
    }

    private void ApplySavedSettings()
    {
        int width = PlayerPrefs.GetInt(WidthKey, Screen.width);
        int height = PlayerPrefs.GetInt(HeightKey, Screen.height);
        FullScreenMode mode = (FullScreenMode)PlayerPrefs.GetInt(
            DisplayModeKey,
            (int)Screen.fullScreenMode
        );

        Screen.SetResolution(width, height, mode);
    }

    private void SyncDropdownsToCurrentScreen()
    {
        if (resolutionDropdown != null && availableResolutions.Count > 0)
        {
            int closestIndex = 0;
            int closestDistance = int.MaxValue;

            for (int i = 0; i < availableResolutions.Count; i++)
            {
                int distance =
                    Mathf.Abs(availableResolutions[i].x - Screen.width) +
                    Mathf.Abs(availableResolutions[i].y - Screen.height);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            resolutionDropdown.SetValueWithoutNotify(closestIndex);
            resolutionDropdown.RefreshShownValue();
        }

        if (displayModeDropdown != null)
        {
            displayModeDropdown.SetValueWithoutNotify(ModeToDropdownIndex(Screen.fullScreenMode));
            displayModeDropdown.RefreshShownValue();
        }
    }

    private static FullScreenMode DropdownIndexToMode(int index)
    {
        return index switch
        {
            1 => FullScreenMode.ExclusiveFullScreen,
            2 => FullScreenMode.Windowed,
            _ => FullScreenMode.FullScreenWindow
        };
    }

    private static int ModeToDropdownIndex(FullScreenMode mode)
    {
        return mode switch
        {
            FullScreenMode.ExclusiveFullScreen => 1,
            FullScreenMode.Windowed => 2,
            _ => 0
        };
    }
}
