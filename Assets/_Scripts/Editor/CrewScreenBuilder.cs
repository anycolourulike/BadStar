using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Crew;
using Rambler.Crew.UI;

namespace Rambler.EditorTools
{
    //Builds the Crew screen into the HUD inside PlayerCore.prefab (so every scene has it) and makes the
    //CrewTerminal prefab. Safe to run again: it replaces the previous build. Colours and fonts follow the ship map;
    //text is sized for a phone (body 28-34 on the 1920x1080 HUD).
    public static class CrewScreenBuilder
    {
        const string PlayerCorePath = "Assets/_Prefabs/PlayerCore/PlayerCore.prefab";
        const string TerminalFolder = "Assets/_Prefabs/Company";
        const string TerminalPath = TerminalFolder + "/CrewTerminal.prefab";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string CirclePath = "Assets/_AssetPacks/UI/Grunge UI Kit/Source Files/Panels/Circle/circle4.png";

        static readonly Color WindowCol = Hex("0A0F17F0"), PanelCol = Hex("171F29D9"), TileCol = Hex("293D52E6");
        static readonly Color Orange = Hex("F29E33FF"), Red = Hex("E64033FF"), Blue = Hex("4CA6F2FF"), Dim = new Color(1, 1, 1, 0.65f);

        static Sprite uiSprite, circle;
        static TMP_FontAsset font;

        [MenuItem("Tools/Company/Build Crew Screen")]
        static void BuildAll()
        {
            Load();
            BuildScreen();
            BuildTerminalPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[Company] Crew screen built into " + PlayerCorePath + " (HUD/CrewScreen); terminal prefab at " + TerminalPath + ".");
        }

        static void Load()
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            circle = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }

        // ---------- The window ----------

