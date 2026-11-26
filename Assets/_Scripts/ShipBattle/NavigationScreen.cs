using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Crew.UI;

namespace Rambler.Crew.Battle
{
    //The Navigation station's screen: the same full-screen top-down tactical map as Weapons, our ship's light at the
    //centre. Tap anywhere on the map to set a course point (a line shows the way); or pick a course: ORBIT the enemy at
    //range, CLOSE IN, KEEP AWAY, or STOP. SLOW / CRUISE / FULL set the throttle: speed is evasion, but fuel burns
    //faster the harder the ship is pushed. EVADE throws the ship into a hard turn and burn (fuel, then a cooldown);
    //hit it while a missile is closing for a good chance to slip it. JUMP plots (or cancels) the escape jump.
    //The battle runs on the whole time. Only the active player standing at the Navigation station can open it.
    //Built by Tools > Company > Build Ship Battle.
    public class NavigationScreen : MonoBehaviour
    {
        public static NavigationScreen Instance { get; private set; }
        public static NavigationScreen Find() => Instance != null ? Instance : FindObjectOfType<NavigationScreen>(true);

        [SerializeField] GameObject window;
        [SerializeField] Button closeButton;
        [SerializeField] TextMeshProUGUI headerText;
        [SerializeField] RawImage map;
        [SerializeField] TacticalMapTap mapTap;
        [SerializeField] RectTransform markerLayer;
        [Tooltip("A ship's marker over its light: title (name), side (range and speed), highlight (ring).")]
        [SerializeField] CrewUIItem markerTemplate;
        [Tooltip("A thin image from our ship to the course point.")]
        [SerializeField] RectTransform courseLine;
        [SerializeField] RectTransform courseMark;
        [SerializeField] TextMeshProUGUI infoText;
        [SerializeField] TextMeshProUGUI warningText;
        [SerializeField] TextMeshProUGUI hintText;

        [Header("Controls")]
        [Tooltip("Orbit, Close in, Keep away, Stop.")]
        [SerializeField] Button[] courseButtons = new Button[4];
        [Tooltip("Slow, Cruise, Full.")]
        [SerializeField] Button[] throttleButtons = new Button[3];
        [SerializeField] Button evadeButton;
        [SerializeField] TextMeshProUGUI evadeLabel;
        [SerializeField] Image evadeBar;
        [SerializeField] Button jumpButton;
        [SerializeField] TextMeshProUGUI jumpLabel;
        [SerializeField] Image jumpBar;

        [Header("Map camera")]
        [Tooltip("Half the map's height in map units.")]
        [SerializeField] float mapSize = 420f;
        [SerializeField] float cameraHeight = 400f;
        [Tooltip("Map units per kilometre, for the range readout.")]
        [SerializeField] float unitsPerKm = 100f;
        [SerializeField] Color background = new Color(0.08f, 0.07f, 0.06f);

        static readonly Course[] Courses = { Course.Orbit, Course.CloseIn, Course.KeepAway, Course.Stop };
        static readonly Color Lit = new Color(0.3f, 0.65f, 0.95f, 1f), Unlit = new Color(0.16f, 0.24f, 0.32f, 0.92f);

        Camera mapCam;
        RenderTexture mapRT;
        readonly List<CrewUIItem> markers = new List<CrewUIItem>();
        float timer, flash;

        public bool IsOpen => window != null && window.activeSelf;
        static ShipBattle Battle => ShipBattle.Instance;

        void Awake()
        {
            Instance = this;
            if (window != null) window.SetActive(false);
            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
            Wire(closeButton, Close);
            for (int i = 0; i < courseButtons.Length && i < Courses.Length; i++)
            {
                var c = Courses[i];
                Wire(courseButtons[i], () => { if (Battle != null) Battle.SetCourse(c); Refresh(); });
            }
            for (int i = 0; i < throttleButtons.Length; i++)
            {
                var t = (Throttle)i;
                Wire(throttleButtons[i], () => { if (Battle != null) Battle.SetThrottle(t); Refresh(); });
            }
            Wire(evadeButton, () => { if (Battle != null && Battle.StartEvade()) flash = 0.6f; Refresh(); });
            Wire(jumpButton, () => { if (Battle != null) Battle.ToggleEscape(); Refresh(); });
            if (mapTap != null) mapTap.Tapped = OnMapTapped;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (mapCam != null) Destroy(mapCam.gameObject);
            if (mapRT != null) mapRT.Release();
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }

        public void Open()
        {
            var b = Battle;
            if (window == null || b == null || !b.IsPlayerControlled(Station.Navigation)) return;
            window.SetActive(true);
            SetUpMapCamera();
            Hint(b.HasTacticalMap ? "Tap the map to set a course, or pick one below." : "No tactical map in this scene.");
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
            if (b == null || b.State != BattleState.Fighting || !b.IsPlayerControlled(Station.Navigation)) { Close(); return; }
            if (flash > 0f) flash -= Time.unscaledDeltaTime;
            timer += Time.unscaledDeltaTime;
            if (timer < 0.12f) return;
            timer = 0f;
            Refresh();
        }

        //The map follows our ship; the markers and the course line follow the lights, every frame
        void LateUpdate()
        {
            if (!IsOpen || Battle == null || !Battle.HasTacticalMap || mapCam == null) return;
            mapCam.transform.position = Battle.PlayerShip.Position + Vector3.up * cameraHeight;
            PlaceMarkers();
            PlaceCourse();
        }

        // ---------- Map ----------

        void SetUpMapCamera()
        {
            if (Battle == null || !Battle.HasTacticalMap || map == null) return;
            if (mapCam == null)
            {
                mapCam = new GameObject("NavigationCamera").AddComponent<Camera>();
                mapCam.orthographic = true;
                mapCam.clearFlags = CameraClearFlags.SolidColor;
                mapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            mapCam.gameObject.SetActive(true);
            mapCam.backgroundColor = background;
            mapCam.orthographicSize = mapSize;
            mapCam.nearClipPlane = 1f;
            mapCam.farClipPlane = cameraHeight + 100f;
            mapRT = Fit(mapRT, map, "NavigationMap");
            mapCam.targetTexture = mapRT;
            mapCam.aspect = (float)mapRT.width / mapRT.height;
            map.texture = mapRT;
        }

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

        //A tap on the map: fly there
        void OnMapTapped(Vector2 viewport)
        {
            var b = Battle;
            if (b == null || mapCam == null || !b.HasTacticalMap) return;
            Vector3 p = mapCam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, cameraHeight));
            b.SetWaypoint(p);
            Hint(b.CanSteer ? "Course set: " + Km(Vector3.Distance(Flat(p), Flat(b.PlayerShip.Position))) + " out." : "Course set, but the helm isn't answering.");
            Refresh();
        }

