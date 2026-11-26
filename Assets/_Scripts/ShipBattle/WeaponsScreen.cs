using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Crew.UI;

namespace Rambler.Crew.Battle
{
    //The Weapons station's screen: the local tactical map, full screen, looking straight down with our ship's light
    //at the centre and every other ship as a light moving over the Titan surface; missiles and rockets fly across it.
    //  Tap the enemy's light: a full-screen picture of that ship. Unscanned, it shows little and only the hull can be
    //    hit; SCAN it (takes a few seconds, faster with skill) to see its rooms and systems.
    //  Tap a part of the picture: the projectile options, each with its chance of success and rounds left. Better
    //    gunners can pick out more rooms. Firing returns to the map to watch the shot land.
    //The battle runs on the whole time. Only the active player standing at the Weapons station can open it.
    //Built by Tools > Company > Build Ship Battle.
    public class WeaponsScreen : MonoBehaviour
    {
        public static WeaponsScreen Instance { get; private set; }
        public static WeaponsScreen Find() => Instance != null ? Instance : FindObjectOfType<WeaponsScreen>(true);

        [SerializeField] GameObject window;
        [SerializeField] Button closeButton;
        [SerializeField] TextMeshProUGUI headerText;
        [SerializeField] RawImage map;
        [SerializeField] RectTransform markerLayer;
        [Tooltip("A ship's marker over its light: select (tap), title (name), side (range and speed).")]
        [SerializeField] CrewUIItem markerTemplate;
        [Tooltip("Missiles, Rockets: a line of text and a charge bar each.")]
        [SerializeField] TextMeshProUGUI[] launcherTexts = new TextMeshProUGUI[2];
        [SerializeField] Image[] launcherBars = new Image[2];
        [SerializeField] TextMeshProUGUI hintText;

        [Header("Target picture")]
        [SerializeField] GameObject targetPanel;
        [SerializeField] RawImage picture;
        [SerializeField] RectTransform zoneLayer;
        [Tooltip("A room's zone on the picture: select, title (name), side (state), bar (fill tint).")]
        [SerializeField] CrewUIItem zoneTemplate;
        [SerializeField] TextMeshProUGUI targetInfo;
        [SerializeField] Button scanButton;
        [SerializeField] TextMeshProUGUI scanLabel;
        [SerializeField] Image scanBar;
        [SerializeField] Button backButton;

        [Header("Projectile options")]
        [SerializeField] GameObject optionsPanel;
        [SerializeField] TextMeshProUGUI optionsTitle;
        [SerializeField] Button[] optionButtons = new Button[3];
        [SerializeField] TextMeshProUGUI[] optionLabels = new TextMeshProUGUI[3];
        [SerializeField] Button optionsCancel;

        [Header("Map camera")]
        [Tooltip("Half the map's height in map units.")]
        [SerializeField] float mapSize = 320f;
        [SerializeField] float cameraHeight = 400f;
        [Tooltip("Map units per kilometre, for the range readout.")]
        [SerializeField] float unitsPerKm = 100f;
        [SerializeField] Color background = new Color(0.08f, 0.07f, 0.06f);

        Camera mapCam, pictureCam;
        RenderTexture mapRT, pictureRT;
        readonly List<CrewUIItem> markers = new List<CrewUIItem>();
        readonly List<CrewUIItem> zones = new List<CrewUIItem>();
        EnemyRoom aimed;
        float timer;
        bool pictureScanned;

        public bool IsOpen => window != null && window.activeSelf;
        static ShipBattle Battle => ShipBattle.Instance;

        void Awake()
        {
            Instance = this;
            if (window != null) window.SetActive(false);
            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
            if (zoneTemplate != null) zoneTemplate.gameObject.SetActive(false);
            Wire(closeButton, Close);
            Wire(backButton, CloseTarget);
            Wire(optionsCancel, () => { aimed = null; if (optionsPanel != null) optionsPanel.SetActive(false); });
            Wire(scanButton, () => { if (Battle != null) Battle.StartScan(); RefreshTarget(); });
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                Wire(optionButtons[i], () => Fire(index));
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (mapCam != null) Destroy(mapCam.gameObject);
            if (pictureCam != null) Destroy(pictureCam.gameObject);
            if (mapRT != null) mapRT.Release();
            if (pictureRT != null) pictureRT.Release();
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }

        public void Open()
        {
            var b = Battle;
            if (window == null || b == null || !b.IsPlayerControlled(Station.Weapons)) return;
            window.SetActive(true);
            if (targetPanel != null) targetPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            SetUpMapCamera();
            Hint(b.HasTacticalMap ? "Tap the enemy's light to target it." : "No tactical map in this scene.");
            Refresh();
        }

        public void Close()
        {
            if (window != null) window.SetActive(false);
            if (mapCam != null) mapCam.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;
            var b = Battle;
            if (b == null || b.State != BattleState.Fighting || !b.IsPlayerControlled(Station.Weapons)) { Close(); return; }
            timer += Time.unscaledDeltaTime;
            if (timer < 0.15f) return;
            timer = 0f;
            Refresh();
        }

        //The map follows our ship, and the markers follow the lights, every frame
        void LateUpdate()
        {
            if (!IsOpen || Battle == null || !Battle.HasTacticalMap || mapCam == null) return;
            var me = Battle.PlayerShip;
            mapCam.transform.position = me.Position + Vector3.up * cameraHeight;
            PlaceMarkers();
        }

        // ---------- Map ----------

        void SetUpMapCamera()
        {
            if (Battle == null || !Battle.HasTacticalMap) return;
            if (mapCam == null)
            {
                mapCam = new GameObject("TacticalCamera").AddComponent<Camera>();
                mapCam.orthographic = true;
                mapCam.clearFlags = CameraClearFlags.SolidColor;
                mapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            mapCam.gameObject.SetActive(true);
            mapCam.backgroundColor = background;
            mapCam.orthographicSize = mapSize;
            mapCam.nearClipPlane = 1f;
            mapCam.farClipPlane = cameraHeight + 100f;
            mapRT = Fit(mapRT, map, "TacticalMap");
            mapCam.targetTexture = mapRT;
            mapCam.aspect = (float)mapRT.width / mapRT.height;
            map.texture = mapRT;
        }

        //A render texture the size of a UI image
        static RenderTexture Fit(RenderTexture rt, RawImage image, string name)
        {
            var rect = image.rectTransform.rect;
            var canvas = image.canvas != null ? image.canvas.rootCanvas : null;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            int w = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 256, 2048), h = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 256, 2048);
            if (rt != null && rt.width == w && rt.height == h) return rt;
            if (rt != null) rt.Release();
            return new RenderTexture(w, h, 24) { name = name };
        }

        void PlaceMarkers()
        {
            var ships = new[] { Battle.PlayerShip, Battle.EnemyShip };
            while (markers.Count < ships.Length)
            {
                var m = Instantiate(markerTemplate, markerLayer);
                markers.Add(m);
            }
            for (int i = 0; i < ships.Length; i++)
            {
                var ship = ships[i];
                var m = markers[i];
                if (ship == null) { m.gameObject.SetActive(false); continue; }
                Vector3 v = mapCam.WorldToViewportPoint(ship.Position);
                bool onMap = v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
                m.gameObject.SetActive(true);
                var r = (RectTransform)m.transform;
                Vector2 pos = new Vector2(Mathf.Clamp(v.x, 0.04f, 0.96f), Mathf.Clamp(v.y, 0.06f, 0.94f));
                r.anchorMin = r.anchorMax = pos;
                r.anchoredPosition = Vector2.zero;
                bool enemy = ship.Team == TacticalShip.Side.Enemy;
                CrewUIItem.SetText(m.title, ship.DisplayName.ToUpperInvariant() + (onMap ? "" : "  ›"));
                CrewUIItem.SetText(m.side, enemy ? Km(Battle.Range) + "   ·   speed " + Mathf.RoundToInt(ship.Speed) : "you   ·   speed " + Mathf.RoundToInt(ship.Speed));
                if (m.select != null)
                {
                    m.select.interactable = enemy;
                    m.select.onClick.RemoveAllListeners();
                    if (enemy) m.select.onClick.AddListener(OpenTarget);
                }
                if (m.highlight != null) m.highlight.color = enemy ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(0.4f, 0.85f, 1f, 0.9f);
            }
        }