        static void BuildScreen()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerCorePath);
            try
            {
                Transform hud = Find(root.transform, "HUD");
                if (hud == null) { Debug.LogError("[Company] No HUD in " + PlayerCorePath); return; }
                var old = hud.Find("CrewScreen");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var holder = Rect("CrewScreen", hud);
                Stretch(holder);
                //Its own canvas, drawn over the rest of the HUD (portraits, wave bar, pause button)
                var overlay = holder.gameObject.AddComponent<Canvas>();
                overlay.overrideSorting = true; overlay.sortingOrder = 50;
                holder.gameObject.AddComponent<GraphicRaycaster>();
                var screen = holder.gameObject.AddComponent<CrewScreen>();
                var so = new SerializedObject(screen);

                //Window
                var window = Img("Window", holder, WindowCol).rectTransform;
                Center(window, new Vector2(1800, 1000));
                Set(so, "window", window.gameObject);

                var title = Txt("Title", window, "CREW", 56, TextAlignmentOptions.MidlineLeft);
                TopLeft(title, new Vector2(40, -20), new Vector2(240, 88));
                title.fontStyle = FontStyles.Bold;
                Set(so, "recruitTab", Btn("RecruitTab", window, TileCol, "Recruit", 38, new Vector2(290, -24), new Vector2(240, 80), out _));
                Set(so, "crewTab", Btn("CrewTab", window, TileCol, "Crew", 38, new Vector2(546, -24), new Vector2(240, 80), out _));
                var credits = Txt("Credits", window, "Credits", 42, TextAlignmentOptions.MidlineRight);
                TopRight(credits, new Vector2(-150, -20), new Vector2(600, 88));
                Set(so, "creditsText", credits);
                var close = Img("Close", window, Color.white, circle);
                TopRight(close, new Vector2(-26, -14), new Vector2(100, 100));
                var closeLabel = Txt("Label", close.rectTransform, "X", 48, TextAlignmentOptions.Center); Stretch(closeLabel.rectTransform);
                Set(so, "closeButton", close.gameObject.AddComponent<Button>());

                var message = Txt("Message", window, "", 32, TextAlignmentOptions.MidlineLeft);
                BottomLeft(message, new Vector2(40, 16), new Vector2(1320, 60));
                message.color = Orange;
                Set(so, "messageText", message);
                var footer = Txt("Footer", window, "Crew 0/12", 32, TextAlignmentOptions.MidlineRight);
                BottomRight(footer, new Vector2(-40, 16), new Vector2(380, 60));
                Set(so, "footerText", footer);

                BuildRecruitPage(window, so);
                BuildCrewPage(window, so);

                so.ApplyModifiedPropertiesWithoutUndo();
                window.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerCorePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void BuildRecruitPage(RectTransform window, SerializedObject so)
        {
            var page = Rect("RecruitPage", window);
            Body(page);
            Set(so, "recruitPage", page.gameObject);
            var content = Scroll(page, true, 24, new RectOffset(10, 10, 10, 10));

            var card = Img("CandidateTemplate", content, TileCol).rectTransform;
            Size(card, new Vector2(548, 760));
            var item = card.gameObject.AddComponent<CrewUIItem>();
            Portrait(card, item, new Vector2(0, -24), 240, true);
            item.title = Txt("Name", card, "Name", 44, TextAlignmentOptions.Center);
            TopCenter(item.title.rectTransform, new Vector2(0, -276), new Vector2(510, 58)); item.title.fontStyle = FontStyles.Bold;
            item.side = Txt("Tag", card, "NAMED", 26, TextAlignmentOptions.Center);
            TopCenter(item.side.rectTransform, new Vector2(0, -334), new Vector2(510, 34)); item.side.color = Orange;
            item.body = Txt("Details", card, "Skills", 34, TextAlignmentOptions.TopLeft);
            TopCenter(item.body.rectTransform, new Vector2(0, -378), new Vector2(460, 250));
            item.body.lineSpacing = -8f;
            item.action = Btn("Hire", card, Orange, "Hire", 40, Vector2.zero, new Vector2(360, 100), out item.actionLabel);
            BottomCenter((RectTransform)item.action.transform, new Vector2(0, 24), new Vector2(360, 100));
            Set(so, "candidateTemplate", item);
        }

        static void BuildCrewPage(RectTransform window, SerializedObject so)
        {
            var page = Rect("CrewPage", window);
            Body(page);
            Set(so, "crewPage", page.gameObject);

            //Left: the crew list
            var list = Img("CrewList", page, PanelCol).rectTransform;
            list.anchorMin = new Vector2(0, 0); list.anchorMax = new Vector2(0, 1); list.pivot = new Vector2(0, 1);
            list.anchoredPosition = Vector2.zero; list.sizeDelta = new Vector2(500, 0);
            var content = Scroll(list, false, 10, new RectOffset(10, 10, 10, 10));
            var row = Img("CrewRowTemplate", content, TileCol).rectTransform;
            Size(row, new Vector2(480, 118));
            var rowItem = row.gameObject.AddComponent<CrewUIItem>();
            rowItem.highlight = Img("Selected", row, new Color(1f, 0.85f, 0.2f, 0.28f)); Stretch(rowItem.highlight.rectTransform);
            Portrait(row, rowItem, Vector2.zero, 90, false);
            var rp = (RectTransform)rowItem.portrait.transform; rp.anchorMin = rp.anchorMax = rp.pivot = new Vector2(0, 0.5f); rp.anchoredPosition = new Vector2(14, 0);
            rowItem.title = Txt("Name", row, "Name", 36, TextAlignmentOptions.MidlineLeft);
            LeftMid(rowItem.title.rectTransform, new Vector2(122, 16), new Vector2(340, 46));
            rowItem.side = Txt("Status", row, "Ready", 28, TextAlignmentOptions.MidlineLeft);
            LeftMid(rowItem.side.rectTransform, new Vector2(122, -26), new Vector2(340, 36)); rowItem.side.color = Dim;
            rowItem.select = row.gameObject.AddComponent<Button>();
            Set(so, "crewRowTemplate", rowItem);
            var empty = Txt("Empty", list, "No crew yet.\nHire someone on the Recruit tab.", 32, TextAlignmentOptions.Center);
            Stretch(empty.rectTransform); empty.color = Dim;
            Set(so, "emptyCrewText", empty);

            //Right: the selected person
            var detail = Img("Detail", page, PanelCol).rectTransform;
            detail.anchorMin = Vector2.zero; detail.anchorMax = Vector2.one; detail.offsetMin = new Vector2(520, 0); detail.offsetMax = Vector2.zero;
            Set(so, "detailRoot", detail.gameObject);

            var header = Rect("Header", detail);
            TopLeft(header, new Vector2(24, -20), new Vector2(1170, 124));
            var headerItem = header.gameObject.AddComponent<CrewUIItem>();
            Portrait(header, headerItem, Vector2.zero, 120, false);
            var pr = (RectTransform)headerItem.portrait.transform; pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0, 1); pr.anchoredPosition = Vector2.zero;
            headerItem.title = Txt("Name", header, "Name", 50, TextAlignmentOptions.MidlineLeft);
            TopLeft(headerItem.title.rectTransform, new Vector2(146, -4), new Vector2(1010, 62)); headerItem.title.fontStyle = FontStyles.Bold;
            headerItem.body = Txt("Info", header, "Ready", 30, TextAlignmentOptions.MidlineLeft);
            TopLeft(headerItem.body.rectTransform, new Vector2(146, -72), new Vector2(1010, 44)); headerItem.body.color = Dim;
            Set(so, "detailHeader", headerItem);

            Set(so, "skillsTab", Btn("SkillsTab", detail, TileCol, "Skills", 34, new Vector2(24, -160), new Vector2(250, 72), out _));
            Set(so, "loadoutTab", Btn("LoadoutTab", detail, TileCol, "Loadout", 34, new Vector2(286, -160), new Vector2(250, 72), out _));
            Set(so, "trainingTab", Btn("TrainingTab", detail, TileCol, "Training", 34, new Vector2(548, -160), new Vector2(250, 72), out _));

            BuildSkillsPage(Page("SkillsPage", detail, so, "skillsPage"), so);
            BuildLoadoutPage(Page("LoadoutPage", detail, so, "loadoutPage"), so);
            BuildTrainingPage(Page("TrainingPage", detail, so, "trainingPage"), so);

            var deploy = Btn("Deploy", detail, Blue, "Deploy", 36, Vector2.zero, new Vector2(280, 88), out var deployLabel);
            BottomLeft((RectTransform)deploy.transform, new Vector2(24, 18), new Vector2(280, 88));
            Set(so, "deployButton", deploy); Set(so, "deployLabel", deployLabel);
            var fire = Btn("Fire", detail, Red, "Fire", 36, Vector2.zero, new Vector2(280, 88), out var fireLabel);
            BottomRight((RectTransform)fire.transform, new Vector2(-24, 18), new Vector2(280, 88));
            Set(so, "fireButton", fire); Set(so, "fireLabel", fireLabel);
        }

        static RectTransform Page(string name, RectTransform detail, SerializedObject so, string field)
        {
            var page = Rect(name, detail);
            page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one;
            page.offsetMin = new Vector2(24, 122); page.offsetMax = new Vector2(-24, -248);
            Set(so, field, page.gameObject);
            return page;
        }

        static void BuildSkillsPage(RectTransform page, SerializedObject so)
        {
            var content = Scroll(page, false, 8, new RectOffset(0, 12, 0, 0));
            var row = Img("SkillTemplate", content, TileCol).rectTransform;
            Size(row, new Vector2(1150, 72));
            var item = row.gameObject.AddComponent<CrewUIItem>();
            item.title = Txt("Skill", row, "Skill", 34, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.title.rectTransform, new Vector2(22, 0), new Vector2(280, 60));
            var barBack = Img("BarBack", row, new Color(0, 0, 0, 0.45f));
            LeftMid(barBack, new Vector2(320, 0), new Vector2(380, 24));
            item.bar = Img("Bar", barBack.rectTransform, Orange);
            Stretch(item.bar.rectTransform);
            item.bar.type = Image.Type.Filled; item.bar.fillMethod = Image.FillMethod.Horizontal; item.bar.fillAmount = 0.3f;
            item.side = Txt("Level", row, "0 / 10", 32, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.side.rectTransform, new Vector2(722, 0), new Vector2(140, 60));
            item.body = Txt("Bonus", row, "", 28, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.body.rectTransform, new Vector2(870, 0), new Vector2(270, 60)); item.body.color = Orange;
            Set(so, "skillTemplate", item);
        }

        static void BuildLoadoutPage(RectTransform page, SerializedObject so)
        {
            var carriedCol = Rect("Carried", page);
            carriedCol.anchorMin = new Vector2(0, 0); carriedCol.anchorMax = new Vector2(0.5f, 1); carriedCol.offsetMin = Vector2.zero; carriedCol.offsetMax = new Vector2(-12, 0);
            var armouryCol = Rect("Armoury", page);
            armouryCol.anchorMin = new Vector2(0.5f, 0); armouryCol.anchorMax = new Vector2(1, 1); armouryCol.offsetMin = new Vector2(12, 0); armouryCol.offsetMax = Vector2.zero;

            foreach (var (col, heading) in new[] { (carriedCol, "CARRYING"), (armouryCol, "ARMOURY") })
            {
                var h = Txt("Heading", col, heading, 30, TextAlignmentOptions.MidlineLeft);
                TopLeft(h.rectTransform, new Vector2(4, 0), new Vector2(500, 44)); h.color = Dim;
            }

            var carriedContent = Scroll(Inset(carriedCol), false, 8, new RectOffset(0, 10, 0, 0));
            Set(so, "carriedTemplate", WeaponRow("CarriedTemplate", carriedContent, true));

            var armouryContent = Scroll(Inset(armouryCol), false, 8, new RectOffset(0, 10, 0, 0));
            Set(so, "armouryTemplate", WeaponRow("ArmouryTemplate", armouryContent, false));
            var empty = Txt("Empty", armouryCol, "Empty. Stored weapons go here.", 30, TextAlignmentOptions.Center);
            Stretch(empty.rectTransform); empty.color = Dim;
            Set(so, "armouryEmptyText", empty);
        }

        static RectTransform Inset(RectTransform col)
        {
            var r = Rect("List", col);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(0, -52);
            return r;
        }

        static CrewUIItem WeaponRow(string name, RectTransform content, bool twoButtons)
        {
            var row = Img(name, content, TileCol).rectTransform;
            Size(row, new Vector2(560, 104));
            var item = row.gameObject.AddComponent<CrewUIItem>();
            Portrait(row, item, Vector2.zero, 80, false);
            var p = (RectTransform)item.portrait.transform; p.anchorMin = p.anchorMax = p.pivot = new Vector2(0, 0.5f); p.anchoredPosition = new Vector2(10, 0);
            item.portrait.preserveAspect = true;
            float textWidth = twoButtons ? 196 : 300;
            item.title = Txt("Weapon", row, "Weapon", 32, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.title.rectTransform, new Vector2(102, 16), new Vector2(textWidth, 46));
            item.title.enableAutoSizing = true; item.title.fontSizeMax = 32; item.title.fontSizeMin = 22;
            item.side = Txt("Tag", row, "", 24, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.side.rectTransform, new Vector2(102, -26), new Vector2(140, 32)); item.side.color = Orange;
            item.body = Txt("Note", row, "", 22, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.body.rectTransform, new Vector2(176, -26), new Vector2(textWidth - 74, 32)); item.body.color = Dim;
            item.action = Btn("Action", row, Orange, twoButtons ? "Equip" : "Take", 30, Vector2.zero, new Vector2(124, 78), out item.actionLabel);
            var a = (RectTransform)item.action.transform; a.anchorMin = a.anchorMax = a.pivot = new Vector2(1, 0.5f);
            a.anchoredPosition = new Vector2(twoButtons ? -146 : -12, 0);
            if (twoButtons)
            {
                item.action2 = Btn("Action2", row, Blue, "Store", 30, Vector2.zero, new Vector2(124, 78), out item.action2Label);
                var b = (RectTransform)item.action2.transform; b.anchorMin = b.anchorMax = b.pivot = new Vector2(1, 0.5f); b.anchoredPosition = new Vector2(-12, 0);
            }
            return item;
        }

        static void BuildTrainingPage(RectTransform page, SerializedObject so)
        {
            var status = Txt("Status", page, "", 30, TextAlignmentOptions.MidlineLeft);
            TopLeft(status.rectTransform, new Vector2(4, 0), new Vector2(1150, 46)); status.color = Dim;
            Set(so, "trainingStatusText", status);
            var list = Rect("List", page);
            list.anchorMin = Vector2.zero; list.anchorMax = Vector2.one; list.offsetMin = Vector2.zero; list.offsetMax = new Vector2(0, -58);
            var content = Scroll(list, false, 8, new RectOffset(0, 12, 0, 0));
            var row = Img("CourseTemplate", content, TileCol).rectTransform;
            Size(row, new Vector2(1150, 106));
            var item = row.gameObject.AddComponent<CrewUIItem>();
            item.title = Txt("Course", row, "Course", 34, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.title.rectTransform, new Vector2(22, 18), new Vector2(700, 46));
            item.body = Txt("Details", row, "", 28, TextAlignmentOptions.MidlineLeft);
            LeftMid(item.body.rectTransform, new Vector2(22, -24), new Vector2(780, 38)); item.body.color = Dim;
            item.side = Txt("Price", row, "0", 34, TextAlignmentOptions.MidlineRight);
            var s = item.side.rectTransform; s.anchorMin = s.anchorMax = s.pivot = new Vector2(1, 0.5f); s.anchoredPosition = new Vector2(-200, 0); s.sizeDelta = new Vector2(170, 60);
            item.action = Btn("Train", row, Orange, "Train", 32, Vector2.zero, new Vector2(170, 80), out item.actionLabel);
            var a = (RectTransform)item.action.transform; a.anchorMin = a.anchorMax = a.pivot = new Vector2(1, 0.5f); a.anchoredPosition = new Vector2(-14, 0);
            Set(so, "courseTemplate", item);
        }

        // ---------- The terminal prefab ----------

        static void BuildTerminalPrefab()
        {
            if (!AssetDatabase.IsValidFolder(TerminalFolder)) AssetDatabase.CreateFolder("Assets/_Prefabs", "Company");
            var root = new GameObject("CrewTerminal");
            try
            {
                var trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true; trigger.center = new Vector3(0, 1f, 0.8f); trigger.size = new Vector3(2.6f, 2f, 2.4f);

                var urp = Shader.Find("Universal Render Pipeline/Lit");
                var body = Mat(TerminalFolder + "/CrewTerminal_Body.mat", urp, new Color(0.18f, 0.22f, 0.27f), Color.black);
                var glow = Mat(TerminalFolder + "/CrewTerminal_Screen.mat", urp, new Color(0.95f, 0.62f, 0.2f), new Color(0.95f, 0.62f, 0.2f) * 1.6f);

                var desk = GameObject.CreatePrimitive(PrimitiveType.Cube); desk.name = "Desk";
                desk.transform.SetParent(root.transform, false);
                desk.transform.localPosition = new Vector3(0, 0.5f, 0); desk.transform.localScale = new Vector3(1.4f, 1f, 0.7f);
                desk.GetComponent<Renderer>().sharedMaterial = body;
                var screen = GameObject.CreatePrimitive(PrimitiveType.Cube); screen.name = "Screen";
                screen.transform.SetParent(root.transform, false);
                screen.transform.localPosition = new Vector3(0, 1.35f, -0.1f); screen.transform.localRotation = Quaternion.Euler(-15f, 0, 0);
                screen.transform.localScale = new Vector3(1.2f, 0.7f, 0.06f);
                screen.GetComponent<Renderer>().sharedMaterial = glow;

                //Prompt: a floating orange button
                var canvasGo = new GameObject("Prompt", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(root.transform, false);
                canvasGo.transform.localPosition = new Vector3(0, 2.5f, 0); canvasGo.transform.localScale = Vector3.one * 0.01f;
                var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(260, 104);
                var button = Btn("Open", (RectTransform)canvasGo.transform, Orange, "CREW", 54, Vector2.zero, new Vector2(260, 104), out var label);
                Stretch((RectTransform)button.transform);
                label.fontStyle = FontStyles.Bold;

                var terminal = root.AddComponent<CrewTerminal>();
                var so = new SerializedObject(terminal);
                so.FindProperty("prompt").objectReferenceValue = canvas;
                so.FindProperty("openButton").objectReferenceValue = button;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, TerminalPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        //Drops a terminal where the Scene view is looking
        [MenuItem("Tools/Company/Place Crew Terminal")]
        static void PlaceTerminal()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerminalPath);
            if (prefab == null) { BuildAll(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerminalPath); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var view = SceneView.lastActiveSceneView;
            Vector3 p = view != null ? view.pivot : Vector3.zero;
            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f)) p = hit.point;
            go.transform.position = p;
            Undo.RegisterCreatedObjectUndo(go, "Place Crew Terminal");
            Selection.activeGameObject = go;
        }

        static Material Mat(string path, Shader shader, Color color, Color emission)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(shader);
            m.SetColor("_BaseColor", color);
            if (emission != Color.black) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------- UI helpers ----------

        static void Portrait(RectTransform parent, CrewUIItem item, Vector2 topOffset, float size, bool topCentered)
        {
            item.portrait = Img("Portrait", parent, Color.gray);
            if (topCentered) TopCenter((RectTransform)item.portrait.transform, topOffset, new Vector2(size, size));
            else Size((RectTransform)item.portrait.transform, new Vector2(size, size));
            item.initials = Txt("Initials", (RectTransform)item.portrait.transform, "AB", size * 0.38f, TextAlignmentOptions.Center);
            Stretch(item.initials.rectTransform);
            item.initials.fontStyle = FontStyles.Bold;
        }

        //A scrolling list; returns the content to put items in
        static RectTransform Scroll(RectTransform parent, bool horizontal, float spacing, RectOffset padding)
        {
            var viewport = Rect("Viewport", parent);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = horizontal ? new Vector2(0, 1) : new Vector2(1, 1);
            content.pivot = new Vector2(0, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            HorizontalOrVerticalLayoutGroup group = horizontal ? content.gameObject.AddComponent<HorizontalLayoutGroup>() : (HorizontalOrVerticalLayoutGroup)content.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing; group.padding = padding;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = !horizontal; group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.UpperLeft;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            if (horizontal) { fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            else fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = parent.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = horizontal; scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            return content;
        }

        static void Size(RectTransform r, Vector2 size)
        {
            r.sizeDelta = size;
            var le = r.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = r.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y; le.minHeight = size.y;
        }

        static Button Btn(string name, RectTransform parent, Color color, string text, float fontSize, Vector2 topLeft, Vector2 size, out TextMeshProUGUI label)
        {
            var img = Img(name, parent, color);
            TopLeft(img.rectTransform, topLeft, size);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors; colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f); b.colors = colors;
            label = Txt("Label", img.rectTransform, text, fontSize, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.margin = new Vector4(8, 0, 8, 0);
            label.enableAutoSizing = true; label.fontSizeMax = fontSize; label.fontSizeMin = fontSize * 0.6f;
            return b;
        }

        static Image Img(string name, RectTransform parent, Color color, Sprite sprite = null)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : uiSprite;
            img.type = sprite != null ? Image.Type.Simple : Image.Type.Sliced;
            img.color = color;
            return img;
        }

        static TextMeshProUGUI Txt(string name, RectTransform parent, string text, float size, TextAlignmentOptions align)
        {
            var r = Rect(name, parent);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
            t.raycastTarget = false; t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Body(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(30, 88); r.offsetMax = new Vector2(-30, -126); }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Center(Component c, Vector2 size) { var r = (RectTransform)c.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = size; }
        static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size; }
        static void TopLeft(Component c, Vector2 pos, Vector2 size) => Place((RectTransform)c.transform, new Vector2(0, 1), pos, size);
        static void TopRight(Component c, Vector2 pos, Vector2 size) => Place((RectTransform)c.transform, new Vector2(1, 1), pos, size);
        static void TopCenter(Component c, Vector2 pos, Vector2 size) => Place((RectTransform)c.transform, new Vector2(0.5f, 1), pos, size);
        static void BottomLeft(Component c, Vector2 pos, Vector2 size) => Place((RectTransform)c.transform, new Vector2(0, 0), pos, size);
        static void BottomRight(Component c, Vector2 pos, Vector2 size) => Place((RectTransform)c.transform, new Vector2(1, 0), pos, size);
        static void BottomCenter(Component c, Vector2 pos, Vector2 size) => Place((RectTransform)c.transform, new Vector2(0.5f, 0), pos, size);
        static void LeftMid(Component c, Vector2 pos, Vector2 size) => Place((RectTransform)c.transform, new Vector2(0, 0.5f), pos, size);

        static void Set(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[Company] CrewScreen has no field '" + field + "'"); return; }
            p.objectReferenceValue = value;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
            return null;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    }
}
