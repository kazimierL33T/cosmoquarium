// Assets/UI/MainMenu/Scripts/Editor/MainMenuBuilder.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Cosmoquarium.UI.MainMenu;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cosmoquarium.UI.MainMenu.Editor
{
    /// <summary>
    /// Builds the generated uGUI main-menu prefab and scene without modifying gameplay assets.
    /// </summary>
    internal static class MainMenuBuilder
    {
        private const string RootFolder = "Assets/UI/MainMenu";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string PrefabPath = PrefabsFolder + "/MainMenuUI.prefab";
        private const string ScenePath = ScenesFolder + "/MainMenu.unity";
        private const string InputActionsPath =
            "Assets/Settings/InputSystem_Actions.inputactions";
        private const string UiActionMapName = "UI";

        private static readonly Vector2 ReferenceResolution =
            new(1920f, 1080f);

        [MenuItem("Tools/Cosmoquarium/Create Main Menu")]
        public static void CreateMainMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "Main Menu Builder cannot run while entering or running Play Mode.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                EnsureGeneratedFolders();
                ValidateGeneratedAssetPath<SceneAsset>(ScenePath);
                ValidateGeneratedAssetPath<GameObject>(PrefabPath);

                InputActionAsset inputActions =
                    LoadAndValidateInputActions();

                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

                GameObject root = new("MainMenu");

                DefaultControls.Resources resources =
                    CreateUiResources();

                MenuUi ui = BuildMenuUi(
                    root.transform,
                    resources);

                EventSystem eventSystem = BuildEventSystem(
                    root.transform,
                    ui.StartButton.gameObject,
                    inputActions);

                AssignControllerReferences(ui);
                WireUiEvents(ui);

                ui.MainPanel.SetActive(true);
                ui.SettingsPanel.SetActive(false);

                PrefabUtility.SaveAsPrefabAssetAndConnect(
                    ui.Canvas.gameObject,
                    PrefabPath,
                    InteractionMode.AutomatedAction,
                    out bool prefabSaved);

                if (!prefabSaved)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save the generated menu prefab at '{PrefabPath}'.");
                }

                EditorSceneManager.MarkSceneDirty(scene);

                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save the generated menu scene at '{ScenePath}'.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                SceneAsset sceneAsset =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

                Selection.activeObject = sceneAsset;
                EditorGUIUtility.PingObject(sceneAsset);

                Debug.Log(
                    $"Main menu generated successfully.\n"
                    + $"Scene: {ScenePath}\n"
                    + $"Prefab: {PrefabPath}\n"
                    + $"Input: {InputActionsPath} / {UiActionMapName}\n"
                    + $"Initial selection: {eventSystem.firstSelectedGameObject.name}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static MenuUi BuildMenuUi(
            Transform root,
            DefaultControls.Resources resources)
        {
            GameObject canvasObject = new(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvasObject.transform.SetParent(root, false);

            Canvas canvas =
                canvasObject.GetComponent<Canvas>();

            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                ReferenceResolution;

            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            scaler.matchWidthOrHeight = 0.5f;

            MainMenuController controller =
                canvasObject.AddComponent<MainMenuController>();

            GameObject background = CreateStretchObject(
                "Background",
                canvasObject.transform,
                typeof(Image));

            Image backgroundImage =
                background.GetComponent<Image>();

            backgroundImage.color =
                new Color(0.055f, 0.08f, 0.12f, 1f);

            backgroundImage.raycastTarget = false;

            GameObject mainPanel = CreateStretchObject(
                "MainPanel",
                background.transform);

            CreateText(
                mainPanel.transform,
                resources,
                "Title",
                "COSMOQUARIUM",
                48,
                new Vector2(0f, 250f),
                new Vector2(700f, 90f));

            Button startButton = CreateButton(
                mainPanel.transform,
                resources,
                "StartButton",
                "Start",
                new Vector2(0f, 80f));

            Button settingsButton = CreateButton(
                mainPanel.transform,
                resources,
                "SettingsButton",
                "Settings",
                new Vector2(0f, 0f));

            Button quitButton = CreateButton(
                mainPanel.transform,
                resources,
                "QuitButton",
                "Quit",
                new Vector2(0f, -80f));

            GameObject settingsPanel = CreateStretchObject(
                "SettingsPanel",
                background.transform);

            CreateText(
                settingsPanel.transform,
                resources,
                "SettingsTitle",
                "Settings",
                42,
                new Vector2(0f, 280f),
                new Vector2(700f, 80f));

            Dropdown resolutionDropdown = CreateDropdown(
                settingsPanel.transform,
                resources,
                "ResolutionDropdown",
                new Vector2(0f, 110f),
                "Resolution");

            Dropdown displayModeDropdown = CreateDropdown(
                settingsPanel.transform,
                resources,
                "DisplayModeDropdown",
                new Vector2(0f, 25f),
                "Display Mode");

            displayModeDropdown.ClearOptions();
            displayModeDropdown.AddOptions(
                new List<string>
                {
                    "Borderless Fullscreen",
                    "Exclusive Fullscreen",
                    "Windowed",
                });

            Button applyButton = CreateButton(
                settingsPanel.transform,
                resources,
                "ApplyButton",
                "Apply",
                new Vector2(0f, -100f));

            Button backButton = CreateButton(
                settingsPanel.transform,
                resources,
                "BackButton",
                "Back",
                new Vector2(0f, -180f));

            return new MenuUi(
                canvas,
                controller,
                mainPanel,
                settingsPanel,
                startButton,
                settingsButton,
                quitButton,
                resolutionDropdown,
                displayModeDropdown,
                applyButton,
                backButton);
        }

        private static EventSystem BuildEventSystem(
            Transform root,
            GameObject firstSelected,
            InputActionAsset inputActions)
        {
            GameObject eventSystemObject = new(
                "EventSystem",
                typeof(EventSystem));

            eventSystemObject.transform.SetParent(root, false);

            EventSystem eventSystem =
                eventSystemObject.GetComponent<EventSystem>();

            eventSystem.sendNavigationEvents = true;
            eventSystem.firstSelectedGameObject = firstSelected;

            InputSystemUIInputModule inputModule =
                eventSystemObject.AddComponent<InputSystemUIInputModule>();

            ConfigureInputSystemUiModule(
                inputModule,
                inputActions);

            return eventSystem;
        }

        private static void ConfigureInputSystemUiModule(
            InputSystemUIInputModule inputModule,
            InputActionAsset inputActions)
        {
            InputActionMap uiMap =
                inputActions.FindActionMap(
                    UiActionMapName,
                    throwIfNotFound: true);

            InputActionReference[] importedReferences =
                AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                    .OfType<InputActionReference>()
                    .ToArray();

            InputActionReference Resolve(string actionName)
            {
                InputAction action =
                    uiMap.FindAction(
                        actionName,
                        throwIfNotFound: true);

                InputActionReference reference =
                    importedReferences.FirstOrDefault(
                        candidate =>
                            candidate != null
                            && candidate.action != null
                            && candidate.action.id == action.id);

                if (reference == null)
                {
                    throw new InvalidOperationException(
                        "Could not resolve the imported "
                        + $"InputActionReference for '{UiActionMapName}/{actionName}' "
                        + $"in '{InputActionsPath}'. Reimport the Input Actions "
                        + "asset and run the builder again.");
                }

                return reference;
            }

            inputModule.UnassignActions();

            inputModule.actionsAsset =
                inputActions;

            inputModule.move =
                Resolve("Navigate");

            inputModule.submit =
                Resolve("Submit");

            inputModule.cancel =
                Resolve("Cancel");

            inputModule.point =
                Resolve("Point");

            inputModule.leftClick =
                Resolve("Click");

            inputModule.rightClick =
                Resolve("RightClick");

            inputModule.middleClick =
                Resolve("MiddleClick");

            inputModule.scrollWheel =
                Resolve("ScrollWheel");

            inputModule.trackedDevicePosition =
                Resolve("TrackedDevicePosition");

            inputModule.trackedDeviceOrientation =
                Resolve("TrackedDeviceOrientation");

            EditorUtility.SetDirty(inputModule);
        }

        private static void AssignControllerReferences(
            MenuUi ui)
        {
            SerializedObject serializedController =
                new(ui.Controller);

            serializedController.Update();

            AssignObjectReference(
                serializedController,
                "mainPanel",
                ui.MainPanel);

            AssignObjectReference(
                serializedController,
                "settingsPanel",
                ui.SettingsPanel);

            AssignObjectReference(
                serializedController,
                "resolutionDropdown",
                ui.ResolutionDropdown);

            AssignObjectReference(
                serializedController,
                "displayModeDropdown",
                ui.DisplayModeDropdown);

            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ui.Controller);
        }

        private static void WireUiEvents(
            MenuUi ui)
        {
            UnityEventTools.AddPersistentListener(
                ui.StartButton.onClick,
                ui.Controller.StartGame);

            UnityEventTools.AddPersistentListener(
                ui.SettingsButton.onClick,
                ui.Controller.OpenSettings);

            UnityEventTools.AddPersistentListener(
                ui.QuitButton.onClick,
                ui.Controller.QuitGame);

            UnityEventTools.AddPersistentListener(
                ui.ResolutionDropdown.onValueChanged,
                ui.Controller.OnResolutionSelected);

            UnityEventTools.AddPersistentListener(
                ui.DisplayModeDropdown.onValueChanged,
                ui.Controller.OnDisplayModeSelected);

            UnityEventTools.AddPersistentListener(
                ui.ApplyButton.onClick,
                ui.Controller.ApplyDisplaySettings);

            UnityEventTools.AddPersistentListener(
                ui.BackButton.onClick,
                ui.Controller.CloseSettings);

            EditorUtility.SetDirty(ui.StartButton);
            EditorUtility.SetDirty(ui.SettingsButton);
            EditorUtility.SetDirty(ui.QuitButton);
            EditorUtility.SetDirty(ui.ResolutionDropdown);
            EditorUtility.SetDirty(ui.DisplayModeDropdown);
            EditorUtility.SetDirty(ui.ApplyButton);
            EditorUtility.SetDirty(ui.BackButton);
        }

        private static Text CreateText(
            Transform parent,
            DefaultControls.Resources resources,
            string objectName,
            string value,
            int fontSize,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject =
                DefaultControls.CreateText(resources);

            textObject.name = objectName;
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform =
                textObject.GetComponent<RectTransform>();

            ConfigureCenteredRect(
                rectTransform,
                anchoredPosition,
                size);

            Text text =
                textObject.GetComponent<Text>();

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return text;
        }

        private static Button CreateButton(
            Transform parent,
            DefaultControls.Resources resources,
            string objectName,
            string label,
            Vector2 anchoredPosition)
        {
            GameObject buttonObject =
                DefaultControls.CreateButton(resources);

            buttonObject.name = objectName;
            buttonObject.transform.SetParent(parent, false);

            ConfigureCenteredRect(
                buttonObject.GetComponent<RectTransform>(),
                anchoredPosition,
                new Vector2(420f, 64f));

            Button button =
                buttonObject.GetComponent<Button>();

            Image image =
                buttonObject.GetComponent<Image>();

            image.color =
                new Color(0.16f, 0.23f, 0.32f, 1f);

            Text text =
                buttonObject.GetComponentInChildren<Text>(true);

            if (text != null)
            {
                text.text = label;
                text.fontSize = 24;
                text.color = Color.white;
            }

            return button;
        }

        private static Dropdown CreateDropdown(
            Transform parent,
            DefaultControls.Resources resources,
            string objectName,
            Vector2 anchoredPosition,
            string placeholder)
        {
            GameObject dropdownObject =
                DefaultControls.CreateDropdown(resources);

            dropdownObject.name = objectName;
            dropdownObject.transform.SetParent(parent, false);

            ConfigureCenteredRect(
                dropdownObject.GetComponent<RectTransform>(),
                anchoredPosition,
                new Vector2(520f, 64f));

            Dropdown dropdown =
                dropdownObject.GetComponent<Dropdown>();

            dropdown.ClearOptions();

            dropdown.AddOptions(
                new List<string>
                {
                    placeholder,
                });

            Image image =
                dropdownObject.GetComponent<Image>();

            image.color =
                new Color(0.16f, 0.23f, 0.32f, 1f);

            if (dropdown.captionText != null)
            {
                dropdown.captionText.fontSize = 22;
                dropdown.captionText.color = Color.white;
            }

            if (dropdown.itemText != null)
            {
                dropdown.itemText.fontSize = 20;
            }

            return dropdown;
        }

        private static GameObject CreateStretchObject(
            string objectName,
            Transform parent,
            params Type[] additionalComponents)
        {
            Type[] components =
                new Type[additionalComponents.Length + 1];

            components[0] = typeof(RectTransform);

            for (
                int index = 0;
                index < additionalComponents.Length;
                index++)
            {
                components[index + 1] =
                    additionalComponents[index];
            }

            GameObject gameObject =
                new(objectName, components);

            gameObject.transform.SetParent(parent, false);

            RectTransform rectTransform =
                gameObject.GetComponent<RectTransform>();

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            return gameObject;
        }

        private static void ConfigureCenteredRect(
            RectTransform rectTransform,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rectTransform.anchorMin =
                new Vector2(0.5f, 0.5f);

            rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);

            rectTransform.pivot =
                new Vector2(0.5f, 0.5f);

            rectTransform.anchoredPosition =
                anchoredPosition;

            rectTransform.sizeDelta = size;
            rectTransform.localScale = Vector3.one;
        }

        private static DefaultControls.Resources CreateUiResources()
        {
            return new DefaultControls.Resources
            {
                standard =
                    LoadBuiltinSprite(
                        "UI/Skin/UISprite.psd"),

                background =
                    LoadBuiltinSprite(
                        "UI/Skin/Background.psd"),

                inputField =
                    LoadBuiltinSprite(
                        "UI/Skin/InputFieldBackground.psd"),

                knob =
                    LoadBuiltinSprite(
                        "UI/Skin/Knob.psd"),

                checkmark =
                    LoadBuiltinSprite(
                        "UI/Skin/Checkmark.psd"),

                dropdown =
                    LoadBuiltinSprite(
                        "UI/Skin/DropdownArrow.psd"),

                mask =
                    LoadBuiltinSprite(
                        "UI/Skin/UIMask.psd"),
            };
        }

        private static Sprite LoadBuiltinSprite(
            string path)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        }

        private static InputActionAsset LoadAndValidateInputActions()
        {
            InputActionAsset inputActions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    InputActionsPath);

            if (inputActions == null)
            {
                throw new InvalidOperationException(
                    $"Required Input Actions asset was not found at '{InputActionsPath}'.");
            }

            InputActionMap uiMap =
                inputActions.FindActionMap(
                    UiActionMapName,
                    throwIfNotFound: false);

            if (uiMap == null)
            {
                throw new InvalidOperationException(
                    $"Input Actions asset '{InputActionsPath}' does not "
                    + $"contain the required '{UiActionMapName}' action map.");
            }

            string[] requiredActions =
            {
                "Navigate",
                "Submit",
                "Cancel",
                "Point",
                "Click",
                "RightClick",
                "MiddleClick",
                "ScrollWheel",
                "TrackedDevicePosition",
                "TrackedDeviceOrientation",
            };

            foreach (string actionName in requiredActions)
            {
                if (uiMap.FindAction(
                        actionName,
                        throwIfNotFound: false) == null)
                {
                    throw new InvalidOperationException(
                        $"Input Actions asset '{InputActionsPath}' "
                        + $"is missing '{UiActionMapName}/{actionName}'.");
                }
            }

            return inputActions;
        }

        private static void AssignObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);

            if (property == null)
            {
                throw new MissingFieldException(
                    serializedObject.targetObject
                        .GetType()
                        .FullName,
                    propertyName);
            }

            property.objectReferenceValue = value;
        }

        private static void EnsureGeneratedFolders()
        {
            EnsureFolder(
                "Assets",
                "UI");

            EnsureFolder(
                "Assets/UI",
                "MainMenu");

            EnsureFolder(
                RootFolder,
                "Prefabs");

            EnsureFolder(
                RootFolder,
                "Scenes");

            EnsureFolder(
                RootFolder,
                "Scripts");

            EnsureFolder(
                RootFolder + "/Scripts",
                "Editor");
        }

        private static void EnsureFolder(
            string parent,
            string child)
        {
            string fullPath =
                $"{parent}/{child}";

            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(
                    parent,
                    child);
            }
        }

        private static void ValidateGeneratedAssetPath<T>(
            string path)
            where T : UnityEngine.Object
        {
            string guid =
                AssetDatabase.AssetPathToGUID(path);

            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            UnityEngine.Object existingAsset =
                AssetDatabase.LoadMainAssetAtPath(path);

            if (existingAsset is T)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Refusing to overwrite '{path}' because an existing "
                + $"asset at that path is not a generated {typeof(T).Name}.");
        }

        private sealed class MenuUi
        {
            public MenuUi(
                Canvas canvas,
                MainMenuController controller,
                GameObject mainPanel,
                GameObject settingsPanel,
                Button startButton,
                Button settingsButton,
                Button quitButton,
                Dropdown resolutionDropdown,
                Dropdown displayModeDropdown,
                Button applyButton,
                Button backButton)
            {
                Canvas = canvas;
                Controller = controller;
                MainPanel = mainPanel;
                SettingsPanel = settingsPanel;
                StartButton = startButton;
                SettingsButton = settingsButton;
                QuitButton = quitButton;
                ResolutionDropdown = resolutionDropdown;
                DisplayModeDropdown = displayModeDropdown;
                ApplyButton = applyButton;
                BackButton = backButton;
            }

            public Canvas Canvas { get; }

            public MainMenuController Controller { get; }

            public GameObject MainPanel { get; }

            public GameObject SettingsPanel { get; }

            public Button StartButton { get; }

            public Button SettingsButton { get; }

            public Button QuitButton { get; }

            public Dropdown ResolutionDropdown { get; }

            public Dropdown DisplayModeDropdown { get; }

            public Button ApplyButton { get; }

            public Button BackButton { get; }
        }
    }
}