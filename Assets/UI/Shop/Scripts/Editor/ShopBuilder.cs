// Assets/UI/Shop/Scripts/Editor/ShopBuilder.cs

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cosmoquarium.UI.Shop.Editor
{
    /// <summary>
    /// Creates or rebuilds only the Shop scene/prefabs and configures only
    /// Shop-facing metadata on the existing UpgradeDatabase prefab.
    /// </summary>
    internal static class ShopBuilder
    {
        private const string RootFolder = "Assets/UI/Shop";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string ScenesFolder = RootFolder + "/Scenes";

        private const string ShopItemPrefabPath =
            PrefabsFolder + "/ShopItemUI.prefab";
        private const string ShopUiPrefabPath =
            PrefabsFolder + "/ShopUI.prefab";
        private const string ShopScenePath =
            ScenesFolder + "/Shop.unity";

        private const string UpgradeDatabasePrefabPath =
            "Assets/PreFabs/Game Managers/UpgradeDatabase.prefab";

        private const string InputActionsPath =
            "Assets/Settings/InputSystem_Actions.inputactions";
        private const string UiActionMapName = "UI";

        private const string UpgradeTemplateId = "upgrade_template";
        private const int ExpectedShopProductCount = 6;

        private static readonly Vector2 ReferenceResolution =
            new Vector2(1920f, 1080f);

        private static readonly ProductMetadata[] Products =
        {
            new ProductMetadata(
                "double_health",
                "Double Health",
                5,
                "Doubles a fish's maximum health, making it much harder to defeat."),

            new ProductMetadata(
                "double_click",
                "Double Click",
                6,
                "Doubles the number of damage hits produced when the upgraded fish is clicked."),

            new ProductMetadata(
                "untargetable",
                "Untargetable",
                8,
                "Prevents predators from selecting the upgraded fish as a hunt target. It may still receive non-targeted damage."),

            new ProductMetadata(
                "shooting_upgrade",
                "Shooting Upgrade",
                10,
                "Gives a fish the ability to automatically fire at nearby predators."),

            new ProductMetadata(
                "double_gold",
                "Double Gold",
                12,
                "Doubles a GoldFish's gold multiplier, increasing the value of the gold it produces."),

            new ProductMetadata(
                "double_fish",
                "Double Fish",
                15,
                "Creates a duplicate of the fish that receives it without permanently occupying that fish's upgrade slot."),
        };

        [MenuItem("Tools/Cosmoquarium/Create Shop")]
        public static void CreateShop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "Shop Builder cannot run while entering or running Play Mode.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                // Validate all external dependencies before changing assets.
                ValidateUpgradeDatabaseForShop();
                InputActionAsset inputActions =
                    LoadAndValidateInputActions();

                ValidateGeneratedAssetPath<GameObject>(
                    ShopItemPrefabPath);
                ValidateGeneratedAssetPath<GameObject>(
                    ShopUiPrefabPath);
                ValidateGeneratedAssetPath<SceneAsset>(
                    ShopScenePath);

                EnsureGeneratedFolders();

                // Configure only the newly introduced Shop metadata fields.
                ConfigureUpgradeDatabaseMetadata();

                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

                ShopItemView itemPrefab =
                    CreateOrUpdateShopItemPrefab();

                GameObject shopRoot = new GameObject("Shop");

                ShopUi ui = BuildShopUi(
                    shopRoot.transform);

                InstantiateUpgradeDatabase(
                    shopRoot.transform,
                    scene);

                EventSystem eventSystem =
                    BuildEventSystem(
                        shopRoot.transform,
                        ui.BackButton.gameObject,
                        inputActions);

                AssignControllerReferences(
                    ui.Controller,
                    itemPrefab,
                    ui.ProductGrid,
                    ui.GoldText,
                    ui.FeedbackText,
                    ui.BackButton,
                    ui.ContinueButton);

                SaveShopUiPrefab(ui.Canvas);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(
                        scene,
                        ShopScenePath))
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save the Shop scene at '{ShopScenePath}'.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                SceneAsset sceneAsset =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        ShopScenePath);

                Selection.activeObject = sceneAsset;
                EditorGUIUtility.PingObject(sceneAsset);

                Debug.Log(
                    "Shop generated successfully.\n"
                    + $"Scene: {ShopScenePath}\n"
                    + $"Shop UI Prefab: {ShopUiPrefabPath}\n"
                    + $"Shop Item Prefab: {ShopItemPrefabPath}\n"
                    + $"Upgrade Database: {UpgradeDatabasePrefabPath}\n"
                    + $"Visible Shop products: {ExpectedShopProductCount}\n"
                    + $"Input: {InputActionsPath} / {UiActionMapName}\n"
                    + $"Initial selection: {eventSystem.firstSelectedGameObject.name}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ValidateUpgradeDatabaseForShop()
        {
            GameObject prefabContentsRoot =
                PrefabUtility.LoadPrefabContents(
                    UpgradeDatabasePrefabPath);

            if (prefabContentsRoot == null)
            {
                throw new InvalidOperationException(
                    $"Required UpgradeDatabase prefab was not found at "
                    + $"'{UpgradeDatabasePrefabPath}'.");
            }

            try
            {
                UpgradeDatabase database =
                    prefabContentsRoot.GetComponentInChildren<UpgradeDatabase>(
                        true);

                if (database == null)
                {
                    throw new InvalidOperationException(
                        $"Prefab '{UpgradeDatabasePrefabPath}' does not contain "
                        + "an UpgradeDatabase component.");
                }

                IReadOnlyList<UpgradeDatabase.UpgradeEntry> entries =
                    database.Entries;

                if (entries == null)
                {
                    throw new InvalidOperationException(
                        "UpgradeDatabase.Entries is null.");
                }

                Dictionary<string, UpgradeDatabase.UpgradeEntry> byId =
                    BuildUniqueEntryMap(entries);

                List<string> missingProductionIds =
                    Products
                        .Where(product => !byId.ContainsKey(product.UpgradeId))
                        .Select(product => product.UpgradeId)
                        .ToList();

                if (missingProductionIds.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Shop Builder stopped because the existing "
                        + "UpgradeDatabase is missing canonical production IDs: "
                        + string.Join(", ", missingProductionIds)
                        + ". No replacement gameplay upgrades will be created.");
                }

                if (!byId.ContainsKey(UpgradeTemplateId))
                {
                    throw new InvalidOperationException(
                        $"Shop Builder stopped because '{UpgradeTemplateId}' "
                        + "is missing from the existing UpgradeDatabase. "
                        + "The builder will not synthesize a replacement entry.");
                }

                HashSet<string> productionIds =
                    new HashSet<string>(
                        Products.Select(product => product.UpgradeId),
                        StringComparer.Ordinal);

                List<string> unexpectedVisibleIds =
                    entries
                        .Where(entry =>
                            entry != null
                            && entry.showInShop
                            && !productionIds.Contains(entry.upgradeId)
                            && !string.Equals(
                                entry.upgradeId,
                                UpgradeTemplateId,
                                StringComparison.Ordinal))
                        .Select(entry => entry.upgradeId)
                        .ToList();

                if (unexpectedVisibleIds.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Shop Builder stopped because non-production "
                        + "UpgradeDatabase entries are already marked showInShop: "
                        + string.Join(", ", unexpectedVisibleIds)
                        + ". The builder will not silently hide or replace "
                        + "unknown team data.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabContentsRoot);
            }
        }

        private static void ConfigureUpgradeDatabaseMetadata()
        {
            GameObject prefabContentsRoot =
                PrefabUtility.LoadPrefabContents(
                    UpgradeDatabasePrefabPath);

            if (prefabContentsRoot == null)
            {
                throw new InvalidOperationException(
                    $"Could not load '{UpgradeDatabasePrefabPath}'.");
            }

            try
            {
                UpgradeDatabase database =
                    prefabContentsRoot.GetComponentInChildren<UpgradeDatabase>(
                        true);

                if (database == null)
                {
                    throw new InvalidOperationException(
                        "UpgradeDatabase component disappeared after validation.");
                }

                Dictionary<string, UpgradeDatabase.UpgradeEntry> byId =
                    BuildUniqueEntryMap(database.Entries);

                foreach (ProductMetadata product in Products)
                {
                    UpgradeDatabase.UpgradeEntry entry =
                        byId[product.UpgradeId];

                    // Intentionally modify Shop metadata only.
                    entry.displayName = product.DisplayName;
                    entry.description = product.Description;
                    entry.cost = product.Cost;
                    entry.showInShop = true;
                }

                // The template remains gameplay/template data only.
                byId[UpgradeTemplateId].showInShop = false;

                List<UpgradeDatabase.UpgradeEntry> visibleEntries =
                    database.Entries
                        .Where(entry =>
                            entry != null
                            && entry.showInShop)
                        .ToList();

                if (visibleEntries.Count != ExpectedShopProductCount)
                {
                    throw new InvalidOperationException(
                        $"UpgradeDatabase validation failed: expected exactly "
                        + $"{ExpectedShopProductCount} entries with showInShop == true, "
                        + $"but found {visibleEntries.Count}. "
                        + "The prefab was not saved.");
                }

                HashSet<string> expectedIds =
                    new HashSet<string>(
                        Products.Select(product => product.UpgradeId),
                        StringComparer.Ordinal);

                List<string> unexpectedIds =
                    visibleEntries
                        .Where(entry => !expectedIds.Contains(entry.upgradeId))
                        .Select(entry => entry.upgradeId)
                        .ToList();

                if (unexpectedIds.Count > 0)
                {
                    throw new InvalidOperationException(
                        "UpgradeDatabase validation failed because unexpected "
                        + "entries would be visible in Shop: "
                        + string.Join(", ", unexpectedIds)
                        + ". The prefab was not saved.");
                }

                PrefabUtility.SaveAsPrefabAsset(
                    prefabContentsRoot,
                    UpgradeDatabasePrefabPath,
                    out bool savedSuccessfully);

                if (!savedSuccessfully)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save Shop metadata to "
                        + $"'{UpgradeDatabasePrefabPath}'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabContentsRoot);
            }
        }

        private static Dictionary<string, UpgradeDatabase.UpgradeEntry>
            BuildUniqueEntryMap(
                IReadOnlyList<UpgradeDatabase.UpgradeEntry> entries)
        {
            if (entries == null)
            {
                throw new InvalidOperationException(
                    "UpgradeDatabase entries are unavailable.");
            }

            Dictionary<string, UpgradeDatabase.UpgradeEntry> byId =
                new Dictionary<string, UpgradeDatabase.UpgradeEntry>(
                    StringComparer.Ordinal);

            foreach (UpgradeDatabase.UpgradeEntry entry in entries)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.upgradeId))
                {
                    continue;
                }

                if (byId.ContainsKey(entry.upgradeId))
                {
                    throw new InvalidOperationException(
                        $"UpgradeDatabase contains duplicate upgradeId "
                        + $"'{entry.upgradeId}'. Shop Builder will not guess "
                        + "which entry is authoritative.");
                }

                byId.Add(
                    entry.upgradeId,
                    entry);
            }

            return byId;
        }

        private static ShopItemView CreateOrUpdateShopItemPrefab()
        {
            GameObject itemRoot =
                new GameObject(
                    "ShopItemUI",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(VerticalLayoutGroup),
                    typeof(LayoutElement),
                    typeof(ShopItemView));

            try
            {
                RectTransform rootRect =
                    itemRoot.GetComponent<RectTransform>();
                rootRect.sizeDelta =
                    new Vector2(500f, 320f);

                Image background =
                    itemRoot.GetComponent<Image>();
                background.color =
                    new Color(0.09f, 0.14f, 0.20f, 0.96f);
                background.raycastTarget = false;

                VerticalLayoutGroup layout =
                    itemRoot.GetComponent<VerticalLayoutGroup>();
                layout.padding =
                    new RectOffset(22, 22, 18, 18);
                layout.spacing = 8f;
                layout.childAlignment =
                    TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                LayoutElement rootLayout =
                    itemRoot.GetComponent<LayoutElement>();
                rootLayout.preferredWidth = 500f;
                rootLayout.preferredHeight = 320f;

                Image icon =
                    CreateIcon(
                        itemRoot.transform);

                TextMeshProUGUI nameText =
                    CreateText(
                        itemRoot.transform,
                        "NameText",
                        "Upgrade Name",
                        27f,
                        TextAlignmentOptions.Center,
                        FontStyles.Bold);
                SetPreferredHeight(
                    nameText.gameObject,
                    40f);

                TextMeshProUGUI descriptionText =
                    CreateText(
                        itemRoot.transform,
                        "DescriptionText",
                        "Upgrade description",
                        18f,
                        TextAlignmentOptions.TopLeft);
                LayoutElement descriptionLayout =
                    GetOrAddLayoutElement(
                        descriptionText.gameObject);
                descriptionLayout.preferredHeight = 82f;
                descriptionLayout.flexibleHeight = 1f;

                TextMeshProUGUI costText =
                    CreateText(
                        itemRoot.transform,
                        "CostText",
                        "Cost: 0",
                        19f,
                        TextAlignmentOptions.Center,
                        FontStyles.Bold);
                SetPreferredHeight(
                    costText.gameObject,
                    30f);

                TextMeshProUGUI ownedCountText =
                    CreateText(
                        itemRoot.transform,
                        "OwnedCountText",
                        "Owned: 0",
                        18f,
                        TextAlignmentOptions.Center);
                SetPreferredHeight(
                    ownedCountText.gameObject,
                    28f);

                Button purchaseButton =
                    CreateButton(
                        itemRoot.transform,
                        "PurchaseButton",
                        "Purchase",
                        new Vector2(220f, 50f));

                LayoutElement buttonLayout =
                    GetOrAddLayoutElement(
                        purchaseButton.gameObject);
                buttonLayout.preferredHeight = 50f;
                buttonLayout.minHeight = 50f;

                ShopItemView view =
                    itemRoot.GetComponent<ShopItemView>();

                SerializedObject serializedView =
                    new SerializedObject(view);
                serializedView.Update();

                AssignObjectReference(
                    serializedView,
                    "icon",
                    icon);
                AssignObjectReference(
                    serializedView,
                    "displayNameText",
                    nameText);
                AssignObjectReference(
                    serializedView,
                    "descriptionText",
                    descriptionText);
                AssignObjectReference(
                    serializedView,
                    "costText",
                    costText);
                AssignObjectReference(
                    serializedView,
                    "ownedCountText",
                    ownedCountText);
                AssignObjectReference(
                    serializedView,
                    "purchaseButton",
                    purchaseButton);

                serializedView.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);

                GameObject savedPrefab =
                    PrefabUtility.SaveAsPrefabAsset(
                        itemRoot,
                        ShopItemPrefabPath,
                        out bool savedSuccessfully);

                if (!savedSuccessfully
                    || savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save Shop item prefab at "
                        + $"'{ShopItemPrefabPath}'.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    itemRoot);
            }

            GameObject prefabAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    ShopItemPrefabPath);

            if (prefabAsset == null)
            {
                throw new InvalidOperationException(
                    $"Shop item prefab could not be reloaded from "
                    + $"'{ShopItemPrefabPath}'.");
            }

            ShopItemView prefabView =
                prefabAsset.GetComponent<ShopItemView>();

            if (prefabView == null)
            {
                throw new InvalidOperationException(
                    "Generated ShopItemUI prefab does not contain ShopItemView.");
            }

            return prefabView;
        }

        private static ShopUi BuildShopUi(
            Transform root)
        {
            GameObject canvasObject =
                new GameObject(
                    "Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));

            canvasObject.transform.SetParent(
                root,
                false);

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

            ShopController controller =
                canvasObject.AddComponent<ShopController>();

            GameObject background =
                CreateStretchObject(
                    "Background",
                    canvasObject.transform,
                    typeof(Image));

            Image backgroundImage =
                background.GetComponent<Image>();
            backgroundImage.color =
                new Color(0.035f, 0.065f, 0.095f, 1f);
            backgroundImage.raycastTarget = false;

            GameObject headerPanel =
                CreateAnchoredPanel(
                    "HeaderPanel",
                    background.transform,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -36f),
                    new Vector2(-140f, 104f));

            HorizontalLayoutGroup headerLayout =
                headerPanel.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding =
                new RectOffset(28, 28, 12, 12);
            headerLayout.spacing = 20f;
            headerLayout.childAlignment =
                TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = true;

            TextMeshProUGUI title =
                CreateText(
                    headerPanel.transform,
                    "Title",
                    "COSMOQUARIUM SHOP",
                    42f,
                    TextAlignmentOptions.Left,
                    FontStyles.Bold);

            LayoutElement titleLayout =
                GetOrAddLayoutElement(
                    title.gameObject);
            titleLayout.flexibleWidth = 2f;

            TextMeshProUGUI goldText =
                CreateText(
                    headerPanel.transform,
                    "GoldText",
                    "Gold: 0",
                    30f,
                    TextAlignmentOptions.Right,
                    FontStyles.Bold);

            LayoutElement goldLayout =
                GetOrAddLayoutElement(
                    goldText.gameObject);
            goldLayout.flexibleWidth = 1f;

            GameObject productGrid =
                new GameObject(
                    "ProductGrid",
                    typeof(RectTransform),
                    typeof(GridLayoutGroup));

            productGrid.transform.SetParent(
                background.transform,
                false);

            RectTransform productGridRect =
                productGrid.GetComponent<RectTransform>();
            productGridRect.anchorMin =
                new Vector2(0f, 0f);
            productGridRect.anchorMax =
                new Vector2(1f, 1f);
            productGridRect.offsetMin =
                new Vector2(100f, 210f);
            productGridRect.offsetMax =
                new Vector2(-100f, -170f);
            productGridRect.localScale =
                Vector3.one;

            GridLayoutGroup grid =
                productGrid.GetComponent<GridLayoutGroup>();
            grid.cellSize =
                new Vector2(500f, 320f);
            grid.spacing =
                new Vector2(30f, 24f);
            grid.startCorner =
                GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis =
                GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment =
                TextAnchor.UpperCenter;
            grid.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            TextMeshProUGUI feedbackText =
                CreateText(
                    background.transform,
                    "FeedbackText",
                    string.Empty,
                    22f,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            ConfigureCenteredRect(
                feedbackText.rectTransform,
                new Vector2(0f, -372f),
                new Vector2(1100f, 48f));

            GameObject footerPanel =
                CreateAnchoredPanel(
                    "FooterPanel",
                    background.transform,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 34f),
                    new Vector2(-140f, 102f));

            HorizontalLayoutGroup footerLayout =
                footerPanel.AddComponent<HorizontalLayoutGroup>();
            footerLayout.padding =
                new RectOffset(24, 24, 18, 18);
            footerLayout.spacing = 36f;
            footerLayout.childAlignment =
                TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;

            Button backButton =
                CreateButton(
                    footerPanel.transform,
                    "BackButton",
                    "Back",
                    new Vector2(280f, 60f));

            Button continueButton =
                CreateButton(
                    footerPanel.transform,
                    "ContinueButton",
                    "Continue",
                    new Vector2(280f, 60f));

            return new ShopUi(
                canvas,
                controller,
                productGrid.transform,
                goldText,
                feedbackText,
                backButton,
                continueButton);
        }

        private static GameObject CreateAnchoredPanel(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject panel =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(Image));

            panel.transform.SetParent(
                parent,
                false);

            RectTransform rect =
                panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;

            Image image =
                panel.GetComponent<Image>();
            image.color =
                new Color(0.07f, 0.11f, 0.16f, 0.96f);
            image.raycastTarget = false;

            return panel;
        }

        private static Image CreateIcon(
            Transform parent)
        {
            GameObject iconObject =
                new GameObject(
                    "Icon",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(LayoutElement));

            iconObject.transform.SetParent(
                parent,
                false);

            Image icon =
                iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            LayoutElement layout =
                iconObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 82f;
            layout.minHeight = 82f;

            return icon;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            TextAlignmentOptions alignment,
            FontStyles fontStyle = FontStyles.Normal)
        {
            GameObject textObject =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));

            textObject.transform.SetParent(
                parent,
                false);

            TextMeshProUGUI text =
                textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.raycastTarget = false;
            text.overflowMode =
                TextOverflowModes.Overflow;

            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font =
                    TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 size)
        {
            GameObject buttonObject =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(LayoutElement));

            buttonObject.transform.SetParent(
                parent,
                false);

            RectTransform rect =
                buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            Image image =
                buttonObject.GetComponent<Image>();
            image.color =
                new Color(0.15f, 0.28f, 0.40f, 1f);

            Button button =
                buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            LayoutElement layout =
                buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;
            layout.minWidth = size.x;
            layout.minHeight = size.y;

            TextMeshProUGUI labelText =
                CreateText(
                    buttonObject.transform,
                    "Label",
                    label,
                    22f,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);

            StretchRect(
                labelText.rectTransform,
                10f,
                6f);

            return button;
        }

        private static void SaveShopUiPrefab(
            Canvas canvas)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                canvas.gameObject,
                ShopUiPrefabPath,
                InteractionMode.AutomatedAction,
                out bool savedSuccessfully);

            if (!savedSuccessfully)
            {
                throw new InvalidOperationException(
                    $"Unity failed to save Shop UI prefab at "
                    + $"'{ShopUiPrefabPath}'.");
            }
        }

        private static void InstantiateUpgradeDatabase(
            Transform root,
            Scene scene)
        {
            GameObject databasePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    UpgradeDatabasePrefabPath);

            if (databasePrefab == null)
            {
                throw new InvalidOperationException(
                    $"Required UpgradeDatabase prefab was not found at "
                    + $"'{UpgradeDatabasePrefabPath}'.");
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    databasePrefab,
                    scene) as GameObject;

            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Unity failed to instantiate the existing "
                    + "UpgradeDatabase prefab into the Shop scene.");
            }

            instance.name = "UpgradeDatabase";
            instance.transform.SetParent(
                root,
                false);
            instance.transform.localPosition =
                Vector3.zero;
            instance.transform.localRotation =
                Quaternion.identity;
            instance.transform.localScale =
                Vector3.one;
        }

        private static EventSystem BuildEventSystem(
            Transform root,
            GameObject firstSelected,
            InputActionAsset inputActions)
        {
            GameObject eventSystemObject =
                new GameObject(
                    "EventSystem",
                    typeof(EventSystem));

            eventSystemObject.transform.SetParent(
                root,
                false);

            EventSystem eventSystem =
                eventSystemObject.GetComponent<EventSystem>();
            eventSystem.sendNavigationEvents = true;
            eventSystem.firstSelectedGameObject =
                firstSelected;

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
                AssetDatabase.LoadAllAssetsAtPath(
                        InputActionsPath)
                    .OfType<InputActionReference>()
                    .ToArray();

            InputActionReference Resolve(
                string actionName)
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
                        + "asset and run the Shop Builder again.");
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

            EditorUtility.SetDirty(
                inputModule);
        }

        private static InputActionAsset LoadAndValidateInputActions()
        {
            InputActionAsset inputActions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    InputActionsPath);

            if (inputActions == null)
            {
                throw new InvalidOperationException(
                    $"Required Input Actions asset was not found at "
                    + $"'{InputActionsPath}'.");
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

        private static void AssignControllerReferences(
            ShopController controller,
            ShopItemView itemPrefab,
            Transform itemContainer,
            TMP_Text goldText,
            TMP_Text feedbackText,
            Button backButton,
            Button continueButton)
        {
            SerializedObject serializedController =
                new SerializedObject(controller);
            serializedController.Update();

            AssignObjectReference(
                serializedController,
                "itemPrefab",
                itemPrefab);
            AssignObjectReference(
                serializedController,
                "itemContainer",
                itemContainer);
            AssignObjectReference(
                serializedController,
                "goldText",
                goldText);
            AssignObjectReference(
                serializedController,
                "feedbackText",
                feedbackText);
            AssignObjectReference(
                serializedController,
                "backButton",
                backButton);
            AssignObjectReference(
                serializedController,
                "continueButton",
                continueButton);

            AssignString(
                serializedController,
                "mainMenuSceneName",
                "MainMenu");
            AssignString(
                serializedController,
                "aquariumSceneName",
                "Aquarium");

            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(
                controller);
        }

        private static void AssignObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    propertyName);

            if (property == null)
            {
                throw new MissingFieldException(
                    serializedObject.targetObject.GetType().FullName,
                    propertyName);
            }

            property.objectReferenceValue =
                value;
        }

        private static void AssignString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            SerializedProperty property =
                serializedObject.FindProperty(
                    propertyName);

            if (property == null)
            {
                throw new MissingFieldException(
                    serializedObject.targetObject.GetType().FullName,
                    propertyName);
            }

            property.stringValue =
                value;
        }

        private static GameObject CreateStretchObject(
            string objectName,
            Transform parent,
            params Type[] additionalComponents)
        {
            Type[] components =
                new Type[additionalComponents.Length + 1];

            components[0] =
                typeof(RectTransform);

            for (int index = 0;
                 index < additionalComponents.Length;
                 index++)
            {
                components[index + 1] =
                    additionalComponents[index];
            }

            GameObject gameObject =
                new GameObject(
                    objectName,
                    components);

            gameObject.transform.SetParent(
                parent,
                false);

            RectTransform rect =
                gameObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            return gameObject;
        }

        private static void ConfigureCenteredRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin =
                new Vector2(0.5f, 0.5f);
            rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            rect.pivot =
                new Vector2(0.5f, 0.5f);
            rect.anchoredPosition =
                anchoredPosition;
            rect.sizeDelta =
                size;
            rect.localScale =
                Vector3.one;
        }

        private static void StretchRect(
            RectTransform rect,
            float horizontalInset,
            float verticalInset)
        {
            rect.anchorMin =
                Vector2.zero;
            rect.anchorMax =
                Vector2.one;
            rect.offsetMin =
                new Vector2(
                    horizontalInset,
                    verticalInset);
            rect.offsetMax =
                new Vector2(
                    -horizontalInset,
                    -verticalInset);
            rect.localScale =
                Vector3.one;
        }

        private static LayoutElement GetOrAddLayoutElement(
            GameObject gameObject)
        {
            LayoutElement layout =
                gameObject.GetComponent<LayoutElement>();

            if (layout == null)
            {
                layout =
                    gameObject.AddComponent<LayoutElement>();
            }

            return layout;
        }

        private static void SetPreferredHeight(
            GameObject gameObject,
            float height)
        {
            LayoutElement layout =
                GetOrAddLayoutElement(
                    gameObject);

            layout.preferredHeight =
                height;
            layout.minHeight =
                height;
        }

        private static void EnsureGeneratedFolders()
        {
            EnsureFolder(
                "Assets",
                "UI");
            EnsureFolder(
                "Assets/UI",
                "Shop");
            EnsureFolder(
                RootFolder,
                "Art");
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

            if (!AssetDatabase.IsValidFolder(
                    fullPath))
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
                AssetDatabase.AssetPathToGUID(
                    path);

            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            UnityEngine.Object existingAsset =
                AssetDatabase.LoadMainAssetAtPath(
                    path);

            if (existingAsset is T)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Refusing to overwrite '{path}' because an existing "
                + $"asset at that path is not a {typeof(T).Name}.");
        }

        private sealed class ProductMetadata
        {
            public ProductMetadata(
                string upgradeId,
                string displayName,
                int cost,
                string description)
            {
                UpgradeId = upgradeId;
                DisplayName = displayName;
                Cost = cost;
                Description = description;
            }

            public string UpgradeId { get; }
            public string DisplayName { get; }
            public int Cost { get; }
            public string Description { get; }
        }

        private sealed class ShopUi
        {
            public ShopUi(
                Canvas canvas,
                ShopController controller,
                Transform productGrid,
                TMP_Text goldText,
                TMP_Text feedbackText,
                Button backButton,
                Button continueButton)
            {
                Canvas = canvas;
                Controller = controller;
                ProductGrid = productGrid;
                GoldText = goldText;
                FeedbackText = feedbackText;
                BackButton = backButton;
                ContinueButton = continueButton;
            }

            public Canvas Canvas { get; }
            public ShopController Controller { get; }
            public Transform ProductGrid { get; }
            public TMP_Text GoldText { get; }
            public TMP_Text FeedbackText { get; }
            public Button BackButton { get; }
            public Button ContinueButton { get; }
        }
    }
}
