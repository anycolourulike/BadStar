using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Rambler.Crew.Battle;
using Rambler.Crew.UI;
using static Rambler.EditorTools.CrewScreenBuilder;

namespace Rambler.EditorTools
{
    //The ship-to-ship layer of a ship battle:
    //  Build Tactical Space   puts the tactical map into the open ShipBattle scene, far below the ship: the Titan
    //                         contour surface, a light for each ship, the enemy's model for the target picture, and
    //                         the missile / rocket prefabs (Missile and Rocket Behavior models, URP materials).
    //  BuildWeaponsScreen     the Weapons station's screen in the HUD (called by Tools > Company > Build Ship Battle).
    public static class TacticalBuilder
    {
        const string Folder = "Assets/Tactical";
        const string MatFolder = Folder + "/Materials";
        const string MapTexture = Folder + "/TitanMap1.png";
        const string EnemyModel = "Assets/Puffin/Puffin.prefab";
        const string PackagePrefabs = "Assets/Missile and Rocket behavior/Media/Prefabs/Clear_Prefabs/Missile/";
        static readonly Vector3 Origin = new Vector3(0f, -1000f, 0f);

        // =====================================================================
        // Tactical space (scene)
        // =====================================================================

        [MenuItem("Tools/Company/Build Tactical Space")]
        static void BuildMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (Object.FindObjectOfType<ShipBattle>() == null) { Debug.LogWarning("[Company] Open the ShipBattle scene first."); return; }
            BuildSpace();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Company] Tactical space built into " + scene.path + ".");
        }

        internal static void BuildSpace()
        {
            foreach (var old in Object.FindObjectsOfType<TacticalSpace>()) Object.DestroyImmediate(old.gameObject);
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder(Folder, "Materials");
            var urp = Shader.Find("Universal Render Pipeline/Lit");

            var root = new GameObject("Tactical").AddComponent<TacticalSpace>();
            root.transform.position = Origin;

            //The Titan surface: a big floor with the contour map, glowing faintly so it reads even away from the lights
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapTexture);
            var importer = AssetImporter.GetAtPath(MapTexture) as TextureImporter;
            //Mirrored tiling hides the seams (the map isn't a seamless tile)
            if (importer != null && (importer.wrapMode != TextureWrapMode.Mirror || importer.maxTextureSize < 4096))
            {
                importer.wrapMode = TextureWrapMode.Mirror; importer.maxTextureSize = 4096; importer.SaveAndReimport();
            }
            var groundMat = Mat(MatFolder + "/TitanSurface.mat", urp, new Color(0.75f, 0.75f, 0.75f), Color.black);
            groundMat.SetTexture("_BaseMap", tex);
            groundMat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
            groundMat.EnableKeyword("_EMISSION");
            groundMat.SetTexture("_EmissionMap", tex);
            groundMat.SetColor("_EmissionColor", new Color(0.35f, 0.35f, 0.35f));
            groundMat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(groundMat);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "TitanSurface";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -2f, 0f);
            ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(6000f, 6000f, 1f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            //The ships: lights
            root.playerShip = Ship(root.transform, "Ship_Player", TacticalShip.Side.Player, "Shearwater", new Vector3(0f, 0f, -200f), 40f, new Color(0.45f, 0.85f, 1f), MatFolder + "/ShipPlayer.mat");
            root.enemyShip = Ship(root.transform, "Ship_Enemy", TacticalShip.Side.Enemy, "Raider", new Vector3(320f, 0f, 160f), 34f, new Color(1f, 0.3f, 0.22f), MatFolder + "/ShipEnemy.mat");

            //The enemy's model, off to one side, photographed from above for the target picture (nose +z)
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyModel);
            if (model != null)
            {
                var portrait = new GameObject("EnemyPortrait").transform;
                portrait.SetParent(root.transform, false);
                portrait.localPosition = new Vector3(6000f, 0f, 0f);
                var m = (GameObject)PrefabUtility.InstantiatePrefab(model, root.gameObject.scene);
                m.transform.SetParent(portrait, false);
                foreach (var c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (var mb in m.GetComponentsInChildren<MonoBehaviour>()) Object.DestroyImmediate(mb);
                root.portrait = portrait;
            }
            else Debug.LogWarning("[Company] No enemy model at " + EnemyModel + "; the target picture will be empty.");

            //Projectiles
            var blast = Blast();
            root.missilePrefab = Projectile("TacticalMissile", PackagePrefabs + "Type-1.prefab", blast, new Color(1f, 0.8f, 0.4f), 6f, 30f, 120f, 70f, 150f, 12f);
            root.rocketPrefab = Projectile("TacticalRocket", PackagePrefabs + "Type-4.prefab", blast, new Color(1f, 0.55f, 0.2f), 4.5f, 140f, 170f, 40f, 30f, 8f);
            root.enemyRocketPrefab = Projectile("EnemyRocket", PackagePrefabs + "Type-3.prefab", blast, new Color(1f, 0.25f, 0.2f), 4.5f, 120f, 150f, 40f, 30f, 9f);
            EditorUtility.SetDirty(root);
        }

        static TacticalShip Ship(Transform parent, string name, TacticalShip.Side side, string label, Vector3 pos, float speed, Color colour, string matPath)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            var mat = Mat(matPath, urp, colour, colour * 3f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * 12f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.5f, 0f); //Above the surface (in sphere units)
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; light.color = colour; light.range = 180f; light.intensity = 4f; light.shadows = LightShadows.None;
            var ship = go.AddComponent<TacticalShip>();
            ship.maxSpeed = speed;
            var so = new SerializedObject(ship);
            so.FindProperty("side").enumValueIndex = (int)side;
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("glow").objectReferenceValue = light;
            so.FindProperty("body").objectReferenceValue = go.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return ship;
        }

        //The flash a projectile makes going off
        static GameObject Blast()
        {
            string path = Folder + "/TacticalBlast.prefab";
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            var mat = Mat(MatFolder + "/Blast.mat", urp, new Color(1f, 0.6f, 0.2f), new Color(4f, 2f, 0.6f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TacticalBlast";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var lightGo = new GameObject("Flash");
            lightGo.transform.SetParent(go.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1f, 0.6f, 0.25f); light.range = 120f; light.intensity = 8f;
            var b = go.AddComponent<TacticalBlast>();
            var so = new SerializedObject(b);
            so.FindProperty("flash").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        //A missile or rocket: the package's model with URP materials, a trail, and TacticalMissile flying it
        static TacticalMissile Projectile(string name, string packagePrefab, GameObject blast, Color trailColour, float scale,
                                          float startSpeed, float maxSpeed, float accel, float turn, float life)
        {
            string path = Folder + "/" + name + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(packagePrefab);
            GameObject go;
            if (source != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var old in go.GetComponentsInChildren<Missiles.BaseMissile>()) Object.DestroyImmediate(old);
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) Object.DestroyImmediate(ps.gameObject);
                foreach (var audio in go.GetComponentsInChildren<AudioSource>()) Object.DestroyImmediate(audio);
                //Built-in materials show magenta in URP: same textures on URP Lit
                var urp = Shader.Find("Universal Render Pipeline/Lit");
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        string mp = MatFolder + "/" + (src != null ? src.name : "Missile") + "_URP.mat";
                        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                        if (m == null)
                        {
                            m = new Material(urp);
                            if (src != null && src.mainTexture != null) m.SetTexture("_BaseMap", src.mainTexture);
                            AssetDatabase.CreateAsset(m, mp);
                        }
                        mats[i] = m;
                    }
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            go.name = name;
            go.transform.localScale = Vector3.one * scale;
            if (go.GetComponent<Collider>() == null) go.AddComponent<SphereCollider>();
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.isTrigger = true;
            var rb = go.GetComponent<Rigidbody>(); if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.7f; trail.minVertexDistance = 1f;
            trail.widthMultiplier = 1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 5f), new Keyframe(1f, 0f));
            trail.sharedMaterial = TrailMat(name, trailColour);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(trailColour, 0f), new GradientColorKey(trailColour * 0.5f, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = grad;

            var m2 = go.AddComponent<TacticalMissile>();
            m2.startSpeed = startSpeed; m2.MaxSpeed = maxSpeed; m2.accelerationSpeed = accel; m2.turnSpeed = turn;
            m2.lifeTime = life; m2.missileStability = 0f; m2.explosionPrefab = blast;
            m2.damageOnTrigger = false; m2.damageOnCollision = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<TacticalMissile>();
        }

        //A see-through, glowing trail (URP particle shader, additive)
        static Material TrailMat(string name, Color colour)
        {
            string path = MatFolder + "/" + name + "_Trail.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // =====================================================================
        // Weapons screen (HUD)
        // =====================================================================

        internal static void BuildWeaponsScreen(Transform hud)
        {
            var oldHolder = hud.Find("WeaponsScreen");
            if (oldHolder != null) Object.DestroyImmediate(oldHolder.gameObject);
            var holder = Rect("WeaponsScreen", hud);
            Stretch(holder);
            var canvas = holder.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 50;
            holder.gameObject.AddComponent<GraphicRaycaster>();
            var screen = holder.gameObject.AddComponent<WeaponsScreen>();
            var so = new SerializedObject(screen);

            var window = Img("Window", holder, new Color(0.03f, 0.03f, 0.04f, 1f)).rectTransform;
            Stretch(window);
            Set(so, "window", window.gameObject);

            //The map, full screen
            var map = Raw("Map", window);
            Stretch(map.rectTransform);
            Set(so, "map", map);
            var markers = Rect("Markers", map.rectTransform); Stretch(markers);
            Set(so, "markerLayer", markers);
            var marker = Rect("MarkerTemplate", markers);
            Place(marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 190));
            var item = marker.gameObject.AddComponent<CrewUIItem>();
            item.highlight = Img("Ring", marker, new Color(1f, 0.3f, 0.25f, 0.9f), circle);
            Place(item.highlight.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            item.highlight.raycastTarget = false;
            var hole = Img("Hole", item.highlight.rectTransform, new Color(0, 0, 0, 0.55f), circle); //Makes the ring a ring
            Place(hole.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46)); hole.raycastTarget = false;
            var hit = Img("Hit", marker, Color.clear);
            Place(hit.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 180));
            item.select = hit.gameObject.AddComponent<Button>(); item.select.transition = Selectable.Transition.None;
            item.title = Txt("Name", marker, "SHIP", 28, TextAlignmentOptions.Center);
            Place(item.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(320, 36));
            item.title.fontStyle = FontStyles.Bold; item.title.raycastTarget = false;
            item.side = Txt("Info", marker, "", 22, TextAlignmentOptions.Center);
            Place(item.side.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -86), new Vector2(320, 30));
            item.side.color = Dim; item.side.raycastTarget = false;
            Set(so, "markerTemplate", item);

            //Header
            var header = Img("Header", window, new Color(0.35f, 0.07f, 0.06f, 0.85f)).rectTransform;
            header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
            header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(0, 90);
            var title = Txt("Status", header, "WEAPONS", 30, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.anchorMin = Vector2.zero; title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(28, 0); title.rectTransform.offsetMax = new Vector2(-120, 0);
            title.enableAutoSizing = true; title.fontSizeMax = 30; title.fontSizeMin = 20;
            Set(so, "headerText", title);
            Set(so, "closeButton", CloseButton(header));

            //Launchers, bottom left
            var texts = new TextMeshProUGUI[2]; var bars = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var panel = Img("Launcher" + i, window, new Color(0.04f, 0.05f, 0.07f, 0.85f));
                BottomLeft(panel, new Vector2(20, 20 + (1 - i) * 96), new Vector2(680, 86));
                panel.raycastTarget = false;
                texts[i] = Txt("Text", panel.rectTransform, "", 28, TextAlignmentOptions.MidlineLeft);
                TopLeft(texts[i], new Vector2(18, -4), new Vector2(650, 48)); texts[i].raycastTarget = false;
                texts[i].enableAutoSizing = true; texts[i].fontSizeMax = 28; texts[i].fontSizeMin = 20;
                bars[i] = Bar(panel.rectTransform, new Vector2(18, -56), new Vector2(644, 16), Orange);
            }
            SetArray(so, "launcherTexts", texts);
            SetArray(so, "launcherBars", bars);
            var hint = Txt("Hint", window, "", 28, TextAlignmentOptions.BottomRight);
            BottomRight(hint, new Vector2(-24, 24), new Vector2(900, 80)); hint.color = Orange; hint.raycastTarget = false;
            Set(so, "hintText", hint);

            //Target picture, full screen over the map
            var target = Img("TargetPanel", window, new Color(0.03f, 0.04f, 0.06f, 0.97f)).rectTransform;
            Stretch(target);
            Set(so, "targetPanel", target.gameObject);
            var pic = Raw("Picture", target);
            pic.rectTransform.anchorMin = Vector2.zero; pic.rectTransform.anchorMax = Vector2.one;
            pic.rectTransform.offsetMin = new Vector2(24, 24); pic.rectTransform.offsetMax = new Vector2(-620, -24);
            Set(so, "picture", pic);
            var zones = Rect("Zones", pic.rectTransform); Stretch(zones);
            Set(so, "zoneLayer", zones);
            var zone = Img("ZoneTemplate", zones, new Color(1f, 0.3f, 0.25f, 0.18f));
            Stretch(zone.rectTransform);
            var z = zone.gameObject.AddComponent<CrewUIItem>();
            z.bar = zone;
            var outline = zone.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(1f, 0.5f, 0.4f, 0.8f); outline.effectDistance = new Vector2(3, -3);
            z.select = zone.gameObject.AddComponent<Button>(); z.select.transition = Selectable.Transition.None;
            z.title = Txt("Name", zone.rectTransform, "ROOM", 30, TextAlignmentOptions.Center);
            Stretch(z.title.rectTransform); z.title.rectTransform.offsetMin = new Vector2(4, 30); z.title.fontStyle = FontStyles.Bold; z.title.raycastTarget = false;
            z.title.enableAutoSizing = true; z.title.fontSizeMax = 30; z.title.fontSizeMin = 18;
            z.side = Txt("State", zone.rectTransform, "", 24, TextAlignmentOptions.Center);
            Stretch(z.side.rectTransform); z.side.rectTransform.offsetMax = new Vector2(-4, -40); z.side.raycastTarget = false;
            z.side.enableAutoSizing = true; z.side.fontSizeMax = 24; z.side.fontSizeMin = 16;
            Set(so, "zoneTemplate", z);

            var col = Img("Info", target, PanelCol).rectTransform;
            col.anchorMin = new Vector2(1, 0); col.anchorMax = new Vector2(1, 1); col.pivot = new Vector2(1, 0.5f);
            col.offsetMin = new Vector2(-596, 24); col.offsetMax = new Vector2(-24, -24);
            var info = Txt("Text", col, "", 28, TextAlignmentOptions.TopLeft);
            info.rectTransform.anchorMin = Vector2.zero; info.rectTransform.anchorMax = Vector2.one;
            info.rectTransform.offsetMin = new Vector2(24, 340); info.rectTransform.offsetMax = new Vector2(-24, -24);
            info.enableAutoSizing = true; info.fontSizeMax = 28; info.fontSizeMin = 18;
            Set(so, "targetInfo", info);
            var scan = Btn("Scan", col, Blue, "SCAN", 34, Vector2.zero, new Vector2(524, 100), out var scanLabel);
            BottomLeft(scan, new Vector2(24, 196), new Vector2(524, 100));
            Set(so, "scanButton", scan); Set(so, "scanLabel", scanLabel);
            var scanBar = Bar(col, Vector2.zero, new Vector2(524, 18), Blue);
            BottomLeft(scanBar.transform.parent, new Vector2(24, 166), new Vector2(524, 18));
            Set(so, "scanBar", scanBar);
            var back = Btn("Back", col, TileCol, "BACK TO MAP", 32, Vector2.zero, new Vector2(524, 100), out _);
            BottomLeft(back, new Vector2(24, 24), new Vector2(524, 100));
            Set(so, "backButton", back);

            //Projectile options, over the picture
            var opts = Img("Options", target, new Color(0.06f, 0.07f, 0.1f, 0.97f)).rectTransform;
            Place(opts, new Vector2(0.5f, 0.5f), new Vector2(-300, 0), new Vector2(960, 540));
            opts.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.6f, 0.2f, 0.8f);
            Set(so, "optionsPanel", opts.gameObject);
            var ot = Txt("Title", opts, "Target", 38, TextAlignmentOptions.MidlineLeft);
            TopLeft(ot, new Vector2(30, -14), new Vector2(900, 64)); ot.fontStyle = FontStyles.Bold;
            Set(so, "optionsTitle", ot);
            var ob = new Button[3]; var ol = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                ob[i] = Btn("Option" + i, opts, Hex("6B2A1FFF"), "", 30, new Vector2(30, -92 - i * 124), new Vector2(900, 112), out ol[i]);
                ol[i].alignment = TextAlignmentOptions.MidlineLeft;
                ol[i].rectTransform.offsetMin = new Vector2(24, 0);
                ol[i].enableAutoSizing = true; ol[i].fontSizeMax = 30; ol[i].fontSizeMin = 20;
            }
            SetArray(so, "optionButtons", ob); SetArray(so, "optionLabels", ol);
            var cancel = Btn("Cancel", opts, TileCol, "CANCEL", 30, Vector2.zero, new Vector2(260, 80), out _);
            BottomRight(cancel, new Vector2(-30, 18), new Vector2(260, 80));
            Set(so, "optionsCancel", cancel);
            opts.gameObject.SetActive(false);
            target.gameObject.SetActive(false);

            so.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);
        }

        // =====================================================================
        // Navigation screen (HUD)
        // =====================================================================

        internal static void BuildNavigationScreen(Transform hud)
        {
            var oldHolder = hud.Find("NavigationScreen");
            if (oldHolder != null) Object.DestroyImmediate(oldHolder.gameObject);
            var holder = Rect("NavigationScreen", hud);
            Stretch(holder);
            var canvas = holder.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 50;
            holder.gameObject.AddComponent<GraphicRaycaster>();
            var screen = holder.gameObject.AddComponent<NavigationScreen>();
            var so = new SerializedObject(screen);
            var navBlue = new Color(0.3f, 0.65f, 0.95f, 1f);

            var window = Img("Window", holder, new Color(0.03f, 0.03f, 0.04f, 1f)).rectTransform;
            Stretch(window);
            Set(so, "window", window.gameObject);

            //The map, full screen; tapping it sets a course
            var map = Raw("Map", window);
            Stretch(map.rectTransform);
            map.raycastTarget = true;
            Set(so, "map", map);
            Set(so, "mapTap", map.gameObject.AddComponent<TacticalMapTap>());
            var markers = Rect("Markers", map.rectTransform); Stretch(markers);
            Set(so, "markerLayer", markers);

            //Course line and point (under the ship markers)
            var line = Img("CourseLine", markers, new Color(0.4f, 0.85f, 1f, 0.75f));
            Place(line.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 5));
            line.rectTransform.pivot = new Vector2(0f, 0.5f); line.raycastTarget = false;
            Set(so, "courseLine", line.rectTransform);
            var mark = Txt("CoursePoint", markers, "X", 54, TextAlignmentOptions.Center);
            Place(mark.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
            mark.color = new Color(0.4f, 0.85f, 1f); mark.fontStyle = FontStyles.Bold; mark.raycastTarget = false;
            Set(so, "courseMark", mark.rectTransform);

            var marker = Rect("MarkerTemplate", markers);
            Place(marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 190));
            var item = marker.gameObject.AddComponent<CrewUIItem>();
            item.highlight = Img("Ring", marker, new Color(1f, 0.3f, 0.25f, 0.9f), circle);
            Place(item.highlight.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            item.highlight.raycastTarget = false;
            var hole = Img("Hole", item.highlight.rectTransform, new Color(0, 0, 0, 0.55f), circle);
            Place(hole.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46)); hole.raycastTarget = false;
            item.title = Txt("Name", marker, "SHIP", 28, TextAlignmentOptions.Center);
            Place(item.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(320, 36));
            item.title.fontStyle = FontStyles.Bold; item.title.raycastTarget = false;
            item.side = Txt("Info", marker, "", 22, TextAlignmentOptions.Center);
            Place(item.side.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -86), new Vector2(320, 30));
            item.side.color = Dim; item.side.raycastTarget = false;
            Set(so, "markerTemplate", item);

            //Header
            var header = Img("Header", window, new Color(0.07f, 0.18f, 0.32f, 0.88f)).rectTransform;
            header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
            header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(0, 90);
            var title = Txt("Status", header, "NAVIGATION", 30, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.anchorMin = Vector2.zero; title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(28, 0); title.rectTransform.offsetMax = new Vector2(-120, 0);
            title.enableAutoSizing = true; title.fontSizeMax = 30; title.fontSizeMin = 20;
            Set(so, "headerText", title);
            Set(so, "closeButton", CloseButton(header));

            //Incoming warning, under the header
            var warn = Txt("Incoming", window, "", 44, TextAlignmentOptions.Center);
            TopCenter(warn, new Vector2(0, -104), new Vector2(900, 64)); warn.color = Red; warn.raycastTarget = false;
            Set(so, "warningText", warn);

            //Course and speed readout, above the buttons
            var infoPanel = Img("Info", window, new Color(0.04f, 0.05f, 0.07f, 0.85f));
            BottomLeft(infoPanel, new Vector2(20, 236), new Vector2(860, 120)); infoPanel.raycastTarget = false;
            var info = Txt("Text", infoPanel.rectTransform, "", 28, TextAlignmentOptions.MidlineLeft);
            Stretch(info.rectTransform); info.rectTransform.offsetMin = new Vector2(18, 4); info.rectTransform.offsetMax = new Vector2(-18, -4);
            info.enableAutoSizing = true; info.fontSizeMax = 28; info.fontSizeMin = 18; info.raycastTarget = false;
            Set(so, "infoText", info);

            //Course buttons, then throttle, bottom left
            string[] courses = { "ORBIT", "CLOSE IN", "KEEP AWAY", "STOP" };
            var cb = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                cb[i] = Btn("Course" + i, window, TileCol, courses[i], 30, Vector2.zero, new Vector2(205, 96), out _);
                BottomLeft(cb[i], new Vector2(20 + i * 218, 128), new Vector2(205, 96));
            }
            SetArray(so, "courseButtons", cb);
            string[] throttles = { "SLOW", "CRUISE", "FULL" };
            var tb = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                tb[i] = Btn("Throttle" + i, window, TileCol, throttles[i], 30, Vector2.zero, new Vector2(278, 96), out _);
                BottomLeft(tb[i], new Vector2(20 + i * 291, 20), new Vector2(278, 96));
            }
            SetArray(so, "throttleButtons", tb);

            //Evade and jump, bottom right
            var evade = Btn("Evade", window, Orange, "EVADE", 40, Vector2.zero, new Vector2(340, 150), out var evadeLabel);
            BottomRight(evade, new Vector2(-24, 156), new Vector2(340, 150));
            evadeLabel.fontStyle = FontStyles.Bold;
            Set(so, "evadeButton", evade); Set(so, "evadeLabel", evadeLabel);
            var evadeBar = Bar(window, Vector2.zero, new Vector2(340, 14), Orange);
            BottomRight(evadeBar.transform.parent, new Vector2(-24, 136), new Vector2(340, 14));
            Set(so, "evadeBar", evadeBar);
            var jump = Btn("Jump", window, TileCol, "PLOT JUMP", 32, Vector2.zero, new Vector2(340, 96), out var jumpLabel);
            BottomRight(jump, new Vector2(-24, 34), new Vector2(340, 96));
            Set(so, "jumpButton", jump); Set(so, "jumpLabel", jumpLabel);
            var jumpBar = Bar(window, Vector2.zero, new Vector2(340, 12), navBlue);
            BottomRight(jumpBar.transform.parent, new Vector2(-24, 16), new Vector2(340, 12));
            Set(so, "jumpBar", jumpBar);

            var hint = Txt("Hint", window, "", 28, TextAlignmentOptions.BottomRight);
            BottomRight(hint, new Vector2(-388, 236), new Vector2(620, 80)); hint.color = Orange; hint.raycastTarget = false;
            Set(so, "hintText", hint);

            so.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);
        }

        static RawImage Raw(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>(); raw.color = Color.white; raw.raycastTarget = false;
            return raw;
        }

        static Button CloseButton(RectTransform parent)
        {
            var close = Img("Close", parent, Color.white, circle);
            TopRight(close, new Vector2(-12, -6), new Vector2(78, 78));
            var cl = Txt("Label", close.rectTransform, "X", 40, TextAlignmentOptions.Center); Stretch(cl.rectTransform); cl.color = Color.black;
            return close.gameObject.AddComponent<Button>();
        }

        static Image Bar(RectTransform parent, Vector2 pos, Vector2 size, Color color)
        {
            var back = Img("BarBack", parent, new Color(0, 0, 0, 0.5f));
            TopLeft(back, pos, size); back.raycastTarget = false;
            var fill = Img("Fill", back.rectTransform, color);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0.5f; fill.raycastTarget = false;
            return fill;
        }

        static void SetArray<T>(SerializedObject so, string field, T[] values) where T : Object
        {
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
