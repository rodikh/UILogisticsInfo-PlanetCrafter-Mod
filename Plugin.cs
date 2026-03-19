using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LogisticsInfo
{
    [BepInPlugin("rodik.theplanetcraftermods.logisticsinfo", "(UI) Logistics Info", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        static Plugin me;
        static ManualLogSource logger;

        static Actionnable currentActionable;
        static Inventory currentInventory;
        static bool displayEnabled = true;

        static GameObject panel;
        static GameObject bgGo;
        static Image bgImage;
        static RectTransform bgRt;

        static Text titleText;
        static RectTransform titleRt;

        static Text priorityText;
        static RectTransform priorityRt;

        static GameObject supplyHeaderGo;
        static Text supplyHeaderText;
        static RectTransform supplyHeaderRt;
        static Image supplyHeaderBg;

        static GameObject demandHeaderGo;
        static Text demandHeaderText;
        static RectTransform demandHeaderRt;
        static Image demandHeaderBg;

        static readonly List<ItemRow> supplyRows = new List<ItemRow>();
        static readonly List<ItemRow> demandRows = new List<ItemRow>();

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> debugMode;
        static ConfigEntry<string> keyToggle;

        static ConfigEntry<int> panelX;
        static ConfigEntry<int> panelY;
        static ConfigEntry<int> panelWidth;
        static ConfigEntry<float> panelOpacity;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<int> iconSize;
        static ConfigEntry<int> rowHeight;
        static ConfigEntry<int> cfgMargin;

        static InputAction toggleAction;
        static Font font;

        static readonly Color supplyColor = new Color(0.3f, 0.9f, 0.3f, 1f);
        static readonly Color demandColor = new Color(0.9f, 0.45f, 0.3f, 1f);
        static readonly Color supplyHeaderBgColor = new Color(0.15f, 0.35f, 0.15f, 0.8f);
        static readonly Color demandHeaderBgColor = new Color(0.35f, 0.15f, 0.12f, 0.8f);

        void Awake()
        {
            me = this;
            logger = Logger;
            Logger.LogInfo("Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging?");

            keyToggle = Config.Bind("Keys", "Toggle", "L",
                "Primary key (with LeftCtrl held) to toggle the logistics info display. Use a single letter or key name, e.g. L.");

            panelX = Config.Bind("UI", "PanelX", -500,
                "Shift the panel in the X direction by this amount relative to screen center.");
            panelY = Config.Bind("UI", "PanelY", 0,
                "Shift the panel in the Y direction by this amount relative to screen center.");
            panelWidth = Config.Bind("UI", "PanelWidth", 350,
                "The width of the panel.");
            panelOpacity = Config.Bind("UI", "PanelOpacity", 0.95f,
                "The opacity: 1 - fully opaque, 0 - fully transparent.");
            fontSize = Config.Bind("UI", "FontSize", 20,
                "The font size.");
            iconSize = Config.Bind("UI", "IconSize", 26,
                "The icon size in pixels.");
            rowHeight = Config.Bind("UI", "RowHeight", 30,
                "The height of item rows in pixels.");
            cfgMargin = Config.Bind("UI", "Margin", 5,
                "The margin between visual elements.");

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            UpdateKeyBindings();

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void Log(string message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        static void UpdateKeyBindings()
        {
            toggleAction?.Disable();
            toggleAction?.Dispose();

            var key = keyToggle.Value?.Trim() ?? "L";
            if (!key.StartsWith("<"))
            {
                key = "<Keyboard>/" + key;
            }

            // LeftCtrl + key (same pattern as Ctrl+ shortcuts in-game)
            toggleAction = new InputAction("LogisticsInfo Toggle", type: InputActionType.Button);
            toggleAction.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<Keyboard>/leftCtrl")
                .With("Button", key);
            toggleAction.Enable();
        }

        void Update()
        {
            if (!modEnabled.Value) return;

            if (toggleAction != null && toggleAction.WasPressedThisFrame())
            {
                displayEnabled = !displayEnabled;
                Log("Display toggled: " + displayEnabled);
                if (!displayEnabled && panel != null)
                {
                    panel.SetActive(false);
                }
            }

            var wh = Managers.GetManager<WindowsHandler>();
            if (wh != null && wh.GetHasUiOpen())
            {
                if (panel != null && panel.activeSelf)
                {
                    panel.SetActive(false);
                }
            }
        }

        // --- Harmony Patches ---

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionOpenable), nameof(ActionOpenable.OnHover))]
        static void ActionOpenable_OnHover(ActionOpenable __instance)
        {
            if (!modEnabled.Value || !displayEnabled) return;
            TryShowLogisticsInfo(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionOpenable), nameof(ActionOpenable.OnHoverOut))]
        static void ActionOpenable_OnHoverOut(ActionOpenable __instance)
        {
            if (__instance == currentActionable)
            {
                ClearState();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionGroupSelector), nameof(ActionGroupSelector.OnHover))]
        static void ActionGroupSelector_OnHover(ActionGroupSelector __instance)
        {
            if (!modEnabled.Value || !displayEnabled) return;
            TryShowLogisticsInfo(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionGroupSelector), nameof(ActionGroupSelector.OnHoverOut))]
        static void ActionGroupSelector_OnHoverOut(ActionGroupSelector __instance)
        {
            if (__instance == currentActionable)
            {
                ClearState();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VisualsToggler), nameof(VisualsToggler.ToggleUi))]
        static void VisualsToggler_ToggleUi(List<GameObject> ___uisToHide)
        {
            bool uiActive = ___uisToHide[0].activeSelf;
            if (panel != null && !uiActive)
            {
                panel.SetActive(false);
            }
        }

        // --- Core Logic ---

        static void ClearState()
        {
            currentActionable = null;
            currentInventory = null;
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        static void TryShowLogisticsInfo(Actionnable instance)
        {
            var inventoryAssoc = instance.GetComponent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, instance));
                return;
            }

            var inventoryAssocProxy = instance.GetComponent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, instance));
                return;
            }

            inventoryAssoc = instance.GetComponentInParent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, instance));
                return;
            }

            inventoryAssocProxy = instance.GetComponentInParent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, instance));
            }
        }

        static void OnInventory(Inventory inventory, Actionnable instance)
        {
            if (inventory == null) return;

            var logisticEntity = inventory.GetLogisticEntity();
            if (logisticEntity == null || !logisticEntity.HasDemandOrSupplyGroups())
            {
                if (panel != null) panel.SetActive(false);
                return;
            }

            currentActionable = instance;
            currentInventory = inventory;

            if (panel == null)
            {
                CreatePanel();
            }

            UpdateDisplay();
            panel.SetActive(true);
        }

        // --- UI Construction ---

        static void CreatePanel()
        {
            panel = new GameObject("LogisticsInfoPanel");
            var canvas = panel.AddComponent<Canvas>();
            canvas.sortingOrder = 200;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            bgGo = new GameObject("Background");
            bgGo.transform.SetParent(panel.transform, false);
            bgImage = bgGo.AddComponent<Image>();
            bgRt = bgGo.GetComponent<RectTransform>();

            var outline = bgGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            outline.effectDistance = new Vector2(1, -1);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(bgGo.transform, false);
            titleText = titleGo.AddComponent<Text>();
            titleText.font = font;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            titleRt = titleGo.GetComponent<RectTransform>();

            var priorityGo = new GameObject("Priority");
            priorityGo.transform.SetParent(bgGo.transform, false);
            priorityText = priorityGo.AddComponent<Text>();
            priorityText.font = font;
            priorityText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            priorityText.alignment = TextAnchor.MiddleCenter;
            priorityText.horizontalOverflow = HorizontalWrapMode.Overflow;
            priorityText.verticalOverflow = VerticalWrapMode.Overflow;
            priorityRt = priorityGo.GetComponent<RectTransform>();

            supplyHeaderGo = CreateSectionHeader("SupplyHeader", "\u25b2 SUPPLY", supplyColor,
                supplyHeaderBgColor, out supplyHeaderText, out supplyHeaderRt, out supplyHeaderBg);

            demandHeaderGo = CreateSectionHeader("DemandHeader", "\u25bc DEMAND", demandColor,
                demandHeaderBgColor, out demandHeaderText, out demandHeaderRt, out demandHeaderBg);
        }

        static GameObject CreateSectionHeader(string name, string text, Color textColor,
            Color bgColor, out Text headerText, out RectTransform headerRt, out Image headerBgImage)
        {
            var headerBgGo = new GameObject(name + "Bg");
            headerBgGo.transform.SetParent(bgGo.transform, false);
            headerBgImage = headerBgGo.AddComponent<Image>();
            headerBgImage.color = bgColor;
            headerBgGo.GetComponent<RectTransform>();

            var headerGo = new GameObject(name);
            headerGo.transform.SetParent(headerBgGo.transform, false);
            headerText = headerGo.AddComponent<Text>();
            headerText.font = font;
            headerText.color = textColor;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleLeft;
            headerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            headerText.verticalOverflow = VerticalWrapMode.Overflow;
            headerText.text = text;
            headerRt = headerGo.GetComponent<RectTransform>();

            return headerBgGo;
        }

        static ItemRow CreateItemRow(string name)
        {
            var row = new ItemRow();

            row.rowObject = new GameObject(name);
            row.rowObject.transform.SetParent(bgGo.transform, false);
            row.rowRt = row.rowObject.AddComponent<RectTransform>();

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.rowObject.transform, false);
            row.icon = iconGo.AddComponent<Image>();
            row.iconRt = iconGo.GetComponent<RectTransform>();

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(row.rowObject.transform, false);
            row.nameText = nameGo.AddComponent<Text>();
            row.nameText.font = font;
            row.nameText.color = Color.white;
            row.nameText.alignment = TextAnchor.MiddleLeft;
            row.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.nameText.verticalOverflow = VerticalWrapMode.Overflow;
            row.nameRt = nameGo.GetComponent<RectTransform>();

            return row;
        }

        static void EnsureRows(List<ItemRow> rows, int count, string prefix)
        {
            while (rows.Count < count)
            {
                rows.Add(CreateItemRow(prefix + rows.Count));
            }
        }

        // --- UI Layout & Update ---

        static void UpdateDisplay()
        {
            if (panel == null || currentInventory == null) return;

            var logisticEntity = currentInventory.GetLogisticEntity();
            if (logisticEntity == null) return;

            var supplyGroups = logisticEntity.GetSupplyGroups();
            var demandGroups = logisticEntity.GetDemandGroups();

            int supplyCount = supplyGroups?.Count ?? 0;
            int demandCount = demandGroups?.Count ?? 0;

            string containerName = GetContainerName();

            int rh = rowHeight.Value;
            int m = cfgMargin.Value;
            int fs = fontSize.Value;
            int pw = panelWidth.Value;
            int titleH = rh + 4;
            int priorityH = (int)(rh * 0.7f);
            int sectionH = rh;

            EnsureRows(supplyRows, supplyCount, "SupplyRow");
            EnsureRows(demandRows, demandCount, "DemandRow");

            int totalHeight = 2 * m + titleH + priorityH;
            if (supplyCount > 0)
            {
                totalHeight += m + sectionH + supplyCount * rh;
            }
            if (demandCount > 0)
            {
                totalHeight += m + sectionH + demandCount * rh;
            }
            totalHeight += m;

            bgImage.color = new Color(0.05f, 0.05f, 0.1f, panelOpacity.Value);
            bgRt.localPosition = new Vector3(panelX.Value + pw / 2f, panelY.Value, 0);
            bgRt.sizeDelta = new Vector2(pw, totalHeight);

            float yPos = totalHeight / 2f - m;
            float contentW = pw - 2 * m;

            titleText.fontSize = fs + 2;
            titleText.text = containerName;
            yPos -= titleH / 2f;
            titleRt.localPosition = new Vector3(0, yPos, 0);
            titleRt.sizeDelta = new Vector2(contentW, titleH);
            yPos -= titleH / 2f;

            priorityText.fontSize = (int)(fs * 0.7f);
            priorityText.text = "Priority: " + logisticEntity.GetPriority();
            yPos -= priorityH / 2f;
            priorityRt.localPosition = new Vector3(0, yPos, 0);
            priorityRt.sizeDelta = new Vector2(contentW, priorityH);
            yPos -= priorityH / 2f;

            bool hasSupply = supplyCount > 0;
            supplyHeaderGo.SetActive(hasSupply);
            if (hasSupply)
            {
                yPos -= m;
                yPos = LayoutSectionHeader(supplyHeaderGo, supplyHeaderText, supplyHeaderRt,
                    supplyHeaderBg, fs, yPos, contentW, sectionH, m);
                yPos = LayoutItemRows(supplyRows, supplyGroups, supplyCount, fs, yPos, contentW, rh, m);
            }
            HideExtraRows(supplyRows, supplyCount);

            bool hasDemand = demandCount > 0;
            demandHeaderGo.SetActive(hasDemand);
            if (hasDemand)
            {
                yPos -= m;
                yPos = LayoutSectionHeader(demandHeaderGo, demandHeaderText, demandHeaderRt,
                    demandHeaderBg, fs, yPos, contentW, sectionH, m);
                yPos = LayoutItemRows(demandRows, demandGroups, demandCount, fs, yPos, contentW, rh, m);
            }
            HideExtraRows(demandRows, demandCount);
        }

        static float LayoutSectionHeader(GameObject headerParent, Text headerText,
            RectTransform headerRt, Image headerBgImage,
            int fs, float yPos, float contentW, int sectionH, int m)
        {
            headerText.fontSize = fs;
            var parentRt = headerParent.GetComponent<RectTransform>();

            yPos -= sectionH / 2f;
            parentRt.localPosition = new Vector3(0, yPos, 0);
            parentRt.sizeDelta = new Vector2(contentW, sectionH);
            headerRt.localPosition = new Vector3(m, 0, 0);
            headerRt.sizeDelta = new Vector2(contentW - 2 * m, sectionH);
            yPos -= sectionH / 2f;

            return yPos;
        }

        static float LayoutItemRows(List<ItemRow> rows, HashSet<Group> groups,
            int count, int fs, float yPos, float contentW, int rh, int m)
        {
            int iSize = iconSize.Value;
            int i = 0;
            foreach (var group in groups)
            {
                if (i >= count || i >= rows.Count) break;
                var row = rows[i];
                row.rowObject.SetActive(true);

                row.nameText.fontSize = fs;
                row.nameText.text = Readable.GetGroupName(group);

                var sprite = group.GetImage();
                if (sprite != null)
                {
                    row.icon.sprite = sprite;
                    row.icon.enabled = true;
                }
                else
                {
                    row.icon.enabled = false;
                }

                yPos -= rh / 2f;
                row.rowRt.localPosition = new Vector3(0, yPos, 0);
                row.rowRt.sizeDelta = new Vector2(contentW, rh);

                row.iconRt.localPosition = new Vector3(-contentW / 2f + m + iSize / 2f, 0, 0);
                row.iconRt.sizeDelta = new Vector2(iSize, iSize);

                float nameX = -contentW / 2f + m + iSize + m;
                float nameW = contentW - iSize - 3 * m;
                row.nameRt.localPosition = new Vector3(nameX + nameW / 2f, 0, 0);
                row.nameRt.sizeDelta = new Vector2(nameW, rh);

                yPos -= rh / 2f;
                i++;
            }
            return yPos;
        }

        static void HideExtraRows(List<ItemRow> rows, int visibleCount)
        {
            for (int i = visibleCount; i < rows.Count; i++)
            {
                rows[i].rowObject.SetActive(false);
            }
        }

        static string GetContainerName()
        {
            if (currentActionable == null) return "";

            var woa = currentActionable.GetComponentInParent<WorldObjectAssociated>()
                ?? currentActionable.GetComponentInChildren<WorldObjectAssociated>();
            if (woa != null && woa.GetWorldObject() != null)
            {
                return Readable.GetGroupName(woa.GetWorldObject().GetGroup());
            }

            if (currentActionable.TryGetComponent<ConstructibleProxy>(out var cp))
            {
                var group = cp.GetGroup();
                if (group != null)
                {
                    return Readable.GetGroupName(group);
                }
            }

            return "Container";
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            UpdateKeyBindings();
            if (panel != null)
            {
                Object.Destroy(panel);
                panel = null;
            }
            supplyRows.Clear();
            demandRows.Clear();
        }

        internal class ItemRow
        {
            internal GameObject rowObject;
            internal RectTransform rowRt;
            internal Image icon;
            internal RectTransform iconRt;
            internal Text nameText;
            internal RectTransform nameRt;
        }
    }
}
