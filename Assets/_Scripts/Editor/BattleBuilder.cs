using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Rambler.Crew;
using Rambler.Crew.Battle;
using Rambler.Ship;
using static Rambler.EditorTools.CrewScreenBuilder;

namespace Rambler.EditorTools
{
    //Ship battle tools: the battle bar and station screen (built into the HUD in PlayerCore.prefab), the four station
    //consoles and the Launch terminal, and the ShipBattle scene set inside the Shearwater. All safe to run again.
    public static class BattleBuilder
    {
        const string PlayerCorePath = "Assets/_Prefabs/PlayerCore/PlayerCore.prefab";
        const string PrefabFolder = "Assets/_Prefabs/Company";
        const string LaunchPath = PrefabFolder + "/LaunchTerminal.prefab";
        const string SceneFolder = "Assets/_Scenes/ShipBattle";
        const string ScenePath = SceneFolder + "/ShipBattle.unity";
        const string ShipyardScenePath = "Assets/_Scenes/Shipyard/Shipyard.unity";
        const string TemplateScenePath = "Assets/Sandbox/Sandbox.unity";
        const string FastnetPath = "Assets/Fastnet/Fastnet.prefab";
        static readonly string[] BoarderPaths = { "Assets/_Prefabs/Characters/Miners/Miner1.prefab", "Assets/_Prefabs/Characters/Humans/Security/AtlasMK3.prefab" };

        static string StationPath(Station s) => PrefabFolder + "/Station_" + s + ".prefab";

        // =====================================================================
        // HUD pieces and prefabs
        // =====================================================================

        [MenuItem("Tools/Company/Build Ship Battle")]
        static void BuildAll()
        {
            Load();
            var root = PrefabUtility.LoadPrefabContents(PlayerCorePath);
            try
            {
                Transform hud = Find(root.transform, "HUD");
                if (hud == null) { Debug.LogError("[Company] No HUD in " + PlayerCorePath); return; }
                BuildBattleHUD(hud);
                BuildStationScreen(hud);
                BuildSystemsScreen(hud);
                TacticalBuilder.BuildWeaponsScreen(hud);
                TacticalBuilder.BuildNavigationScreen(hud);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerCorePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            foreach (Station s in System.Enum.GetValues(typeof(Station))) StationPrefab(s);
            LaunchPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[Company] Ship battle HUD and station screen built into " + PlayerCorePath + "; station consoles and Launch terminal in " + PrefabFolder + ".");
        }

        static RectTransform Holder(Transform hud, string name, int order)
        {
            var old = hud.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var holder = Rect(name, hud);
            Stretch(holder);
            var canvas = holder.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = order;
            holder.gameObject.AddComponent<GraphicRaycaster>();
            return holder;
        }

        static void BuildBattleHUD(Transform hud)
        {
            var holder = Holder(hud, "ShipBattleHUD", 30);
            var hudComp = holder.gameObject.AddComponent<ShipBattleHUD>();
            var so = new SerializedObject(hudComp);
            var root = Rect("Root", holder); Stretch(root);
            Set(so, "root", root.gameObject);

            var bar = Img("TopBar", root, new Color(0.04f, 0.06f, 0.09f, 0.88f));
            TopCenter(bar, new Vector2(0, -8), new Vector2(1240, 132));
            bar.raycastTarget = false;
            var ship = Txt("Ship", bar.rectTransform, "SHIP", 28, TextAlignmentOptions.MidlineLeft);
            TopLeft(ship, new Vector2(20, -6), new Vector2(580, 40)); ship.fontStyle = FontStyles.Bold;
            Set(so, "shipText", ship);
            Set(so, "hullBar", FillBar(bar.rectTransform, new Vector2(20, -50), new Vector2(580, 26), Hex("59CC73FF"), false));
            var enemy = Txt("Enemy", bar.rectTransform, "ENEMY", 28, TextAlignmentOptions.MidlineRight);
            TopRight(enemy, new Vector2(-20, -6), new Vector2(580, 40)); enemy.fontStyle = FontStyles.Bold;
            Set(so, "enemyText", enemy);
            Set(so, "enemyHullBar", FillBar(bar.rectTransform, new Vector2(-20, -50), new Vector2(580, 26), Red, true));
            var timers = Txt("Timers", bar.rectTransform, "", 26, TextAlignmentOptions.Center);
            TopCenter(timers, new Vector2(0, -84), new Vector2(1200, 40));
            Set(so, "timersText", timers);

            var chips = new Image[4]; var chipTexts = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                chips[i] = Img("Station" + i, root, TileCol);
                TopCenter(chips[i], new Vector2(-453 + i * 302, -150), new Vector2(290, 78));
                chips[i].raycastTarget = false;
                chipTexts[i] = Txt("Text", chips[i].rectTransform, "", 24, TextAlignmentOptions.Center);
                Stretch(chipTexts[i].rectTransform);
                chipTexts[i].lineSpacing = -10f; chipTexts[i].enableAutoSizing = true; chipTexts[i].fontSizeMax = 24; chipTexts[i].fontSizeMin = 16;
            }
            SetArray(so, "stationChips", chips);
            SetArray(so, "stationTexts", chipTexts);

            var logBack = Img("LogBack", root, new Color(0, 0, 0, 0.55f));
            TopCenter(logBack, new Vector2(0, -238), new Vector2(1240, 46));
            logBack.raycastTarget = false;
            var log = Txt("Log", logBack.rectTransform, "", 26, TextAlignmentOptions.Center);
            Stretch(log.rectTransform); log.color = Orange; log.enableAutoSizing = true; log.fontSizeMax = 26; log.fontSizeMin = 18;
            Set(so, "logText", log);

            var end = Img("EndPanel", root, WindowCol);
            Center(end, new Vector2(900, 540));
            Set(so, "endPanel", end.gameObject);
            var title = Txt("Title", end.rectTransform, "VICTORY", 72, TextAlignmentOptions.Center);
            TopCenter(title, new Vector2(0, -30), new Vector2(840, 100)); title.fontStyle = FontStyles.Bold;
            Set(so, "endTitle", title);
            var body = Txt("Body", end.rectTransform, "", 34, TextAlignmentOptions.Top);
            TopCenter(body, new Vector2(0, -150), new Vector2(820, 220));
            Set(so, "endBody", body);
            var cont = Btn("Continue", end.rectTransform, Orange, "Continue", 40, Vector2.zero, new Vector2(380, 104), out _);
            BottomCenter((RectTransform)cont.transform, new Vector2(0, 36), new Vector2(380, 104));
            Set(so, "continueButton", cont);
            end.gameObject.SetActive(false);

            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
        }

