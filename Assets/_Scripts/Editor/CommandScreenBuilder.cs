using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Crew.UI;
using Rambler.UI;
using B = Rambler.EditorTools.CrewScreenBuilder;

namespace Rambler.EditorTools
{
    //Builds the Command/Map screen and the HUD's MAP button into PlayerCore.prefab (HUD/CommandScreen, HUD/MapButton).
    //Safe to run again: it replaces the previous build. Uses the Crew screen builder's helpers, colours and font sizes.
    public static class CommandScreenBuilder
    {
        const string PlayerCorePath = "Assets/_Prefabs/PlayerCore/PlayerCore.prefab";
        static readonly Color Window = Hex("0A0F17F0"), Panel = Hex("171F29E6"), Tile = Hex("293D52E6");
        static readonly Color Orange = Hex("F29E33FF"), Red = Hex("E64033FF"), Blue = Hex("4CA6F2FF"), Dim = new Color(1, 1, 1, 0.7f);

        [MenuItem("Tools/Company/Build Command Screen")]
        static void Build()
        {
            B.Load();
            var root = PrefabUtility.LoadPrefabContents(PlayerCorePath);
            try
            {
                Transform hud = B.Find(root.transform, "HUD");
                if (hud == null) { Debug.LogError("[Company] No HUD in " + PlayerCorePath); return; }
                foreach (var n in new[] { "CommandScreen", "MapButton" })
                {
                    var old = hud.Find(n);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }

                //HUD button, top left beside the ship map button
                var mapButton = B.Img("MapButton", (RectTransform)hud, Orange, null);
                B.TopLeft(mapButton, new Vector2(200, -30), new Vector2(130, 120));
                var mapLabel = B.Txt("Label", mapButton.rectTransform, "MAP", 36, TextAlignmentOptions.Center);
                B.Stretch(mapLabel.rectTransform); mapLabel.fontStyle = FontStyles.Bold;
                var openButton = mapButton.gameObject.AddComponent<Button>();

                var holder = B.Rect("CommandScreen", hud);
                B.Stretch(holder);
                var canvas = holder.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true; canvas.sortingOrder = 40; //Under the Crew screen (50), over the HUD
                holder.gameObject.AddComponent<GraphicRaycaster>();
                var screen = holder.gameObject.AddComponent<CommandScreen>();
                var so = new SerializedObject(screen);
                B.Set(so, "openButton", openButton);

                var window = B.Rect("Window", holder);
                B.Stretch(window);
                B.Set(so, "window", window.gameObject);

                //1. The map's tap layer (see-through)
                var tap = B.Img("TapArea", window, new Color(0, 0, 0, 0), null);
                B.Stretch(tap.rectTransform);
                var tapArea = tap.gameObject.AddComponent<CommandTapArea>();
                B.Set(so, "tapArea", tapArea);

                //2. Markers, in draw order
                var markers = B.Rect("Markers", window);
                B.Stretch(markers);
                B.Set(so, "markerLayer", markers);
                var lines = Layer("Lines", markers); var flags = Layer("Flags", markers); var pois = Layer("Points", markers);
                var enemies = Layer("Enemies", markers); var units = Layer("Units", markers);

                var line = B.Img("LineTemplate", lines, Orange, null);
                Mid(line.rectTransform, new Vector2(100, 8)); line.raycastTarget = false;
                B.Set(so, "lineTemplate", line);

                var flag = B.Img("FlagTemplate", flags, Orange, null);
                Mid(flag.rectTransform, new Vector2(56, 56)); flag.raycastTarget = false;
                var flagItem = flag.gameObject.AddComponent<CrewUIItem>();
                flagItem.initials = B.Txt("Glyph", flag.rectTransform, "H", 30, TextAlignmentOptions.Center);
                B.Stretch(flagItem.initials.rectTransform); flagItem.initials.fontStyle = FontStyles.Bold; flagItem.initials.color = Hex("0A0F17FF");
                B.Set(so, "flagTemplate", flagItem);

                var poi = B.Rect("PoiTemplate", pois);
                Mid(poi, new Vector2(240, 120));
                var poiItem = poi.gameObject.AddComponent<CrewUIItem>();
                poiItem.portrait = B.Img("Icon", poi, Orange, null);
                B.Place(poiItem.portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(66, 66)); poiItem.portrait.raycastTarget = false;
                poiItem.initials = B.Txt("Glyph", poiItem.portrait.rectTransform, "!", 38, TextAlignmentOptions.Center);
                B.Stretch(poiItem.initials.rectTransform); poiItem.initials.fontStyle = FontStyles.Bold; poiItem.initials.color = Hex("0A0F17FF");
                poiItem.title = B.Txt("Label", poi, "Exit", 26, TextAlignmentOptions.Center);
                B.Place(poiItem.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(240, 36));
                Outline(poiItem.title);
                B.Set(so, "poiMarkerTemplate", poiItem);

                var enemy = B.Img("EnemyTemplate", enemies, Red, null);
                Mid(enemy.rectTransform, new Vector2(70, 70));
                var enemyItem = enemy.gameObject.AddComponent<CrewUIItem>();
                enemyItem.initials = B.Txt("Glyph", enemy.rectTransform, "!", 40, TextAlignmentOptions.Center);
                B.Stretch(enemyItem.initials.rectTransform); enemyItem.initials.fontStyle = FontStyles.Bold;
                enemyItem.bar = Bar(enemy.rectTransform, new Vector2(0, -50), new Vector2(80, 12), Red);
                enemyItem.select = enemy.gameObject.AddComponent<Button>();
                B.Set(so, "enemyMarkerTemplate", enemyItem);

                var unit = B.Rect("UnitTemplate", units);
                Mid(unit, new Vector2(220, 190));
                var unitItem = unit.gameObject.AddComponent<CrewUIItem>();
                unitItem.highlight = B.Img("Ring", unit, new Color(1f, 0.85f, 0.2f, 0.85f), B.circle);
                B.Place(unitItem.highlight.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(108, 108)); unitItem.highlight.raycastTarget = false;
                var hit = B.Img("Hit", unit, Color.clear, null); //What a tap on the marker lands on
                B.Place(hit.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(150, 160));
                unitItem.portrait = B.Img("Portrait", unit, Color.gray, null);
                B.Place(unitItem.portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(84, 84)); unitItem.portrait.raycastTarget = false;
                unitItem.initials = B.Txt("Initials", unitItem.portrait.rectTransform, "AB", 34, TextAlignmentOptions.Center);
                B.Stretch(unitItem.initials.rectTransform); unitItem.initials.fontStyle = FontStyles.Bold;
                unitItem.bar = Bar(unit, new Vector2(0, -22), new Vector2(96, 12), new Color(0.35f, 0.95f, 0.45f));
                unitItem.title = B.Txt("Name", unit, "Name", 28, TextAlignmentOptions.Center);
                B.Place(unitItem.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(220, 36)); Outline(unitItem.title);
                unitItem.side = B.Txt("Order", unit, "Follow", 24, TextAlignmentOptions.Center);
                B.Place(unitItem.side.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(220, 30)); unitItem.side.color = Orange; Outline(unitItem.side);
                var unitButton = hit.gameObject.AddComponent<Button>(); unitButton.transition = Selectable.Transition.None;
                unitItem.select = unitButton;
                B.Set(so, "unitMarkerTemplate", unitItem);

                //3. Header
                var header = B.Img("Header", window, Window, null);
                header.rectTransform.anchorMin = new Vector2(0, 1); header.rectTransform.anchorMax = new Vector2(1, 1); header.rectTransform.pivot = new Vector2(0.5f, 1);
                header.rectTransform.anchoredPosition = Vector2.zero; header.rectTransform.sizeDelta = new Vector2(0, 116);
                var title = B.Txt("Title", header.rectTransform, "COMMAND", 52, TextAlignmentOptions.MidlineLeft);
                B.TopLeft(title, new Vector2(40, -14), new Vector2(400, 88)); title.fontStyle = FontStyles.Bold;
                var resume = B.Btn("Resume", header.rectTransform, Orange, "RESUME", 38, Vector2.zero, new Vector2(280, 86), out _);
                B.TopRight((RectTransform)resume.transform, new Vector2(-150, -15), new Vector2(280, 86));
                B.Set(so, "resumeButton", resume);
                var close = B.Img("Close", header.rectTransform, Color.white, B.circle);
                B.TopRight(close, new Vector2(-26, -8), new Vector2(100, 100));
                var closeLabel = B.Txt("Label", close.rectTransform, "X", 48, TextAlignmentOptions.Center); B.Stretch(closeLabel.rectTransform);
                B.Set(so, "closeButton", close.gameObject.AddComponent<Button>());

                //4. Squad bar (left)
                var squad = B.Img("Squad", window, Panel, null);
                B.TopLeft(squad, new Vector2(24, -132), new Vector2(400, 790));
                var all = B.Btn("All", squad.rectTransform, Tile, "ALL", 34, new Vector2(15, -15), new Vector2(370, 80), out _);
                B.Set(so, "allButton", all);
                var listArea = B.Rect("List", squad.rectTransform);
                listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = new Vector2(15, 15); listArea.offsetMax = new Vector2(-15, -110);
                var content = B.Scroll(listArea, false, 10, new RectOffset(0, 0, 0, 0));
                var row = B.Img("SquadRowTemplate", content, Tile, null).rectTransform;
                B.Size(row, new Vector2(370, 116));
                var rowItem = row.gameObject.AddComponent<CrewUIItem>();
                rowItem.highlight = B.Img("Selected", row, new Color(1f, 0.85f, 0.2f, 0.3f), null); B.Stretch(rowItem.highlight.rectTransform); rowItem.highlight.raycastTarget = false;
                rowItem.portrait = B.Img("Portrait", row, Color.gray, null);
                B.Place(rowItem.portrait.rectTransform, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(86, 86)); rowItem.portrait.raycastTarget = false;
                rowItem.initials = B.Txt("Initials", rowItem.portrait.rectTransform, "AB", 34, TextAlignmentOptions.Center);
                B.Stretch(rowItem.initials.rectTransform); rowItem.initials.fontStyle = FontStyles.Bold;
                rowItem.title = B.Txt("Name", row, "Name", 32, TextAlignmentOptions.MidlineLeft);
                B.LeftMid(rowItem.title.rectTransform, new Vector2(112, 26), new Vector2(240, 42));
                rowItem.bar = Bar(row, Vector2.zero, new Vector2(150, 12), new Color(0.35f, 0.95f, 0.45f));
                B.Place((RectTransform)rowItem.bar.transform.parent, new Vector2(0, 0.5f), new Vector2(112, -4), new Vector2(150, 12));
                rowItem.side = B.Txt("Order", row, "Follow", 26, TextAlignmentOptions.MidlineLeft);
                B.LeftMid(rowItem.side.rectTransform, new Vector2(112, -32), new Vector2(240, 34)); rowItem.side.color = Orange;
                rowItem.select = row.gameObject.AddComponent<Button>();
                B.Set(so, "squadRowTemplate", rowItem);

                //5. Orders (right)
                var orders = B.Img("Orders", window, Panel, null);
                B.TopRight(orders, new Vector2(-24, -132), new Vector2(330, 470));
                B.Set(so, "followButton", B.Btn("Follow", orders.rectTransform, Blue, "FOLLOW", 36, new Vector2(20, -20), new Vector2(290, 96), out _));
                B.Set(so, "holdButton", B.Btn("Hold", orders.rectTransform, Orange, "HOLD", 36, new Vector2(20, -132), new Vector2(290, 96), out _));
                B.Set(so, "attackButton", B.Btn("Attack", orders.rectTransform, Red, "ATTACK", 36, new Vector2(20, -244), new Vector2(290, 96), out _));
                B.Set(so, "controlButton", B.Btn("TakeControl", orders.rectTransform, Tile, "TAKE CONTROL", 34, new Vector2(20, -356), new Vector2(290, 96), out _));

                //6. Footer
                var footer = B.Img("Footer", window, Window, null);
                footer.rectTransform.anchorMin = Vector2.zero; footer.rectTransform.anchorMax = new Vector2(1, 0); footer.rectTransform.pivot = new Vector2(0.5f, 0);
                footer.rectTransform.anchoredPosition = Vector2.zero; footer.rectTransform.sizeDelta = new Vector2(0, 110);
                var selectedText = B.Txt("Selected", footer.rectTransform, "Selected: Whole squad", 34, TextAlignmentOptions.MidlineLeft);
                B.LeftMid(selectedText.rectTransform, new Vector2(40, 0), new Vector2(560, 90)); selectedText.fontStyle = FontStyles.Bold;
                B.Set(so, "selectedText", selectedText);
                var hint = B.Txt("Hint", footer.rectTransform, "", 30, TextAlignmentOptions.MidlineLeft);
                hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one; hint.rectTransform.offsetMin = new Vector2(620, 0); hint.rectTransform.offsetMax = new Vector2(-40, 0);
                hint.color = Dim;
                B.Set(so, "hintText", hint);

                so.ApplyModifiedPropertiesWithoutUndo();
                window.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerCorePath);
                Debug.Log("[Company] Command screen built into " + PlayerCorePath + " (HUD/CommandScreen, HUD/MapButton).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static RectTransform Layer(string name, RectTransform parent)
        {
            var r = B.Rect(name, parent);
            B.Stretch(r);
            return r;
        }

        //Centre-anchored, so anchoredPosition is the marker's place in the map layer
        static void Mid(RectTransform r, Vector2 size) => B.Place(r, new Vector2(0.5f, 0.5f), Vector2.zero, size);

        static Image Bar(RectTransform parent, Vector2 pos, Vector2 size, Color colour)
        {
            var back = B.Img("BarBack", parent, new Color(0, 0, 0, 0.6f), null);
            B.Place(back.rectTransform, new Vector2(0.5f, 0.5f), pos, size); back.raycastTarget = false;
            var fill = B.Img("Bar", back.rectTransform, colour, null);
            B.Stretch(fill.rectTransform); fill.raycastTarget = false;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 1f;
            return fill;
        }

        //Map labels sit on the level itself, so put a dark backing behind them to read on any floor
        //(a TMP outline would need a per-label material instance, which doesn't save into the prefab)
        static void Outline(TextMeshProUGUI t)
        {
            var rt = t.rectTransform;
            var back = B.Img(t.name + "Back", (RectTransform)rt.parent, new Color(0.04f, 0.06f, 0.09f, 0.7f), null);
            var br = back.rectTransform;
            br.anchorMin = rt.anchorMin; br.anchorMax = rt.anchorMax; br.pivot = rt.pivot;
            br.anchoredPosition = rt.anchoredPosition; br.sizeDelta = rt.sizeDelta;
            back.raycastTarget = false;
            back.transform.SetSiblingIndex(rt.GetSiblingIndex());
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    }
}