        string Km(float units) => (units / unitsPerKm).ToString("0.0") + " km";

        void Refresh()
        {
            var b = Battle;
            if (b == null) return;
            var t = b.Terminal(Station.Weapons);
            string scan = b.Scanned ? "<color=#59CC73>scanned</color>" : b.Scanning ? "scanning " + Mathf.RoundToInt(b.ScanProgress * 100f) + "%" : "<color=#E64033>not scanned</color>";
            Set(headerText, "<b>WEAPONS</b>   " + (t != null && t.OperatorName != null ? t.OperatorName + ": " + t.SkillName + " " + t.OperatorSkill : "") +
                            "   ·   " + b.EnemyName + " " + scan + "   ·   range " + Km(b.Range) + (b.Range > b.WeaponRange ? " <color=#E64033>(too far)</color>" : "") +
                            "   ·   fuel " + Mathf.RoundToInt(b.Fuel / Mathf.Max(1f, b.MaxFuel) * 100f) + "%");
            for (int i = 0; i < launcherTexts.Length; i++)
            {
                var l = i < b.Launchers.Count ? b.Launchers[i] : null;
                if (launcherTexts[i] != null) launcherTexts[i].transform.parent.gameObject.SetActive(l != null);
                if (l == null) continue;
                string why = b.NotReady(l);
                Set(launcherTexts[i], "<b>" + l.name.ToUpperInvariant() + "</b>  " + l.left + " / " + l.rounds + "   " + (why == null ? "<color=#59CC73>READY</color>" : why));
                if (i < launcherBars.Length && launcherBars[i] != null) launcherBars[i].fillAmount = l.left > 0 ? l.charge : 0f;
            }
            if (targetPanel != null && targetPanel.activeSelf) RefreshTarget();
        }

        // ---------- Target picture ----------

        void OpenTarget()
        {
            var b = Battle;
            if (b == null || targetPanel == null) return;
            targetPanel.SetActive(true);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            aimed = null;
            TakePicture();
            BuildZones();
            RefreshTarget();
        }

        void CloseTarget()
        {
            if (targetPanel != null) targetPanel.SetActive(false);
            aimed = null;
        }