        static void BuildStationScreen(Transform hud)
        {
            var holder = Holder(hud, "StationScreen", 48);
            var screen = holder.gameObject.AddComponent<StationScreen>();
            var so = new SerializedObject(screen);

            var window = Img("Window", holder, WindowCol).rectTransform;
            Place(window, new Vector2(1, 0.5f), new Vector2(-30, -40), new Vector2(1040, 760));
            Set(so, "window", window.gameObject);
            var titleBar = Img("TitleBar", window, Blue);
            var tb = titleBar.rectTransform; tb.anchorMin = new Vector2(0, 1); tb.anchorMax = new Vector2(1, 1); tb.pivot = new Vector2(0.5f, 1); tb.anchoredPosition = Vector2.zero; tb.sizeDelta = new Vector2(0, 88);
            Set(so, "titleBar", titleBar);
            var title = Txt("Title", tb, "STATION", 48, TextAlignmentOptions.MidlineLeft);
            TopLeft(title, new Vector2(28, 0), new Vector2(700, 88)); title.fontStyle = FontStyles.Bold;
            Set(so, "titleText", title);
            var close = Img("Close", window, Color.white, circle);
            TopRight(close, new Vector2(-12, -4), new Vector2(80, 80));
            var cl = Txt("Label", close.rectTransform, "X", 40, TextAlignmentOptions.Center); Stretch(cl.rectTransform); cl.color = Color.black;
            Set(so, "closeButton", close.gameObject.AddComponent<Button>());
            var op = Txt("Operator", window, "", 30, TextAlignmentOptions.MidlineLeft);
            TopLeft(op, new Vector2(28, -100), new Vector2(984, 44)); op.enableAutoSizing = true; op.fontSizeMax = 30; op.fontSizeMin = 22;
            Set(so, "operatorText", op);
            var status = Txt("Status", window, "", 28, TextAlignmentOptions.MidlineLeft);
            TopLeft(status, new Vector2(28, -146), new Vector2(984, 40)); status.color = Dim;
            Set(so, "statusText", status);

            //Navigation
            var nav = Page(window, "NavigationPage"); Set(so, "navPage", nav.gameObject);
            Set(so, "navText", Text(nav, "", 32, new Vector2(0, 0), new Vector2(984, 110)));
            Heading(nav, "ESCAPE JUMP", new Vector2(0, -122));
            Set(so, "escapeBar", FillBar(nav, new Vector2(0, -158), new Vector2(984, 40), Blue, false));
            Set(so, "escapeText", Text(nav, "", 30, new Vector2(0, -206), new Vector2(984, 44)));
            var esc = Btn("Escape", nav, Blue, "Plot escape", 38, new Vector2(0, -270), new Vector2(440, 104), out var escLabel);
            Set(so, "escapeButton", esc); Set(so, "escapeLabel", escLabel);
            nav.gameObject.SetActive(false);

            //Weapons
            var wpn = Page(window, "WeaponsPage"); Set(so, "weaponsPage", wpn.gameObject);
            Heading(wpn, "CHARGE", new Vector2(0, 0));
            Set(so, "chargeBar", FillBar(wpn, new Vector2(0, -36), new Vector2(984, 40), Red, false));
            Set(so, "weaponText", Text(wpn, "", 30, new Vector2(0, -88), new Vector2(984, 90)));
            Heading(wpn, "AIM AT", new Vector2(0, -186));
            var targets = new Button[3];
            string[] names = { "Hull", "Weapons", "Engines" };
            for (int i = 0; i < 3; i++) targets[i] = Btn("Target" + names[i], wpn, TileCol, names[i], 36, new Vector2(i * 337, -222), new Vector2(310, 92), out _);
            SetArray(so, "targetButtons", targets);
            Set(so, "enemyWeaponsBar", FillBar(wpn, new Vector2(0, -338), new Vector2(480, 24), Orange, false));
            Set(so, "enemyEnginesBar", FillBar(wpn, new Vector2(504, -338), new Vector2(480, 24), Orange, false));
            var es = Text(wpn, "", 24, new Vector2(0, -370), new Vector2(984, 86)); es.color = Dim;
            Set(so, "enemySystemsText", es);
            wpn.gameObject.SetActive(false);

            //Engines
            var eng = Page(window, "EnginesPage"); Set(so, "enginesPage", eng.gameObject);
            Set(so, "powerText", Text(eng, "", 32, new Vector2(0, 0), new Vector2(984, 46)));
            //Navigation, Weapons, Systems, Shields
            var labels = new TextMeshProUGUI[4]; var pips = new Image[16]; var minus = new Button[4]; var plus = new Button[4];
            for (int r = 0; r < 4; r++)
            {
                float y = -58 - r * 76;
                labels[r] = Text(eng, "Row", 30, new Vector2(0, y), new Vector2(270, 68));
                labels[r].alignment = TextAlignmentOptions.MidlineLeft; labels[r].enableAutoSizing = true; labels[r].fontSizeMax = 30; labels[r].fontSizeMin = 18;
                for (int k = 0; k < 4; k++)
                {
                    pips[r * 4 + k] = Img("Pip" + r + k, eng, Hex("1F2B3AFF"));
                    TopLeft(pips[r * 4 + k], new Vector2(286 + k * 100, y - 9), new Vector2(90, 50));
                }
                minus[r] = Btn("Minus" + r, eng, TileCol, "−", 48, new Vector2(700, y), new Vector2(120, 68), out _);
                plus[r] = Btn("Plus" + r, eng, Orange, "+", 48, new Vector2(836, y), new Vector2(120, 68), out _);
            }
            SetArray(so, "powerLabels", labels); SetArray(so, "pipImages", pips);
            SetArray(so, "minusButtons", minus); SetArray(so, "plusButtons", plus);
            Set(so, "repairText", Text(eng, "", 28, new Vector2(0, -362), new Vector2(984, 44)));
            var repairs = new Button[4];
            for (int i = 0; i < 4; i++) repairs[i] = Btn("Repair" + i, eng, TileCol, ((Station)i).ToString(), 30, new Vector2(i * 246, -408), new Vector2(232, 80), out _);
            SetArray(so, "repairButtons", repairs);
            Set(so, "repairBar", FillBar(eng, new Vector2(0, -500), new Vector2(984, 26), Orange, false));
            eng.gameObject.SetActive(false);

            so.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);
        }

