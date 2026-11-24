using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Rambler.Crew;
using Rambler.Crew.UI;
using static Rambler.EditorTools.CrewScreenBuilder;

namespace Rambler.EditorTools
{
    //Phase 5 tools: the Shipyard and Broker windows (built into the HUD in PlayerCore.prefab, like the Home Terminal),
    //their terminal prefabs, starter ships / parts / businesses, and the Shipyard scene. All safe to run again.
    public static class FleetBuilder
    {
        const string PlayerCorePath = "Assets/_Prefabs/PlayerCore/PlayerCore.prefab";
        const string PrefabFolder = "Assets/_Prefabs/Company";
        const string ShipyardTerminalPath = PrefabFolder + "/ShipyardTerminal.prefab";
        const string BrokerTerminalPath = PrefabFolder + "/BrokerTerminal.prefab";
        const string HomeTerminalPath = PrefabFolder + "/CrewTerminal.prefab";
        const string SceneFolder = "Assets/_Scenes/Shipyard";
        const string ScenePath = SceneFolder + "/Shipyard.unity";
        const string TemplateScenePath = "Assets/Sandbox/Sandbox.unity";

        static readonly Color Green = Hex("59CC73FF");

        // =====================================================================
        // Windows
        // =====================================================================

        [MenuItem("Tools/Company/Build Shipyard and Broker")]
        static void BuildScreens()
        {
            Load();
            var root = PrefabUtility.LoadPrefabContents(PlayerCorePath);
            try
            {
                Transform hud = Find(root.transform, "HUD");
                if (hud == null) { Debug.LogError("[Company] No HUD in " + PlayerCorePath); return; }
                BuildShipyard(hud);
                BuildBroker(hud);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerCorePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            TerminalPrefab<ShipyardTerminal>("ShipyardTerminal", ShipyardTerminalPath, "SHIPYARD", Blue);
            TerminalPrefab<BrokerTerminal>("BrokerTerminal", BrokerTerminalPath, "BROKER", Green);
            AssetDatabase.SaveAssets();
            Debug.Log("[Company] Shipyard and Broker built into " + PlayerCorePath + " (HUD/ShipyardScreen, HUD/BrokerScreen); terminals in " + PrefabFolder + ".");
        }

        //Holder with its own canvas over the HUD, the window, title, tabs, credits, close, message and footer
        static RectTransform Window<T>(Transform hud, string name, string title, string[] tabs, string[] tabFields, out SerializedObject so) where T : CompanyWindow
        {
            var old = hud.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var holder = Rect(name, hud);
            Stretch(holder);
            var overlay = holder.gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true; overlay.sortingOrder = 50;
            holder.gameObject.AddComponent<GraphicRaycaster>();
            so = new SerializedObject(holder.gameObject.AddComponent<T>());

            var window = Img("Window", holder, WindowCol).rectTransform;
            Center(window, new Vector2(1800, 1000));
            Set(so, "window", window.gameObject);
            var t = Txt("Title", window, title, 50, TextAlignmentOptions.MidlineLeft);
            TopLeft(t, new Vector2(36, -20), new Vector2(452, 88));
            t.fontStyle = FontStyles.Bold; t.enableWordWrapping = false; t.enableAutoSizing = true; t.fontSizeMax = 50; t.fontSizeMin = 34;
            for (int i = 0; i < tabs.Length; i++)
                Set(so, tabFields[i], Btn(tabFields[i], window, TileCol, tabs[i], 38, new Vector2(500 + i * 240, -24), new Vector2(228, 80), out _));
            var credits = Txt("Credits", window, "Credits", 42, TextAlignmentOptions.MidlineRight);
            TopRight(credits, new Vector2(-150, -20), new Vector2(380, 88));
            Set(so, "creditsText", credits);
            var close = Img("Close", window, Color.white, circle);
            TopRight(close, new Vector2(-26, -14), new Vector2(100, 100));
            var closeLabel = Txt("Label", close.rectTransform, "X", 48, TextAlignmentOptions.Center); Stretch(closeLabel.rectTransform);
            Set(so, "closeButton", close.gameObject.AddComponent<Button>());
            var message = Txt("Message", window, "", 30, TextAlignmentOptions.MidlineLeft);
            BottomLeft(message, new Vector2(40, 16), new Vector2(1100, 60));
            message.color = Orange; message.enableAutoSizing = true; message.fontSizeMax = 30; message.fontSizeMin = 22;
            Set(so, "messageText", message);
            var footer = Txt("Footer", window, "", 30, TextAlignmentOptions.MidlineRight);
            BottomRight(footer, new Vector2(-40, 16), new Vector2(600, 60));
            footer.enableAutoSizing = true; footer.fontSizeMax = 30; footer.fontSizeMin = 22;
            Set(so, "footerText", footer);
            return window;
        }

        // ---------- Shipyard ----------

        static void BuildShipyard(Transform hud)
        {
            var window = Window<ShipyardScreen>(hud, "ShipyardScreen", "SHIPYARD", new[] { "For Sale", "My Ships", "Parts" }, new[] { "forSaleTab", "myShipsTab", "partsTab" }, out var so);

            var page = Rect("ShipsPage", window);
            Body(page);
            Set(so, "shipsPage", page.gameObject);

            //Left: ship list
            var list = Img("ShipList", page, PanelCol).rectTransform;
            list.anchorMin = new Vector2(0, 0); list.anchorMax = new Vector2(0, 1); list.pivot = new Vector2(0, 1);
            list.anchoredPosition = Vector2.zero; list.sizeDelta = new Vector2(470, 0);
            var content = Scroll(list, false, 10, new RectOffset(10, 10, 10, 10));
            Set(so, "shipRowTemplate", ListRow("ShipRowTemplate", content, 450, 116, 84));
            var empty = Txt("Empty", list, "", 30, TextAlignmentOptions.Center);
            Stretch(empty.rectTransform); empty.color = Dim; empty.margin = new Vector4(20, 0, 20, 0);
            Set(so, "emptyListText", empty);

            //Middle: the 3D preview (full height on For Sale; top part on My Ships, over the spare parts)
            var frame = Img("PreviewFrame", page, PanelCol).rectTransform;
            frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(486, 0); frame.offsetMax = new Vector2(-614, 0);
            Set(so, "previewFrame", frame);
            var raw = Rect("Preview", frame);
            raw.anchorMin = Vector2.zero; raw.anchorMax = Vector2.one; raw.offsetMin = new Vector2(6, 6); raw.offsetMax = new Vector2(-6, -6);
            raw.gameObject.AddComponent<RawImage>();
            Set(so, "preview", raw.gameObject.AddComponent<ShipPreview>());
            var hint = Txt("Hint", frame, "Drag to turn", 24, TextAlignmentOptions.Center);
            BottomCenter(hint, new Vector2(0, 12), new Vector2(400, 34)); hint.color = Dim;

            var spares = Img("SpareParts", page, PanelCol).rectTransform;
            spares.anchorMin = Vector2.zero; spares.anchorMax = new Vector2(1, 0.46f);
            spares.offsetMin = new Vector2(486, 0); spares.offsetMax = new Vector2(-614, -14);
            Set(so, "sparesRoot", spares.gameObject);
            var sh = Txt("Heading", spares, "SPARE PARTS", 28, TextAlignmentOptions.MidlineLeft);
            TopLeft(sh.rectTransform, new Vector2(16, -6), new Vector2(500, 40)); sh.color = Dim;
            var sparesList = Rect("List", spares);
            sparesList.anchorMin = Vector2.zero; sparesList.anchorMax = Vector2.one; sparesList.offsetMin = new Vector2(10, 10); sparesList.offsetMax = new Vector2(-10, -50);
            Set(so, "spareTemplate", StockRow("SpareTemplate", Scroll(sparesList, false, 8, new RectOffset(0, 10, 0, 0)), 600, "Fit"));
            var sparesEmpty = Txt("Empty", spares, "No spare parts. Buy them on the Parts tab.", 26, TextAlignmentOptions.Center);
            Stretch(sparesEmpty.rectTransform); sparesEmpty.color = Dim;
            Set(so, "sparesEmptyText", sparesEmpty);

            //Right: the selected ship
            var detail = Img("Detail", page, PanelCol).rectTransform;
            detail.anchorMin = Vector2.zero; detail.anchorMax = Vector2.one; detail.offsetMin = new Vector2(1142, 0); detail.offsetMax = Vector2.zero;
            Set(so, "detailRoot", detail.gameObject);
            var name = Txt("Name", detail, "Ship", 46, TextAlignmentOptions.MidlineLeft);
            TopLeft(name, new Vector2(24, -12), new Vector2(550, 62));
            name.fontStyle = FontStyles.Bold; name.enableWordWrapping = false; name.enableAutoSizing = true; name.fontSizeMax = 46; name.fontSizeMin = 30;
            Set(so, "nameText", name);
            var desc = Txt("Description", detail, "", 26, TextAlignmentOptions.TopLeft);
            TopLeft(desc, new Vector2(24, -78), new Vector2(550, 76)); desc.color = Dim;
            desc.enableAutoSizing = true; desc.fontSizeMax = 26; desc.fontSizeMin = 20;
            Set(so, "descriptionText", desc);
            var stats = Txt("Stats", detail, "", 30, TextAlignmentOptions.TopLeft);
            TopLeft(stats, new Vector2(24, -160), new Vector2(550, 200));
            stats.enableAutoSizing = true; stats.fontSizeMax = 30; stats.fontSizeMin = 22;
            Set(so, "statsText", stats);
            var slotsHeading = Txt("SlotsHeading", detail, "SLOTS   (tap a part to take it off)", 24, TextAlignmentOptions.MidlineLeft);
            TopLeft(slotsHeading, new Vector2(24, -368), new Vector2(550, 36)); slotsHeading.color = Dim;
            var slotsHolder = Rect("Slots", detail);
            Stretch(slotsHolder);
            var slotsItem = slotsHolder.gameObject.AddComponent<CrewUIItem>();
            slotsItem.slots = new Button[6];
            slotsItem.slotLabels = new TextMeshProUGUI[6];
            for (int i = 0; i < 6; i++)
            {
                slotsItem.slots[i] = Btn("Slot" + i, slotsHolder, Hex("1F2B3AFF"), "Slot", 24, new Vector2(24 + (i % 3) * 186, -408 - (i / 3) * 72), new Vector2(176, 62), out slotsItem.slotLabels[i]);
                slotsItem.slotLabels[i].fontSizeMin = 15;
            }
            Set(so, "slotsItem", slotsItem);
            var status = Txt("Status", detail, "", 26, TextAlignmentOptions.TopLeft);
            TopLeft(status, new Vector2(24, -558), new Vector2(550, 70)); status.color = Orange;
            status.enableAutoSizing = true; status.fontSizeMax = 26; status.fontSizeMin = 20;
            Set(so, "statusText", status);
            DetailButton(so, detail, "primary", Orange, new Vector2(24, 112), new Vector2(270, 84));
            DetailButton(so, detail, "second", Blue, new Vector2(306, 112), new Vector2(270, 84));
            DetailButton(so, detail, "third", Blue, new Vector2(24, 18), new Vector2(270, 84));
            DetailButton(so, detail, "fourth", Red, new Vector2(306, 18), new Vector2(270, 84));

            //Parts tab
            var parts = Rect("PartsPage", window);
            Body(parts);
            Set(so, "partsPage", parts.gameObject);
            var bg = Img("Background", parts, PanelCol); Stretch(bg.rectTransform);
            var pl = Rect("List", parts);
            pl.anchorMin = Vector2.zero; pl.anchorMax = Vector2.one; pl.offsetMin = new Vector2(14, 14); pl.offsetMax = new Vector2(-14, -14);
            Set(so, "partTemplate", ShopRow("PartTemplate", Scroll(pl, false, 8, new RectOffset(0, 12, 0, 0))));
            parts.gameObject.SetActive(false);

            so.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);
        }