        //Where a map point sits on the map image, in the image's own units (from its centre)
        Vector2 OnImage(Vector3 world, out Vector3 viewport)
        {
            viewport = mapCam.WorldToViewportPoint(world);
            var size = map.rectTransform.rect.size;
            return new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);
        }

        void PlaceMarkers()
        {
            var ships = new[] { Battle.PlayerShip, Battle.EnemyShip };
            while (markers.Count < ships.Length) markers.Add(Instantiate(markerTemplate, markerLayer));
            for (int i = 0; i < ships.Length; i++)
            {
                var ship = ships[i];
                var m = markers[i];
                if (ship == null) { m.gameObject.SetActive(false); continue; }
                Vector3 v = mapCam.WorldToViewportPoint(ship.Position);
                bool onMap = v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
                m.gameObject.SetActive(true);
                var r = (RectTransform)m.transform;
                r.anchorMin = r.anchorMax = new Vector2(Mathf.Clamp(v.x, 0.04f, 0.96f), Mathf.Clamp(v.y, 0.06f, 0.94f));
                r.anchoredPosition = Vector2.zero;
                bool enemy = ship.Team == TacticalShip.Side.Enemy;
                CrewUIItem.SetText(m.title, ship.DisplayName.ToUpperInvariant() + (onMap ? "" : "  ›"));
                CrewUIItem.SetText(m.side, (enemy ? Km(Battle.Range) : "you") + "   ·   speed " + Mathf.RoundToInt(ship.Speed));
                if (m.highlight != null) m.highlight.color = enemy ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(0.4f, 0.85f, 1f, 0.9f);
            }
        }

        //A line from our ship to the course point while one is set
        void PlaceCourse()
        {
            var b = Battle;
            bool show = b.Course == Course.Waypoint && !b.Escaping;
            if (courseLine != null) courseLine.gameObject.SetActive(show);
            if (courseMark != null) courseMark.gameObject.SetActive(show);
            if (!show) return;
            Vector2 from = OnImage(b.PlayerShip.Position, out _);
            Vector2 to = OnImage(b.Waypoint, out _);
            if (courseMark != null) { courseMark.anchorMin = courseMark.anchorMax = new Vector2(0.5f, 0.5f); courseMark.anchoredPosition = to; }
            if (courseLine == null) return;
            Vector2 d = to - from;
            courseLine.anchorMin = courseLine.anchorMax = new Vector2(0.5f, 0.5f);
            courseLine.pivot = new Vector2(0f, 0.5f);
            courseLine.anchoredPosition = from;
            courseLine.sizeDelta = new Vector2(d.magnitude, courseLine.sizeDelta.y);
            courseLine.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        string Km(float units) => (units / unitsPerKm).ToString("0.0") + " km";
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ---------- Readouts and controls ----------

        void Refresh()
        {
            var b = Battle;
            if (b == null) return;
            var t = b.Terminal(Station.Navigation);
            var me = b.PlayerShip;
            float fuelPct = b.Fuel / Mathf.Max(1f, b.MaxFuel) * 100f;
            Set(headerText, "<b>NAVIGATION</b>   " + (t != null && t.OperatorName != null ? t.OperatorName + ": " + t.SkillName + " " + t.OperatorSkill : "") +
                            "   ·   range " + Km(b.Range) + (b.Range > b.WeaponRange ? " <color=#E64033>(out of weapon range)</color>" : "") +
                            "   ·   fuel " + (fuelPct < 20f ? "<color=#E64033>" + Mathf.RoundToInt(fuelPct) + "%</color>" : Mathf.RoundToInt(fuelPct) + "%"));

            string course = b.Escaping ? "<color=#F29E33>running for the jump</color>" :
                b.Course == Course.Waypoint ? (b.WaypointReached ? "circling the course point" : "to the course point, " + Km(Vector3.Distance(Flat(b.Waypoint), Flat(me != null ? me.Position : Vector3.zero)))) :
                b.Course == Course.Orbit ? "orbit at " + Km(b.OrbitRange) :
                b.Course == Course.CloseIn ? "close in to " + Km(b.CloseRange) :
                b.Course == Course.KeepAway ? "keep away at " + Km(b.AwayRange) : "all stop";
            string helm = !b.CanSteer ? "\n<color=#E64033>" + (b.Fuel <= 0f ? "Out of fuel: drifting." : b.IsDamaged(Station.Navigation) ? "Navigation damaged: drifting." : "The helm isn't answering: drifting.") + "</color>" : "";
            Set(infoText, "Course: <b>" + course + "</b>\nSpeed " + (me != null ? Mathf.RoundToInt(me.Speed) : 0) + " / " + (me != null ? Mathf.RoundToInt(me.maxSpeed) : 0) +
                          "   ·   evading <b>" + Mathf.RoundToInt(b.Evade * 100f) + "%</b>" + (b.Evading ? "  <color=#59CC73>+ burn " + Mathf.RoundToInt(b.EvadeChance * 100f) + "% dodge</color>" : "") + helm);

            //Incoming fire: how many and how soon
            int count = 0;
            foreach (var m in b.Incoming) if (m != null) count++;
            b.NearestIncoming(out _, out float eta);
            Set(warningText, count > 0 ? "<b>INCOMING ×" + count + "</b>   " + eta.ToString("0.0") + "s" : "");
            if (warningText != null) warningText.alpha = count > 0 ? (Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.5f + 0.5f) : 1f;

            for (int i = 0; i < courseButtons.Length && i < Courses.Length; i++) Tint(courseButtons[i], !b.Escaping && b.Course == Courses[i]);
            for (int i = 0; i < throttleButtons.Length; i++) Tint(throttleButtons[i], (int)b.Throttle == i);

            string why = b.EvadeNotReady;
            Set(evadeLabel, b.Evading || flash > 0f ? "EVADING!" : why == null ? "EVADE" + (count > 0 ? "  NOW!" : "") : "EVADE\n<size=60%>" + why + "</size>");
            if (evadeButton != null)
            {
                evadeButton.interactable = why == null;
                var img = evadeButton.targetGraphic as Image;
                if (img != null) img.color = why == null ? (count > 0 ? new Color(0.95f, 0.55f, 0.15f) : new Color(0.75f, 0.42f, 0.12f)) : new Color(0.3f, 0.25f, 0.2f);
            }
            if (evadeBar != null) evadeBar.fillAmount = b.EvadeCharge;

            float nav = b.Effect(Station.Navigation);
            Set(jumpLabel, b.Escaping ? "CANCEL JUMP\n<size=60%>" + Mathf.RoundToInt(b.EscapeCharge * 100f) + "%" + (nav > 0f ? "" : " · no power") + "</size>"
                                      : "PLOT JUMP\n<size=60%>" + Mathf.RoundToInt(b.EscapeCharge * 100f) + "% charged</size>");
            if (jumpBar != null) jumpBar.fillAmount = b.EscapeCharge;
        }

        static void Tint(Button btn, bool on)
        {
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            if (img != null) img.color = on ? Lit : Unlit;
        }

        void Hint(string s) => Set(hintText, s);
        static void Set(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
    }
}