        //Ship Systems: the live top-down view of the ship (left), and the selected room's panel (right)
        static void BuildSystemsScreen(Transform hud)
        {
            var holder = Holder(hud, "SystemsScreen", 49);
            var screen = holder.gameObject.AddComponent<SystemsScreen>();
            var so = new SerializedObject(screen);

            var window = Img("Window", holder, WindowCol).rectTransform;
            Stretch(window);
            Set(so, "window", window.gameObject);

            var header = Img("Header", window, Hex("1A6B3AFF")).rectTransform;
            header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
            header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(0, 96);
            var title = Txt("Status", header, "SHIP SYSTEMS", 32, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.anchorMin = Vector2.zero; title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(28, 0); title.rectTransform.offsetMax = new Vector2(-120, 0);
            title.enableAutoSizing = true; title.fontSizeMax = 32; title.fontSizeMin = 22;
            Set(so, "headerText", title);
            var close = Img("Close", header, Color.white, circle);
            TopRight(close, new Vector2(-12, -8), new Vector2(80, 80));
            var cl = Txt("Label", close.rectTransform, "X", 40, TextAlignmentOptions.Center); Stretch(cl.rectTransform); cl.color = Color.black;
            Set(so, "closeButton", close.gameObject.AddComponent<Button>());

            //The camera view
            var viewGo = new GameObject("View", typeof(RectTransform), typeof(RawImage));
            var viewRect = (RectTransform)viewGo.transform; viewRect.SetParent(window, false);
            viewRect.anchorMin = Vector2.zero; viewRect.anchorMax = Vector2.one; viewRect.offsetMin = new Vector2(16, 16); viewRect.offsetMax = new Vector2(-576, -108);
            var raw = viewGo.GetComponent<RawImage>(); raw.color = Color.white; raw.raycastTarget = false;
            Set(so, "view", raw);
            var zones = Rect("Zones", viewRect); Stretch(zones);
            Set(so, "zoneLayer", zones);

            //A room's tap zone
            var zone = Img("ZoneTemplate", zones, new Color(0.3f, 0.6f, 0.9f, 0.06f));
            Stretch(zone.rectTransform);
            var item = zone.gameObject.AddComponent<SystemsRoomZone>();
            item.fill = zone;
            item.frame = zone.gameObject.AddComponent<Outline>();
            item.button = zone.gameObject.AddComponent<Button>(); item.button.transition = Selectable.Transition.None;
            item.nameText = Txt("Name", zone.rectTransform, "ROOM", 24, TextAlignmentOptions.TopLeft);
            Stretch(item.nameText.rectTransform); item.nameText.rectTransform.offsetMin = new Vector2(8, 6); item.nameText.rectTransform.offsetMax = new Vector2(-8, -6);
            item.nameText.fontStyle = FontStyles.Bold; item.nameText.color = new Color(1, 1, 1, 0.85f); item.nameText.raycastTarget = false;
            item.nameText.enableAutoSizing = true; item.nameText.fontSizeMax = 24; item.nameText.fontSizeMin = 16;
            item.alertText = Txt("Alerts", zone.rectTransform, "", 26, TextAlignmentOptions.Center);
            Stretch(item.alertText.rectTransform); item.alertText.rectTransform.offsetMax = new Vector2(0, -26);
            item.alertText.fontStyle = FontStyles.Bold; item.alertText.raycastTarget = false;
            item.alertText.enableAutoSizing = true; item.alertText.fontSizeMax = 26; item.alertText.fontSizeMin = 16;
            Set(so, "zoneTemplate", item);

            //The selected room
            var panel = Img("RoomPanel", window, PanelCol).rectTransform;
            panel.anchorMin = new Vector2(1, 0); panel.anchorMax = new Vector2(1, 1); panel.pivot = new Vector2(1, 0.5f);
            panel.offsetMin = new Vector2(-560, 16); panel.offsetMax = new Vector2(-16, -108);
            var roomTitle = Txt("Room", panel, "Tap a room", 40, TextAlignmentOptions.MidlineLeft);
            TopLeft(roomTitle, new Vector2(24, -12), new Vector2(500, 60)); roomTitle.fontStyle = FontStyles.Bold;
            Set(so, "roomTitle", roomTitle);
            var info = Txt("Info", panel, "", 26, TextAlignmentOptions.TopLeft);
            info.rectTransform.anchorMin = new Vector2(0, 0); info.rectTransform.anchorMax = new Vector2(1, 1);
            info.rectTransform.offsetMin = new Vector2(24, 400); info.rectTransform.offsetMax = new Vector2(-24, -80);
            info.enableAutoSizing = true; info.fontSizeMax = 26; info.fontSizeMin = 18;
            Set(so, "roomInfo", info);
            var actions = Rect("Actions", panel);
            actions.anchorMin = new Vector2(0, 0); actions.anchorMax = new Vector2(1, 0); actions.pivot = new Vector2(0.5f, 0);
            actions.anchoredPosition = Vector2.zero; actions.sizeDelta = new Vector2(0, 400);
            Set(so, "sealButton", Btn("Seal", actions, TileCol, "SEAL DOORS", 30, new Vector2(24, -10), new Vector2(240, 90), out _));
            Set(so, "openDoorsButton", Btn("OpenDoors", actions, TileCol, "OPEN DOORS", 30, new Vector2(280, -10), new Vector2(240, 90), out _));
            Set(so, "ventButton", Btn("Vent", actions, Blue, "VENT AIR", 30, new Vector2(24, -112), new Vector2(240, 90), out _));
            Set(so, "restoreButton", Btn("Restore", actions, TileCol, "RESTORE AIR", 30, new Vector2(280, -112), new Vector2(240, 90), out _));
            Set(so, "sendCrewButton", Btn("SendCrew", actions, Orange, "SEND CREW", 32, new Vector2(24, -214), new Vector2(496, 90), out _));
            var act = Txt("Action", actions, "", 24, TextAlignmentOptions.TopLeft);
            TopLeft(act, new Vector2(24, -314), new Vector2(496, 76)); act.color = Orange;
            act.enableAutoSizing = true; act.fontSizeMax = 24; act.fontSizeMin = 18;
            Set(so, "actionText", act);

            so.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);
        }