        // ---------- Broker ----------

        static void BuildBroker(Transform hud)
        {
            var window = Window<BrokerScreen>(hud, "BrokerScreen", "BROKER", new[] { "For Sale", "Owned" }, new[] { "forSaleTab", "ownedTab" }, out var so);
            var page = Rect("Page", window);
            Body(page);

            var list = Img("List", page, PanelCol).rectTransform;
            list.anchorMin = new Vector2(0, 0); list.anchorMax = new Vector2(0, 1); list.pivot = new Vector2(0, 1);
            list.anchoredPosition = Vector2.zero; list.sizeDelta = new Vector2(620, 0);
            var content = Scroll(list, false, 10, new RectOffset(10, 10, 10, 10));
            Set(so, "rowTemplate", ListRow("RowTemplate", content, 600, 120, 90));
            var empty = Txt("Empty", list, "", 30, TextAlignmentOptions.Center);
            Stretch(empty.rectTransform); empty.color = Dim; empty.margin = new Vector4(20, 0, 20, 0);
            Set(so, "emptyListText", empty);

            var detail = Img("Detail", page, PanelCol).rectTransform;
            detail.anchorMin = Vector2.zero; detail.anchorMax = Vector2.one; detail.offsetMin = new Vector2(640, 0); detail.offsetMax = Vector2.zero;
            Set(so, "detailRoot", detail.gameObject);

            var header = Rect("Header", detail);
            TopLeft(header, new Vector2(24, -20), new Vector2(1050, 124));
            var headerItem = header.gameObject.AddComponent<CrewUIItem>();
            Portrait(header, headerItem, Vector2.zero, 120, false);
            var pr = (RectTransform)headerItem.portrait.transform; pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0, 1); pr.anchoredPosition = Vector2.zero;
            headerItem.portrait.preserveAspect = true;
            headerItem.title = Txt("Name", header, "Business", 50, TextAlignmentOptions.MidlineLeft);
            TopLeft(headerItem.title.rectTransform, new Vector2(146, -4), new Vector2(900, 62)); headerItem.title.fontStyle = FontStyles.Bold;
            headerItem.body = Txt("Info", header, "", 30, TextAlignmentOptions.MidlineLeft);
            TopLeft(headerItem.body.rectTransform, new Vector2(146, -72), new Vector2(900, 44)); headerItem.body.color = Dim;
            Set(so, "detailHeader", headerItem);