        //Photographs the enemy ship's model from above, nose to the right
        void TakePicture()
        {
            var space = Battle.Space;
            if (space == null || space.portrait == null || picture == null) return;
            var renderers = space.portrait.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds all = renderers[0].bounds;
            foreach (var r in renderers) all.Encapsulate(r.bounds);
            if (pictureCam == null)
            {
                pictureCam = new GameObject("TargetPictureCamera").AddComponent<Camera>();
                pictureCam.orthographic = true;
                pictureCam.clearFlags = CameraClearFlags.SolidColor;
                pictureCam.enabled = false;
            }
            pictureCam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            pictureRT = Fit(pictureRT, picture, "TargetPicture");
            pictureCam.targetTexture = pictureRT;
            pictureCam.aspect = (float)pictureRT.width / pictureRT.height;
            //Looking down with the ship's length across the picture (its longer side, nose to the right)
            Vector3 nose = all.size.x > all.size.z ? space.portrait.right : space.portrait.forward;
            nose.y = 0f; nose = nose.sqrMagnitude > 0.001f ? nose.normalized : Vector3.forward;
            pictureCam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.Cross(nose, Vector3.up));
            pictureCam.transform.position = all.center + Vector3.up * (all.extents.y + 50f);
            pictureCam.nearClipPlane = 1f;
            pictureCam.farClipPlane = all.size.y + 100f;
            //Fit the ship's length across the picture
            Vector3 right = pictureCam.transform.right, up = pictureCam.transform.up;
            float halfW = 0f, halfH = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3(i % 2 == 0 ? all.min.x : all.max.x, (i / 2) % 2 == 0 ? all.min.y : all.max.y, i < 4 ? all.min.z : all.max.z) - all.center;
                halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(c, right)));
                halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(c, up)));
            }
            pictureCam.orthographicSize = Mathf.Max(halfH, halfW / pictureCam.aspect) * 1.08f;
            pictureCam.Render();
            picture.texture = pictureRT;
            //Where the ship sits in the picture, so room areas (0-1 along the ship) land on it
            Vector3 lo = pictureCam.WorldToViewportPoint(all.center - right * halfW - up * halfH);
            Vector3 hi = pictureCam.WorldToViewportPoint(all.center + right * halfW + up * halfH);
            shipInPicture = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
        }

        Rect shipInPicture = new Rect(0f, 0f, 1f, 1f);

        void BuildZones()
        {
            foreach (var z in zones) if (z != null) Destroy(z.gameObject);
            zones.Clear();
            zoneRooms.Clear();
            var b = Battle;
            pictureScanned = b.Scanned;
            if (zoneTemplate == null) return;
            if (!b.Scanned)
            {
                //Unscanned: the whole ship is one target, the hull
                AddZone(new EnemyRoom { name = "Hull", system = EnemyTarget.Hull, area = new Rect(0f, 0f, 1f, 1f), minSkill = 0 });
                return;
            }
            foreach (var room in b.EnemyRooms) AddZone(room);
        }

        void AddZone(EnemyRoom room)
        {
            var z = Instantiate(zoneTemplate, zoneLayer);
            z.gameObject.SetActive(true);
            var r = (RectTransform)z.transform;
            var s = shipInPicture;
            r.anchorMin = new Vector2(s.x + room.area.xMin * s.width, s.y + room.area.yMin * s.height);
            r.anchorMax = new Vector2(s.x + room.area.xMax * s.width, s.y + room.area.yMax * s.height);
            r.offsetMin = new Vector2(3, 3); r.offsetMax = new Vector2(-3, -3);
            var target = room;
            if (z.select != null) { z.select.onClick.RemoveAllListeners(); z.select.onClick.AddListener(() => Aim(target)); }
            z.name = "Zone_" + room.name;
            zones.Add(z);
            zoneRooms[z] = room;
        }

        readonly Dictionary<CrewUIItem, EnemyRoom> zoneRooms = new Dictionary<CrewUIItem, EnemyRoom>();

        void RefreshTarget()
        {
            var b = Battle;
            if (b == null) return;
            if (pictureScanned != b.Scanned) BuildZones(); //The scan just finished: the rooms appear

            foreach (var z in zones)
            {
                if (z == null || !zoneRooms.TryGetValue(z, out var room)) continue;
                bool can = b.CanAim(room);
                CrewUIItem.SetText(z.title, room.name.ToUpperInvariant());
                CrewUIItem.SetText(z.side, can ? SystemState(b, room.system) : "needs skill " + room.minSkill);
                if (z.select != null) z.select.interactable = can;
                if (z.bar != null) z.bar.color = !can ? new Color(0.2f, 0.2f, 0.2f, 0.35f) : aimed == room ? new Color(1f, 0.6f, 0.15f, 0.45f) : new Color(1f, 0.3f, 0.25f, 0.18f);
            }

            var sb = new StringBuilder();
            sb.Append("<b>").Append(b.EnemyName.ToUpperInvariant()).Append("</b>\n");
            sb.Append("Hull ").Append(b.EnemyHull).Append(" / ").Append(b.EnemyMaxHull).Append('\n');
            var foe = b.EnemyShip;
            sb.Append("Range ").Append(Km(b.Range)).Append("   ·   speed ").Append(foe != null ? Mathf.RoundToInt(foe.Speed) : 0).Append('\n');
            if (b.Scanned)
            {
                sb.Append("\nWeapons ").Append(b.EnemyWeaponsDown ? "<color=#59CC73>DOWN</color>" : b.EnemyWeapons + "").Append('\n');
                sb.Append("Engines ").Append(b.EnemyEnginesDown ? "<color=#59CC73>DOWN</color>" : b.EnemyEngines + "").Append('\n');
                sb.Append("Bridge ").Append(b.EnemyBridgeDown ? "<color=#59CC73>DOWN</color>" : b.EnemyBridge + "").Append('\n');
                sb.Append("\nArmed with rocket batteries and boarding pods.\n");
                sb.Append("<color=#999999>Weapons down: no volleys. Engines down: dead in space, no boarders. Bridge down: can't steer or dodge.</color>");
            }
            else sb.Append("\n<color=#E64033>Not scanned.</color> Only the hull can be hit, and less often.\nScan it to see its rooms and systems.");
            Set(targetInfo, sb.ToString());

            Set(scanLabel, b.Scanned ? "SCANNED" : b.Scanning ? "SCANNING " + Mathf.RoundToInt(b.ScanProgress * 100f) + "%" : "SCAN");
            if (scanButton != null) scanButton.interactable = !b.Scanned && !b.Scanning;
            if (scanBar != null) scanBar.fillAmount = b.Scanned ? 1f : b.ScanProgress;

            if (aimed != null && optionsPanel != null && optionsPanel.activeSelf) RefreshOptions();
        }

        static string SystemState(ShipBattle b, EnemyTarget t)
        {
            switch (t)
            {
                case EnemyTarget.Weapons: return b.EnemyWeaponsDown ? "DOWN" : b.EnemyWeapons + " / " + b.EnemySystemMax;
                case EnemyTarget.Engines: return b.EnemyEnginesDown ? "DOWN" : b.EnemyEngines + " / " + b.EnemySystemMax;
                case EnemyTarget.Bridge: return b.EnemyBridgeDown ? "DOWN" : b.EnemyBridge + " / " + b.EnemySystemMax;
                default: return "hull " + b.EnemyHull;
            }
        }

        // ---------- Projectile options ----------

        void Aim(EnemyRoom room)
        {
            aimed = room;
            if (optionsPanel != null) optionsPanel.SetActive(true);
            RefreshOptions();
            RefreshTarget();
        }

        void RefreshOptions()
        {
            var b = Battle;
            if (b == null || aimed == null) return;
            Set(optionsTitle, "Target: " + aimed.name + (aimed.system == EnemyTarget.Hull ? " (hull)" : ""));
            for (int i = 0; i < optionButtons.Length; i++)
            {
                var l = i < b.Launchers.Count ? b.Launchers[i] : null;
                if (optionButtons[i] == null) continue;
                optionButtons[i].gameObject.SetActive(l != null);
                if (l == null) continue;
                string why = b.NotReady(l);
                int pct = Mathf.RoundToInt(b.Chance(l, aimed.system) * 100f);
                Set(i < optionLabels.Length ? optionLabels[i] : null,
                    "<b>" + l.name.ToUpperInvariant() + "</b>  ×" + l.salvo + "     " + pct + "% to hit" + (l.salvo > 1 ? " each" : "") +
                    "     " + l.left + " left\n<size=80%>" + (why == null ? "<color=#59CC73>READY: tap to fire</color>" : why) + "</size>");
                optionButtons[i].interactable = why == null;
            }
        }

        void Fire(int index)
        {
            var b = Battle;
            if (b == null || aimed == null || index >= b.Launchers.Count) return;
            if (!b.FireAt(b.Launchers[index], aimed.system)) { RefreshOptions(); return; }
            //Back to the map to watch it fly
            if (optionsPanel != null) optionsPanel.SetActive(false);
            CloseTarget();
            Hint("Shot away. Tap the enemy's light to fire again.");
            Refresh();
        }

        void Hint(string s) => Set(hintText, s);
        static void Set(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
    }
}