        static RectTransform Page(RectTransform window, string name)
        {
            var p = Rect(name, window);
            p.anchorMin = Vector2.zero; p.anchorMax = Vector2.one; p.offsetMin = new Vector2(28, 24); p.offsetMax = new Vector2(-28, -200);
            return p;
        }

        static TextMeshProUGUI Text(RectTransform parent, string text, float size, Vector2 pos, Vector2 box)
        {
            var t = Txt("Text", parent, text, size, TextAlignmentOptions.TopLeft);
            TopLeft(t, pos, box);
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.7f;
            return t;
        }

        static void Heading(RectTransform parent, string text, Vector2 pos)
        {
            var h = Txt("Heading", parent, text, 24, TextAlignmentOptions.MidlineLeft);
            TopLeft(h, pos, new Vector2(600, 32)); h.color = Dim;
        }

        //A dark back with a filled bar in it; returns the fill. Right-anchored bars fill from the right.
        static Image FillBar(RectTransform parent, Vector2 pos, Vector2 size, Color color, bool fromRight)
        {
            var back = Img("BarBack", parent, new Color(0, 0, 0, 0.5f));
            if (fromRight) TopRight(back, pos, size); else TopLeft(back, pos, size);
            back.raycastTarget = false;
            var fill = Img("Fill", back.rectTransform, color);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = fromRight ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.6f; fill.raycastTarget = false;
            return fill;
        }