            var desc = Txt("Description", detail, "", 28, TextAlignmentOptions.TopLeft);
            TopLeft(desc, new Vector2(24, -162), new Vector2(1050, 90)); desc.color = Dim;
            desc.enableAutoSizing = true; desc.fontSizeMax = 28; desc.fontSizeMin = 22;
            Set(so, "descriptionText", desc);
            var stats = Txt("Stats", detail, "", 32, TextAlignmentOptions.TopLeft);
            TopLeft(stats, new Vector2(24, -266), new Vector2(1050, 140));
            stats.enableAutoSizing = true; stats.fontSizeMax = 32; stats.fontSizeMin = 24;
            Set(so, "statsText", stats);

            BarRow(so, detail, "Level", -420, Orange, "levelRoot", "levelBar", "levelText");
            BarRow(so, detail, "Income", -500, Blue, "storeRoot", "storeBar", "storeText");

            DetailButton(so, detail, "primary", Orange, new Vector2(24, 18), new Vector2(330, 88));
            DetailButton(so, detail, "second", Blue, new Vector2(366, 18), new Vector2(330, 88));
            DetailButton(so, detail, "third", Red, new Vector2(708, 18), new Vector2(330, 88));

            so.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);
        }

        //A heading, a bar and the figure next to it, e.g. "Level  [=====     ]  Level 2 / 5"
        static void BarRow(SerializedObject so, RectTransform detail, string heading, float y, Color color, string rootField, string barField, string textField)
        {
            var row = Rect(heading + "Row", detail);
            TopLeft(row, new Vector2(24, y), new Vector2(1050, 64));
            Set(so, rootField, row.gameObject);
            var h = Txt("Heading", row, heading.ToUpperInvariant(), 26, TextAlignmentOptions.MidlineLeft);
            LeftMid(h, new Vector2(0, 0), new Vector2(170, 50)); h.color = Dim;
            var back = Img("BarBack", row, new Color(0, 0, 0, 0.45f));
            LeftMid(back, new Vector2(180, 0), new Vector2(420, 28));
            var bar = Img("Bar", back.rectTransform, color);
            Stretch(bar.rectTransform);
            bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal; bar.fillAmount = 0.4f;
            Set(so, barField, bar);
            var t = Txt("Value", row, "", 28, TextAlignmentOptions.MidlineLeft);
            LeftMid(t, new Vector2(624, 0), new Vector2(426, 56));
            t.enableAutoSizing = true; t.fontSizeMax = 28; t.fontSizeMin = 20;
            Set(so, textField, t);
        }

        static void DetailButton(SerializedObject so, RectTransform detail, string prefix, Color color, Vector2 bottomLeft, Vector2 size)
        {
            var b = Btn(prefix + "Button", detail, color, prefix, 32, Vector2.zero, size, out var label);
            BottomLeft((RectTransform)b.transform, bottomLeft, size);
            Set(so, prefix + "Button", b);
            Set(so, prefix + "Label", label);
        }

        //A selectable list row: highlight, portrait (icon or initials), name, a status line
        static CrewUIItem ListRow(string name, RectTransform content, float width, float height, float portrait)
        {
            var row = Img(name, content, TileCol).rectTransform;
            Size(row, new Vector2(width, height));
            var item = row.gameObject.AddComponent<CrewUIItem>();
            item.highlight = Img("Selected", row, new Color(1f, 0.85f, 0.2f, 0.28f)); Stretch(item.highlight.rectTransform);
            Portrait(row, item, Vector2.zero, portrait, false);
            var p = (RectTransform)item.portrait.transform; p.anchorMin = p.anchorMax = p.pivot = new Vector2(0, 0.5f); p.anchoredPosition = new Vector2(14, 0);
            item.portrait.preserveAspect = true;
            float x = portrait + 30;
            item.title = Txt("Name", row, "Name", 36, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.title.rectTransform, new Vector2(x, 18), new Vector2(width - x - 14, 48));
            item.title.enableAutoSizing = true; item.title.fontSizeMax = 36; item.title.fontSizeMin = 26;
            item.side = Txt("Status", row, "", 26, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.side.rectTransform, new Vector2(x, -26), new Vector2(width - x - 14, 38)); item.side.color = new Color(1, 1, 1, 0.8f);
            item.side.enableAutoSizing = true; item.side.fontSizeMax = 26; item.side.fontSizeMin = 18;
            item.select = row.gameObject.AddComponent<Button>();
            return item;
        }

        //A stock row (spare part): icon, name, count, a note, one button
        static CrewUIItem StockRow(string name, RectTransform content, float width, string button)
        {
            var row = Img(name, content, TileCol).rectTransform;
            Size(row, new Vector2(width, 96));
            var item = row.gameObject.AddComponent<CrewUIItem>();
            Portrait(row, item, Vector2.zero, 64, false);
            var p = (RectTransform)item.portrait.transform; p.anchorMin = p.anchorMax = p.pivot = new Vector2(0, 0.5f); p.anchoredPosition = new Vector2(10, 0);
            item.portrait.preserveAspect = true;
            item.title = Txt("Name", row, "Name", 28, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.title.rectTransform, new Vector2(86, 18), new Vector2(width - 300, 40));
            item.title.enableAutoSizing = true; item.title.fontSizeMax = 28; item.title.fontSizeMin = 20;
            item.side = Txt("Count", row, "", 24, TextAlignmentOptions.MidlineRight);
            LeftMid(item.side.rectTransform, new Vector2(width - 210, 18), new Vector2(80, 40)); item.side.color = Orange;
            item.body = Txt("Note", row, "", 20, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.body.rectTransform, new Vector2(86, -22), new Vector2(width - 214, 44)); item.body.color = Dim;
            item.body.enableAutoSizing = true; item.body.fontSizeMax = 20; item.body.fontSizeMin = 15;
            item.action = Btn("Action", row, Orange, button, 28, Vector2.zero, new Vector2(104, 66), out item.actionLabel);
            var a = (RectTransform)item.action.transform; a.anchorMin = a.anchorMax = a.pivot = new Vector2(1, 0.5f); a.anchoredPosition = new Vector2(-10, 0);
            return item;
        }

        //A shop row: icon, name, details, price, Buy
        static CrewUIItem ShopRow(string name, RectTransform content)
        {
            var row = Img(name, content, TileCol).rectTransform;
            Size(row, new Vector2(1700, 112));
            var item = row.gameObject.AddComponent<CrewUIItem>();
            Portrait(row, item, Vector2.zero, 82, false);
            var p = (RectTransform)item.portrait.transform; p.anchorMin = p.anchorMax = p.pivot = new Vector2(0, 0.5f); p.anchoredPosition = new Vector2(14, 0);
            item.portrait.preserveAspect = true;
            item.title = Txt("Name", row, "Item", 34, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.title.rectTransform, new Vector2(116, 20), new Vector2(900, 46));
            item.body = Txt("Details", row, "", 26, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.body.rectTransform, new Vector2(116, -24), new Vector2(1180, 38)); item.body.color = Dim;
            item.body.enableAutoSizing = true; item.body.fontSizeMax = 26; item.body.fontSizeMin = 19;
            item.side = Txt("Price", row, "0", 34, TextAlignmentOptions.MidlineRight);
            var s = item.side.rectTransform; s.anchorMin = s.anchorMax = s.pivot = new Vector2(1, 0.5f); s.anchoredPosition = new Vector2(-214, 0); s.sizeDelta = new Vector2(170, 60);
            item.action = Btn("Buy", row, Orange, "Buy", 32, Vector2.zero, new Vector2(180, 80), out item.actionLabel);
            var a = (RectTransform)item.action.transform; a.anchorMin = a.anchorMax = a.pivot = new Vector2(1, 0.5f); a.anchoredPosition = new Vector2(-16, 0);
            return item;
        }

        // ---------- Terminal prefabs ----------

        //A desk and a glowing screen in the terminal's colour, a trigger in front, and a floating prompt button
        static void TerminalPrefab<T>(string name, string path, string label, Color color) where T : LocationTerminal
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Prefabs", "Company");
            var root = new GameObject(name);
            try
            {
                var trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true; trigger.center = new Vector3(0, 1f, 0.8f); trigger.size = new Vector3(2.6f, 2f, 2.4f);
                var urp = Shader.Find("Universal Render Pipeline/Lit");
                var body = Mat(PrefabFolder + "/CrewTerminal_Body.mat", urp, new Color(0.18f, 0.22f, 0.27f), Color.black);
                var glow = Mat(PrefabFolder + "/" + name + "_Screen.mat", urp, color, color * 1.6f);

                var desk = GameObject.CreatePrimitive(PrimitiveType.Cube); desk.name = "Desk";
                desk.transform.SetParent(root.transform, false);
                desk.transform.localPosition = new Vector3(0, 0.5f, 0); desk.transform.localScale = new Vector3(1.4f, 1f, 0.7f);
                desk.GetComponent<Renderer>().sharedMaterial = body;
                var screen = GameObject.CreatePrimitive(PrimitiveType.Cube); screen.name = "Screen";
                screen.transform.SetParent(root.transform, false);
                screen.transform.localPosition = new Vector3(0, 1.35f, -0.1f); screen.transform.localRotation = Quaternion.Euler(-15f, 0, 0);
                screen.transform.localScale = new Vector3(1.2f, 0.7f, 0.06f);
                screen.GetComponent<Renderer>().sharedMaterial = glow;

                var canvasGo = new GameObject("Prompt", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(root.transform, false);
                canvasGo.transform.localPosition = new Vector3(0, 2.5f, 0); canvasGo.transform.localScale = Vector3.one * 0.01f;
                var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(320, 104);
                var button = Btn("Open", (RectTransform)canvasGo.transform, color, label, 54, Vector2.zero, new Vector2(320, 104), out var l);
                Stretch((RectTransform)button.transform);
                l.fontStyle = FontStyles.Bold;

                var terminal = root.AddComponent<T>();
                var so = new SerializedObject(terminal);
                so.FindProperty("prompt").objectReferenceValue = canvas;
                so.FindProperty("openButton").objectReferenceValue = button;
                so.FindProperty("label").stringValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // =====================================================================
        // Starter content
        // =====================================================================

        //Two ships (Shearwater, Fastnet), twelve ship parts (two per slot) and four businesses.
        //Existing assets are left alone.
        [MenuItem("Tools/Company/Create Fleet Content")]
        static void CreateFleetContent()
        {
            string root = CompanyEditor.Root;
            CompanyEditor.Folder(root + "/Ships"); CompanyEditor.Folder(root + "/Ship Parts"); CompanyEditor.Folder(root + "/Businesses");

            Ship("Shearwater", "Light Titan cargo hauler with a rear ramp. Short hops, a small crew, cheap to run.", "Assets/Shearwater/Shearwater.prefab",
                8000, 2, 1, 400, 300, 20f, 2, new Vector3(90, 0, 0));
            Ship("Fastnet", "Big ore freighter: six fans, three hoppers and room for the whole squad. Long, well-paid runs.", "Assets/Fastnet/Fastnet.prefab",
                22000, 4, 2, 900, 900, 45f, 3, Vector3.zero);

            Part("Tuned Thrusters", "Re-jetted fans for a little more reach.", ShipSlot.Engine, 1500, range: 1, cargoTime: -10);
            Part("Long-haul Drive", "A bigger core and deeper tanks.", ShipSlot.Engine, 3500, range: 2, cargoTime: -25);
            Part("Plated Hull", "Bolt-on plates over the weak spots.", ShipSlot.Hull, 1200, hull: 150);
            Part("Ablative Armour", "Layers that burn off instead of the hull.", ShipSlot.Hull, 3000, hull: 350);
            Part("Cargo Racks", "Better packing, more paying freight.", ShipSlot.Cargo, 1000, cargoPay: 20);
            Part("Smuggler's Hold", "Hidden space for cargo that pays extra.", ShipSlot.Cargo, 2800, cargoPay: 45);
            Part("Jump Seats", "Fold-down seats in the hold.", ShipSlot.Bay, 1200, seats: 1);
            Part("Drop Bay", "A rapid-exit bay: one more seat and a quick pickup.", ShipSlot.Bay, 3200, seats: 1, ability: SupportAbility.Extraction);
            Part("Rocket Pod", "A strike on call from the map.", ShipSlot.Weapons, 2600, ability: SupportAbility.Strike);
            Part("Point Defence", "Turrets that shoot down what's shot at you.", ShipSlot.Weapons, 1600, hull: 100);
            Part("Scanner Suite", "Sweeps an area and shows who's there.", ShipSlot.Utility, 1800, ability: SupportAbility.Scan);
            Part("Supply Drone Rack", "Drops ammo and med kits to the squad.", ShipSlot.Utility, 2200, ability: SupportAbility.Resupply);

            Business("Dockside Bar", "A noisy bar by the landing pads. Steady takings from pilots and dockers.", 3000, 40, 60, 8f, 1000);
            Business("Repair Shop", "Patches up other people's ships for a fee.", 5000, 70, 90, 8f, 1800);
            Business("Ore Refinery", "Turns the mine's ore into something worth shipping.", 9000, 120, 150, 10f, 3000);
            Business("Security Firm", "Guards for hire. Pays well every time you're out on a job.", 12000, 200, 120, 12f, 4000);

            //Ship battle numbers (power pips, firepower), also filled in on assets made before they existed
            PowerAndFire("Ships/Shearwater", "baseStats", 5, 40);
            PowerAndFire("Ships/Fastnet", "baseStats", 7, 60);
            PowerAndFire("Ship Parts/Tuned Thrusters", "stats", 1, 0);
            PowerAndFire("Ship Parts/Long-haul Drive", "stats", 2, 0);
            PowerAndFire("Ship Parts/Rocket Pod", "stats", 0, 30);
            PowerAndFire("Ship Parts/Point Defence", "stats", 0, 10);

            AssetDatabase.SaveAssets();
            Debug.Log("[Company] Fleet content is in " + root + " (Ships, Ship Parts, Businesses).");
        }

        static void PowerAndFire(string asset, string field, int power, int firepower)
        {
            var a = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CompanyEditor.Root + "/" + asset + ".asset");
            if (a == null) return;
            var so = new SerializedObject(a);
            var st = so.FindProperty(field);
            if (st.FindPropertyRelative("power").intValue != 0 || st.FindPropertyRelative("firepower").intValue != 0) return;
            st.FindPropertyRelative("power").intValue = power;
            st.FindPropertyRelative("firepower").intValue = firepower;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a);
        }

        static void Ship(string name, string description, string prefabPath, int price, int seats, int range, int hull, int cargoPay, float cargoMinutes, int repairPerPoint, Vector3 modelRotation)
        {
            var ship = CompanyEditor.Asset<ShipDefinition>(CompanyEditor.Root + "/Ships/" + name + ".asset", so =>
            {
                CompanyEditor.Common(so, name, description, null, price);
                so.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var s = so.FindProperty("baseStats");
                s.FindPropertyRelative("crewSeats").intValue = seats;
                s.FindPropertyRelative("range").intValue = range;
                s.FindPropertyRelative("hull").intValue = hull;
                so.FindProperty("cargoRunPay").intValue = cargoPay;
                so.FindProperty("cargoRunMinutes").floatValue = cargoMinutes;
                so.FindProperty("repairCostPerPoint").intValue = repairPerPoint;
            });
            //Stands Z-up exports upright (also set on assets made before this field existed)
            var sso = new SerializedObject(ship);
            sso.FindProperty("modelRotation").vector3Value = modelRotation;
            sso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ship);
        }

        static void Part(string name, string description, ShipSlot slot, int price, int seats = 0, int range = 0, int hull = 0, float cargoPay = 0, float cargoTime = 0, SupportAbility ability = SupportAbility.None)
        {
            CompanyEditor.Asset<ShipPartDefinition>(CompanyEditor.Root + "/Ship Parts/" + name + ".asset", so =>
            {
                CompanyEditor.Common(so, name, description, null, price);
                so.FindProperty("slot").enumValueIndex = (int)slot;
                so.FindProperty("ability").enumValueIndex = (int)ability;
                var s = so.FindProperty("stats");
                s.FindPropertyRelative("crewSeats").intValue = seats;
                s.FindPropertyRelative("range").intValue = range;
                s.FindPropertyRelative("hull").intValue = hull;
                s.FindPropertyRelative("cargoPay").floatValue = cargoPay;
                s.FindPropertyRelative("cargoTime").floatValue = cargoTime;
            });
        }

        static void Business(string name, string description, int price, int perMission, int perHour, float storeHours, int upgradeCost)
        {
            CompanyEditor.Asset<BusinessDefinition>(CompanyEditor.Root + "/Businesses/" + name + ".asset", so =>
            {
                CompanyEditor.Common(so, name, description, null, price);
                so.FindProperty("incomePerMission").intValue = perMission;
                so.FindProperty("incomePerHour").intValue = perHour;
                so.FindProperty("storeHours").floatValue = storeHours;
                so.FindProperty("upgradeCost").intValue = upgradeCost;
            });
        }

        // =====================================================================
        // The Shipyard scene
        // =====================================================================

        //A landing field with the Shearwater and Fastnet parked, a Shipyard office, a Broker office and a Home
        //Terminal. The player rig (PlayerCore, cameras, Rambler) is set up the way the Sandbox scene has it: the
        //Sandbox is opened alongside (not saved) and its scene links are copied across.
        [MenuItem("Tools/Company/Build Shipyard Scene")]
        static void BuildSceneMenu() => BuildScene(true);

        static void BuildScene(bool ask)
        {
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) { Debug.LogWarning("[Company] Close the Shipyard scene (open another) before rebuilding it."); return; }
            if (ask && System.IO.File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Shipyard scene", ScenePath + " exists. Rebuild it from scratch?", "Rebuild", "Cancel")) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShipyardTerminalPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(BrokerTerminalPath) == null) BuildScreens();

            var template = SceneManager.GetSceneByPath(TemplateScenePath);
            bool openedTemplate = false;
            if (!template.isLoaded) { template = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive); openedTemplate = true; }
            var previousActive = SceneManager.GetActiveScene();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Fill(scene, template);
                if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder("Assets/_Scenes", "Shipyard");
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[Company] Shipyard scene saved to " + ScenePath + ". Open it and press Play.");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded && previousActive != scene) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
                if (openedTemplate) EditorSceneManager.CloseScene(template, true);
            }
        }

        static void Fill(Scene scene, Scene template)
        {
            //Lighting like the Sandbox
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.22f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, 330f, 0f);
            RenderSettings.sun = sun;

            //Ground, pads, offices
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            string matFolder = SceneFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder("Assets/_Scenes", "Shipyard");
            if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(SceneFolder, "Materials");
            var ground = Mat(matFolder + "/SY_Ground.mat", urp, Hex("6E6458FF"), Color.black);
            var pad = Mat(matFolder + "/SY_Pad.mat", urp, Hex("3A3F45FF"), Color.black);
            var stripe = Mat(matFolder + "/SY_Stripe.mat", urp, Hex("E8B030FF"), Color.black);
            var wall = Mat(matFolder + "/SY_Wall.mat", urp, Hex("8C949CFF"), Color.black);
            var roof = Mat(matFolder + "/SY_Roof.mat", urp, Hex("4A5260FF"), Color.black);
            var blueSign = Mat(matFolder + "/SY_SignBlue.mat", urp, Blue, Blue * 1.4f);
            var greenSign = Mat(matFolder + "/SY_SignGreen.mat", urp, Green, Green * 1.4f);
            var orangeSign = Mat(matFolder + "/SY_SignOrange.mat", urp, Orange, Orange * 1.4f);

            var env = new GameObject("SY_Environment");
            Box("Ground", env.transform, new Vector3(0, -0.5f, 10), new Vector3(150, 1, 130), ground);
            //Pad A: Shearwater. Pad B: Fastnet.
            PadWithMarkings("PadA", env.transform, new Vector3(-22, 0, 30), new Vector2(26, 28), pad, stripe);
            PadWithMarkings("PadB", env.transform, new Vector3(28, 0, 34), new Vector2(34, 46), pad, stripe);
            //Offices along the south edge of the apron, doors facing north
            var shipOffice = Office("Shipyard Office", env.transform, new Vector3(-16, 0, -6), wall, roof, blueSign);
            var brokerOffice = Office("Broker Office", env.transform, new Vector3(6, 0, -6), wall, roof, greenSign);
            var homeOffice = Office("Company Office", env.transform, new Vector3(26, 0, -6), wall, roof, orangeSign);
            //A few crates for cover and scale
            for (int i = 0; i < 6; i++)
                Box("Crate " + i, env.transform, new Vector3(-40 + i * 2.2f, 0.6f, 8 + (i % 2) * 1.6f), new Vector3(1.6f, 1.2f, 1.4f), roof);

            //Terminals in front of each office, facing the apron (their trigger is on the +Z side)
            Place(ShipyardTerminalPath, new Vector3(shipOffice.x, 0, shipOffice.z + 4.2f), env.transform);
            Place(BrokerTerminalPath, new Vector3(brokerOffice.x, 0, brokerOffice.z + 4.2f), env.transform);
            Place(HomeTerminalPath, new Vector3(homeOffice.x, 0, homeOffice.z + 4.2f), env.transform);

            //Parked ships (scenery)
            var shearwater = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shearwater/Shearwater.prefab");
            if (shearwater != null)
            {
                var sw = (GameObject)PrefabUtility.InstantiatePrefab(shearwater, scene);
                sw.name = "Shearwater_Parked";
                sw.transform.SetParent(env.transform, true);
                sw.transform.position = new Vector3(-22, 0.3f, 30);
                sw.transform.rotation = Quaternion.Euler(0, 200, 0) * Quaternion.Euler(90, 0, 0) * shearwater.transform.rotation; //Exported Z-up
                SitOnPad(sw, 0.3f);
            }
            var fastnet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fastnet/Fastnet.prefab");
            if (fastnet != null)
            {
                var fn = (GameObject)PrefabUtility.InstantiatePrefab(fastnet, scene);
                fn.name = "Fastnet_Parked";
                fn.transform.position = new Vector3(28, 0.3f, 34);
                fn.transform.rotation = Quaternion.Euler(0, 160, 0) * fastnet.transform.rotation;
                SitOnPad(fn, 0.3f);
            }

            //Player rig, linked the way the Sandbox links it
            var coreSrc = Root(template, "PlayerCore");
            var camsSrc = Root(template, "----Cameras---- Sandbox");
            var playerSrc = Root(template, "Rambler");
            var map = new Dictionary<GameObject, GameObject>();
            var core = Copy(coreSrc, scene, "PlayerCore", map);
            var cams = Copy(camsSrc, scene, "----Cameras---- Shipyard", map);
            var player = Copy(playerSrc, scene, "Rambler", map);
            foreach (var pair in new[] { (coreSrc, core), (camsSrc, cams), (playerSrc, player) })
                CopyLinks(pair.Item1, pair.Item2, map);
            PlaceInstance(player, new Vector3(6, 0.05f, 6), Quaternion.Euler(0, 180, 0));
            if (core != null) core.transform.position = Vector3.zero;
            if (cams != null) cams.transform.position = Vector3.zero;
            TrimPlayers(core);

            //Hired crew can be deployed from the Home Terminal next to the player
            var deployer = new GameObject("SquadDeployer").AddComponent<SquadDeployer>();
            deployer.transform.position = new Vector3(8, 0, 6);
            var dso = new SerializedObject(deployer);
            dso.FindProperty("deployOnStart").boolValue = false;
            dso.ApplyModifiedPropertiesWithoutUndo();

            //NavMesh over the apron
            var surface = env.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
            surface.BuildNavMesh();
            var navFolder = SceneFolder + "/Shipyard";
            if (!AssetDatabase.IsValidFolder(navFolder)) AssetDatabase.CreateFolder(SceneFolder, "Shipyard");
            if (surface.navMeshData != null)
            {
                //CreateAsset replaces the previous bake at this path
                AssetDatabase.CreateAsset(surface.navMeshData, navFolder + "/NavMesh-SY_Environment.asset");
            }
        }

        // ---------- Scene helpers ----------

        internal static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.isStatic = true;
            return go;
        }

        internal static void PadWithMarkings(string name, Transform parent, Vector3 centre, Vector2 size, Material pad, Material stripe)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = centre;
            Box("Deck", p, new Vector3(0, 0.15f, 0), new Vector3(size.x, 0.3f, size.y), pad);
            float t = 0.5f, y = 0.31f;
            Box("EdgeN", p, new Vector3(0, y, size.y / 2 - t), new Vector3(size.x - 1, 0.02f, t), stripe);
            Box("EdgeS", p, new Vector3(0, y, -size.y / 2 + t), new Vector3(size.x - 1, 0.02f, t), stripe);
            Box("EdgeE", p, new Vector3(size.x / 2 - t, y, 0), new Vector3(t, 0.02f, size.y - 1), stripe);
            Box("EdgeW", p, new Vector3(-size.x / 2 + t, y, 0), new Vector3(t, 0.02f, size.y - 1), stripe);
            Box("Cross1", p, new Vector3(0, y, 0), new Vector3(size.x * 0.5f, 0.02f, 0.8f), stripe);
            Box("Cross2", p, new Vector3(0, y, 0), new Vector3(0.8f, 0.02f, size.y * 0.5f), stripe);
        }

        //A box office with a sign strip over the door; returns its position
        static Vector3 Office(string name, Transform parent, Vector3 pos, Material wall, Material roof, Material sign)
        {
            var o = new GameObject(name).transform;
            o.SetParent(parent, false);
            o.localPosition = pos;
            Box("Walls", o, new Vector3(0, 2f, 0), new Vector3(10, 4, 6), wall);
            Box("Roof", o, new Vector3(0, 4.15f, 0.3f), new Vector3(10.6f, 0.3f, 6.8f), roof);
            Box("Sign", o, new Vector3(0, 3.4f, 3.02f), new Vector3(6, 0.7f, 0.08f), sign);
            Box("Door", o, new Vector3(0, 1.2f, 3.01f), new Vector3(1.6f, 2.4f, 0.06f), roof);
            return pos;
        }

        internal static void Place(string prefabPath, Vector3 pos, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning("[Company] Missing " + prefabPath); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;
        }

        //Drops a parked ship so its lowest point rests on the deck
        internal static void SitOnPad(GameObject ship, float deckY)
        {
            var rs = ship.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            ship.transform.position += Vector3.up * (deckY - b.min.y);
        }

        internal static GameObject Root(Scene s, string name)
        {
            foreach (var r in s.GetRootGameObjects()) if (r.name == name) return r;
            Debug.LogWarning("[Company] " + s.name + " has no " + name);
            return null;
        }

        //A fresh instance of the same prefab, with the same overrides (scene links are fixed up after)
        internal static GameObject Copy(GameObject source, Scene scene, string name, Dictionary<GameObject, GameObject> map)
        {
            if (source == null) return null;
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(source);
            if (prefab == null) { Debug.LogWarning("[Company] " + source.name + " isn't a prefab instance"); return null; }
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            copy.name = name;
            map[source] = copy;
            return copy;
        }

        //Applies the source instance's overrides to the copy, pointing scene links at the matching new objects.
        //Links to things that aren't in the new scene (Hayes, Pervis...) are dropped.
        internal static void CopyLinks(GameObject source, GameObject copy, Dictionary<GameObject, GameObject> map)
        {
            if (source == null || copy == null) return;
            var mods = new List<PropertyModification>();
            var rootTransform = PrefabUtility.GetCorrespondingObjectFromSource(source.transform);
            foreach (var m in PrefabUtility.GetPropertyModifications(source))
            {
                //Keep the copy's own name and placement
                if (m.propertyPath == "m_Name") continue;
                if (m.target == rootTransform && (m.propertyPath.StartsWith("m_LocalPosition") || m.propertyPath.StartsWith("m_LocalRotation") || m.propertyPath.StartsWith("m_LocalEulerAngles") || m.propertyPath.StartsWith("m_AnchoredPosition"))) continue;
                if (m.objectReference != null && !EditorUtility.IsPersistent(m.objectReference))
                {
                    var mapped = MapObject(m.objectReference, map);
                    if (mapped == null) continue;
                    m.objectReference = mapped;
                }
                mods.Add(m);
            }
            string name = copy.name;
            PrefabUtility.SetPropertyModifications(copy, mods.ToArray());
            copy.name = name;
        }

        static Object MapObject(Object o, Dictionary<GameObject, GameObject> map)
        {
            GameObject go = o as GameObject ?? (o as Component)?.gameObject;
            if (go == null) return null;
            var root = go.transform.root.gameObject;
            if (!map.TryGetValue(root, out var newRoot)) return null;
            string path = PathFrom(root.transform, go.transform);
            var t = path.Length == 0 ? newRoot.transform : newRoot.transform.Find(path);
            if (t == null) return null;
            if (o is GameObject) return t.gameObject;
            return t.GetComponent(o.GetType());
        }

        static string PathFrom(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != root && c != null; c = c.parent) parts.Insert(0, c.name);
            return string.Join("/", parts);
        }

        //Moves a prefab instance and records it as an override (Rambler's root is a RectTransform, whose anchored
        //position would otherwise be put back to the prefab's)
        internal static void PlaceInstance(GameObject go, Vector3 position, Quaternion rotation)
        {
            if (go == null) return;
            if (go.transform is RectTransform rt) rt.anchoredPosition3D = position;
            go.transform.SetPositionAndRotation(position, rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }

        //The Sandbox lists Hayes and Pervis as players too; here it's just Rambler (crew are deployed at runtime)
        internal static void TrimPlayers(GameObject core)
        {
            if (core == null) return;
            var selector = core.GetComponent<Rambler.Control.PlayerSelector>();
            if (selector == null) return;
            var so = new SerializedObject(selector);
            var players = so.FindProperty("players");
            if (players == null || !players.isArray) return;
            for (int i = players.arraySize - 1; i >= 0; i--)
                if (players.GetArrayElementAtIndex(i).objectReferenceValue == null) players.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