        static void SetArray<T>(SerializedObject so, string field, T[] values) where T : Object
        {
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---------- Consoles ----------

        //A compact console (fits a cockpit seat) with a glowing screen in the station's colour and a prompt button
        static void StationPrefab(Station s)
        {
            Console("Station_" + s, StationPath(s), s == Station.Systems ? "SYSTEMS" : s.ToString().ToUpperInvariant(), StationTerminal.ColorFor(s), root =>
            {
                var t = root.AddComponent<StationTerminal>();
                var so = new SerializedObject(t);
                so.FindProperty("station").enumValueIndex = (int)s;
                so.ApplyModifiedPropertiesWithoutUndo();
                return t;
            });
        }

        static void LaunchPrefab()
        {
            Console("LaunchTerminal", LaunchPath, "LAUNCH", Hex("E64D40FF"), root => root.AddComponent<LaunchTerminal>());
        }

        static void Console(string name, string path, string label, Color color, System.Func<GameObject, LocationTerminal> add)
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Prefabs", "Company");
            var root = new GameObject(name);
            try
            {
                var trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true; trigger.center = new Vector3(0, 1f, 0.65f); trigger.size = new Vector3(1.5f, 2f, 1.3f);
                var urp = Shader.Find("Universal Render Pipeline/Lit");
                var body = Mat(PrefabFolder + "/CrewTerminal_Body.mat", urp, new Color(0.18f, 0.22f, 0.27f), Color.black);
                var glow = Mat(PrefabFolder + "/" + name + "_Screen.mat", urp, color, color * 1.6f);
                var desk = GameObject.CreatePrimitive(PrimitiveType.Cube); desk.name = "Desk";
                desk.transform.SetParent(root.transform, false);
                desk.transform.localPosition = new Vector3(0, 0.45f, 0); desk.transform.localScale = new Vector3(0.8f, 0.9f, 0.45f);
                desk.GetComponent<Renderer>().sharedMaterial = body;
                Object.DestroyImmediate(desk.GetComponent<Collider>());
                var screen = GameObject.CreatePrimitive(PrimitiveType.Cube); screen.name = "Screen";
                screen.transform.SetParent(root.transform, false);
                screen.transform.localPosition = new Vector3(0, 1.1f, -0.05f); screen.transform.localRotation = Quaternion.Euler(-20f, 0, 0);
                screen.transform.localScale = new Vector3(0.7f, 0.42f, 0.05f);
                screen.GetComponent<Renderer>().sharedMaterial = glow;
                Object.DestroyImmediate(screen.GetComponent<Collider>());

                var canvasGo = new GameObject("Prompt", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(root.transform, false);
                canvasGo.transform.localPosition = new Vector3(0, 2.1f, 0); canvasGo.transform.localScale = Vector3.one * 0.01f;
                var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(300, 96);
                var button = Btn("Open", (RectTransform)canvasGo.transform, color, label, 48, Vector2.zero, new Vector2(300, 96), out var l);
                Stretch((RectTransform)button.transform);
                l.fontStyle = FontStyles.Bold;

                var terminal = add(root);
                var so = new SerializedObject(terminal);
                so.FindProperty("prompt").objectReferenceValue = canvas;
                so.FindProperty("openButton").objectReferenceValue = button;
                so.FindProperty("label").stringValue = label;
                so.FindProperty("promptOffset").vector3Value = new Vector3(0, 2.1f, 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // =====================================================================
        // The ShipBattle scene
        // =====================================================================

        [MenuItem("Tools/Company/Build Ship Battle Scene")]
        static void BuildSceneMenu() => BuildScene(true);

        //Ship coordinates: the Shearwater's floor is at y 0, centred on the origin, nose +z.
        static void BuildScene(bool ask)
        {
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) { Debug.LogWarning("[Company] Open another scene before rebuilding the ship battle."); return; }
            if (ask && System.IO.File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Ship battle scene", ScenePath + " exists. Rebuild it?", "Rebuild", "Cancel")) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StationPath(Station.Navigation)) == null) BuildAll();

            var template = SceneManager.GetSceneByPath(TemplateScenePath);
            bool openedTemplate = false;
            if (!template.isLoaded) { template = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive); openedTemplate = true; }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Fill(scene, template);
                if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder("Assets/_Scenes", "ShipBattle");
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuild(ShipyardScenePath);
                AddToBuild(ScenePath);
                Debug.Log("[Company] Ship battle scene saved to " + ScenePath + " (Shipyard and ShipBattle are in Build Settings).");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded && previous != scene) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
                if (openedTemplate) EditorSceneManager.CloseScene(template, true);
            }
        }

        static void Fill(Scene scene, Scene template)
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder("Assets/_Scenes", "ShipBattle");
            string matFolder = SceneFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(matFolder)) AssetDatabase.CreateFolder(SceneFolder, "Materials");
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            var space = Mat(matFolder + "/SB_Space.mat", urp, Hex("05070DFF"), Color.black);
            var star = Mat(matFolder + "/SB_Star.mat", urp, Color.white, Color.white * 2f);
            var planet = Mat(matFolder + "/SB_Planet.mat", urp, Hex("B07A4AFF"), Hex("2A1608FF"));

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.5f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.36f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.14f, 0.16f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, 320f, 0f);
            RenderSettings.sun = sun;

            //Space: a dark floor far below, scattered stars, a planet
            var backdrop = new GameObject("SB_Space").transform;
            var floor = FleetBuilder.Box("Void", backdrop, new Vector3(0, -60, 0), new Vector3(600, 1, 600), space);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var rng = new System.Random(7);
            for (int i = 0; i < 220; i++)
            {
                float s = 0.2f + (float)rng.NextDouble() * 0.5f;
                var st = FleetBuilder.Box("Star", backdrop, new Vector3((float)rng.NextDouble() * 400 - 200, -59f, (float)rng.NextDouble() * 400 - 200), Vector3.one * s, star);
                Object.DestroyImmediate(st.GetComponent<Collider>());
            }
            var p = GameObject.CreatePrimitive(PrimitiveType.Sphere); p.name = "Planet";
            p.transform.SetParent(backdrop, false); p.transform.localPosition = new Vector3(-70, -50, 60); p.transform.localScale = Vector3.one * 45f;
            p.GetComponent<Renderer>().sharedMaterial = planet; Object.DestroyImmediate(p.GetComponent<Collider>());

            //Your ship: the Shearwater interior (Tools > Company > Build Shearwater Interior), floor at y 0, centred on the origin
            var interior = AssetDatabase.LoadAssetAtPath<GameObject>(ShearwaterBuilder.PrefabPath);
            if (interior == null) interior = ShearwaterBuilder.Build();
            var shipRoot = new GameObject("SB_Ship");
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(interior, scene);
            ship.name = "Shearwater";
            ship.transform.SetParent(shipRoot.transform, false);
            ship.transform.localPosition = new Vector3(0, 0, -14.5f);

            //The enemy, hanging off the starboard side (scenery)
            var fastnet = AssetDatabase.LoadAssetAtPath<GameObject>(FastnetPath);
            if (fastnet != null)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(fastnet, scene);
                enemy.name = "Raider (scenery)";
                enemy.transform.position = new Vector3(40, -8, 10);
                enemy.transform.rotation = Quaternion.Euler(0, -30, 0) * fastnet.transform.rotation;
                foreach (var c in enemy.GetComponentsInChildren<Collider>()) c.enabled = false;
            }

            //Interior lights, one per room
            foreach (var room in ship.GetComponentsInChildren<ShipRoom>())
            {
                var l = new GameObject("Light " + room.roomName).AddComponent<Light>();
                l.transform.SetParent(shipRoot.transform, false);
                l.transform.position = room.transform.position + Vector3.up * 1.0f;
                l.type = LightType.Point; l.range = 9f; l.intensity = 1.6f; l.color = new Color(0.85f, 0.92f, 1f);
            }

            //Stations at the Shearwater's anchors
            var stations = new GameObject("SB_Stations").transform;
            foreach (Station s in System.Enum.GetValues(typeof(Station)))
            {
                var anchor = ship.transform.Find("Station Anchors/" + s);
                if (anchor != null) PlaceStation(stations, s, anchor.position, anchor.eulerAngles.y);
            }
            Vector3 playerStart = Anchor(ship, "Player Start", new Vector3(0, 0, 3.5f));
            Vector3 squadStart = Anchor(ship, "Squad Start", new Vector3(0, 0, 0.5f));

            //The ship's brain (handed to the player at the start) and the battle
            var brain = new GameObject("SB_Battle");
            brain.AddComponent<ShipAI>();
            var battle = brain.AddComponent<ShipBattle>();
            var bso = new SerializedObject(battle);
            var boarders = bso.FindProperty("boarderPrefabs");
            boarders.arraySize = 0;
            foreach (var path in BoarderPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                boarders.arraySize++;
                boarders.GetArrayElementAtIndex(boarders.arraySize - 1).objectReferenceValue = prefab;
            }
            bso.ApplyModifiedPropertiesWithoutUndo();

            //Player rig like the Sandbox, Rambler in the crew quarters, hired crew deployed into the hold
            var coreSrc = FleetBuilder.Root(template, "PlayerCore");
            var camsSrc = FleetBuilder.Root(template, "----Cameras---- Sandbox");
            var playerSrc = FleetBuilder.Root(template, "Rambler");
            var map = new Dictionary<GameObject, GameObject>();
            var core = FleetBuilder.Copy(coreSrc, scene, "PlayerCore", map);
            var cams = FleetBuilder.Copy(camsSrc, scene, "----Cameras---- ShipBattle", map);
            var player = FleetBuilder.Copy(playerSrc, scene, "Rambler", map);
            foreach (var pair in new[] { (coreSrc, core), (camsSrc, cams), (playerSrc, player) })
                FleetBuilder.CopyLinks(pair.Item1, pair.Item2, map);
            FleetBuilder.PlaceInstance(player, playerStart, Quaternion.identity);
            if (core != null) core.transform.position = Vector3.zero;
            if (cams != null) cams.transform.position = Vector3.zero;
            FleetBuilder.TrimPlayers(core);

            var deployer = new GameObject("SquadDeployer").AddComponent<SquadDeployer>();
            deployer.transform.position = squadStart;
            var dso = new SerializedObject(deployer);
            dso.FindProperty("deployOnStart").boolValue = true;
            dso.FindProperty("spacing").floatValue = 1.1f;
            dso.ApplyModifiedPropertiesWithoutUndo();

            //NavMesh over the ship
            //(room air volumes and door triggers are triggers, which the bake ignores)
            var surface = shipRoot.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                string navFolder = SceneFolder + "/ShipBattle";
                if (!AssetDatabase.IsValidFolder(navFolder)) AssetDatabase.CreateFolder(SceneFolder, "ShipBattle");
                AssetDatabase.CreateAsset(surface.navMeshData, navFolder + "/NavMesh-Shearwater.asset");
            }

            //The ship-to-ship layer, far below: the tactical map the Weapons screen shows
            TacticalBuilder.BuildSpace();
        }

        //The ramp comes in lowered (parked); swing it up about its hinge until it stands closed
        static void RaiseRamp(GameObject ship)
        {
            var ramp = ship.transform.Find("SW_Ramp");
            if (ramp == null) return;
            var r = ramp.GetComponent<Renderer>();
            Vector3 hinge = ramp.position;
            Quaternion startRot = ramp.rotation; Vector3 startPos = ramp.position;
            float best = 0f, bestDepth = float.MaxValue;
            for (float a = 0f; a <= 180f; a += 1f)
            {
                ramp.SetPositionAndRotation(startPos, startRot);
                ramp.RotateAround(hinge, Vector3.right, a);
                var b = r.bounds;
                if (b.center.y < hinge.y) continue;
                if (b.size.z < bestDepth) { bestDepth = b.size.z; best = a; }
            }
            ramp.SetPositionAndRotation(startPos, startRot);
            ramp.RotateAround(hinge, Vector3.right, best);
        }

        static Vector3 Anchor(GameObject ship, string name, Vector3 fallback)
        {
            var t = ship.transform.Find("Station Anchors/" + name);
            return t != null ? t.position : fallback;
        }

        static ShipRoom Room(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = size;
            var room = go.AddComponent<ShipRoom>();
            room.roomName = name;
            return room;
        }

        static ShipDoor SlidingDoor(Transform parent, string name, Vector3 pos, Vector3 size, GameObject ship, string[] panels, Vector3[] offsets)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = size;
            var door = go.AddComponent<ShipDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("displayName").stringValue = name;
            var sp = so.FindProperty("slidingPanels");
            var op = so.FindProperty("openOffsets");
            sp.arraySize = panels.Length; op.arraySize = panels.Length;
            for (int i = 0; i < panels.Length; i++)
            {
                sp.GetArrayElementAtIndex(i).objectReferenceValue = ship.transform.Find(panels[i]);
                op.GetArrayElementAtIndex(i).vector3Value = offsets[i];
            }
            var obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true; obstacle.size = new Vector3(size.x, size.y, 0.3f); obstacle.enabled = false;
            so.FindProperty("obstacle").objectReferenceValue = obstacle;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        static void LinkDoors(ShipRoom room, params ShipDoor[] doors)
        {
            var so = new SerializedObject(room);
            var d = so.FindProperty("doors");
            d.arraySize = doors.Length;
            for (int i = 0; i < doors.Length; i++) d.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void PlaceStation(Transform parent, Station s, Vector3 pos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StationPath(s));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        }

        static void AddToBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == path) { s.enabled = true; EditorBuildSettings.scenes = list.ToArray(); return; }
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ---------- Shipyard: the Launch terminal ----------

        //Puts a LAUNCH terminal by the first pad in the open Shipyard scene
        [MenuItem("Tools/Company/Place Launch Terminal")]
        static void PlaceLaunch()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaunchPath);
            if (prefab == null) { BuildAll(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaunchPath); }
            var scene = SceneManager.GetActiveScene();
            foreach (var t in Object.FindObjectsOfType<LaunchTerminal>()) Object.DestroyImmediate(t.gameObject);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var env = GameObject.Find("SY_Environment");
            if (env != null) go.transform.SetParent(env.transform, true);
            go.transform.SetPositionAndRotation(new Vector3(-12f, 0f, 14f), Quaternion.identity);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = go;
        }
    }
}
