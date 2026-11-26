using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Ship;

namespace Rambler.Crew.Battle
{
    //The Ship Systems station's screen: a live camera looking straight down on the whole ship, with a tap zone over
    //each room showing its alerts (fire, breach, intruders, EMP, damage, no power, low oxygen). Tap a room to see what
    //it does and what's wrong, then act: seal or open its doors, vent its air (kills fires, and boarders' breathing)
    //or let it back in, or send a crew member there (crew in a room fight its fire, patch its breach and repair its
    //station by hand). Only the active player standing at the Systems station can open it; the battle runs on.
    //Built by Tools > Company > Build Ship Battle.
    public class SystemsScreen : MonoBehaviour
    {
        public static SystemsScreen Instance { get; private set; }
        public static SystemsScreen Find() => Instance != null ? Instance : FindObjectOfType<SystemsScreen>(true);

        [SerializeField] GameObject window;
        [SerializeField] Button closeButton;
        [SerializeField] TextMeshProUGUI headerText;
        [SerializeField] RawImage view;
        [Tooltip("Stretched over the view; the room zones are placed in it by viewport position.")]
        [SerializeField] RectTransform zoneLayer;
        [SerializeField] SystemsRoomZone zoneTemplate;

        [Header("Selected room")]
        [SerializeField] TextMeshProUGUI roomTitle;
        [SerializeField] TextMeshProUGUI roomInfo;
        [SerializeField] Button sealButton;
        [SerializeField] Button openDoorsButton;
        [SerializeField] Button ventButton;
        [SerializeField] Button restoreButton;
        [SerializeField] Button sendCrewButton;
        [SerializeField] TextMeshProUGUI actionText;

        [Header("Camera")]
        [Tooltip("Turn of the view: -90 puts the ship's nose to the right.")]
        [SerializeField] float yaw = -90f;
        [SerializeField] float margin = 1.5f;
        [SerializeField] Color background = new Color(0.03f, 0.04f, 0.06f);
        [SerializeField] Color quietTint = new Color(0.3f, 0.6f, 0.9f, 0.06f);

        Camera cam;
        RenderTexture rt;
        readonly List<SystemsRoomZone> zones = new List<SystemsRoomZone>();
        readonly List<RoomAlert> alerts = new List<RoomAlert>();
        SystemsRoomZone selected;
        ShipBattle hooked;
        float timer;

        public bool IsOpen => window != null && window.activeSelf;
        static ShipBattle Battle => ShipBattle.Instance;

        void Awake()
        {
            Instance = this;
            if (window != null) window.SetActive(false);
            if (zoneTemplate != null) zoneTemplate.gameObject.SetActive(false);
            Wire(closeButton, Close);
            Wire(sealButton, () => Act(r => { r.SetDoorsSealed(true); return "Doors sealed."; }));
            Wire(openDoorsButton, () => Act(r => { r.SetDoorsSealed(false); return "Doors released."; }));
            Wire(ventButton, () => Act(r => { r.VentOxygen(); return "Venting the air. Anyone inside will choke; fires die."; }));
            Wire(restoreButton, () => Act(r => { r.RestoreAtmosphere(); return "Letting the air back in."; }));
            Wire(sendCrewButton, () => Act(r =>
            {
                var who = Battle.SendCrew(r);
                return who != null ? who.name + " is on the way." : "Nobody free to send.";
            }));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unhook();
            if (cam != null) Destroy(cam.gameObject);
            if (rt != null) rt.Release();
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }

        public void Open()
        {
            var b = Battle;
            if (window == null || b == null || !b.IsPlayerControlled(Station.Systems)) return;
            window.SetActive(true);
            SetUpCamera();
            BuildZones();
            if (actionText != null) actionText.text = "";
            if (b != hooked) { Unhook(); hooked = b; b.RoomHit += OnRoomHit; }
            Refresh();
        }

        public void Close()
        {
            if (window != null) window.SetActive(false);
            if (cam != null) cam.gameObject.SetActive(false);
        }

        void Unhook()
        {
            if (hooked != null) hooked.RoomHit -= OnRoomHit;
            hooked = null;
        }

        void OnRoomHit(ShipRoom room)
        {
            foreach (var z in zones) if (z != null && z.Room == room) z.Flash();
        }

        void Update()
        {
            if (!IsOpen) return;
            var b = Battle;
            if (b == null || b.State != BattleState.Fighting || !b.IsPlayerControlled(Station.Systems)) { Close(); return; }
            timer += Time.unscaledDeltaTime;
            if (timer >= 0.15f) { timer = 0f; Refresh(); }
            else DrawZones(b); //Keeps the hit flashes smooth
        }

        // ---------- Camera ----------

        //The whole ship from straight above, rendered into the view
        void SetUpCamera()
        {
            if (cam == null)
            {
                cam = new GameObject("SystemsCamera").AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                var main = Camera.main;
                if (main != null) cam.cullingMask = main.cullingMask;
            }
            cam.gameObject.SetActive(true);
            cam.backgroundColor = background;

            var rect = view.rectTransform.rect;
            var canvas = view.canvas != null ? view.canvas.rootCanvas : null;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            int w = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 256, 2048), h = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 256, 2048);
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(w, h, 24) { name = "SystemsView" };
            }
            cam.targetTexture = rt;
            view.texture = rt;

            //Fit every room in, with a little room round the edge
            if (ShipRoom.All.Count == 0) return;
            Bounds all = RoomBounds(ShipRoom.All[0]);
            foreach (var r in ShipRoom.All) all.Encapsulate(RoomBounds(r));
            cam.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            cam.transform.position = all.center + Vector3.up * (all.extents.y + 40f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = all.size.y + 80f;
            Vector3 right = cam.transform.right, up = cam.transform.up;
            float halfW = 0f, halfH = 0f;
            foreach (var c in Corners(all))
            {
                Vector3 d = c - all.center;
                halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(d, right)));
                halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(d, up)));
            }
            float aspect = (float)w / h;
            cam.aspect = aspect;
            cam.orthographicSize = Mathf.Max(halfH, halfW / aspect) + margin;
        }

        static Bounds RoomBounds(ShipRoom room)
        {
            var box = room.GetComponent<BoxCollider>();
            return box != null ? box.bounds : new Bounds(room.transform.position, Vector3.one * 4f);
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 4; i++)
                yield return new Vector3(i % 2 == 0 ? b.min.x : b.max.x, b.min.y, i < 2 ? b.min.z : b.max.z);
        }

        // ---------- Rooms ----------

        void BuildZones()
        {
            foreach (var z in zones) if (z != null) Destroy(z.gameObject);
            zones.Clear();
            if (zoneTemplate == null) return;
            foreach (var room in ShipRoom.All)
            {
                if (room == null) continue;
                var z = Instantiate(zoneTemplate, zoneLayer);
                z.gameObject.SetActive(true);
                z.Bind(room, Select);
                //The room's floor plan as seen by the camera, in 0-1 view coordinates
                Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
                foreach (var c in Corners(RoomBounds(room)))
                {
                    Vector3 v = cam.WorldToViewportPoint(c);
                    min = Vector2.Min(min, v); max = Vector2.Max(max, v);
                }
                var r = (RectTransform)z.transform;
                r.anchorMin = min; r.anchorMax = max; r.offsetMin = new Vector2(3, 3); r.offsetMax = new Vector2(-3, -3);
                zones.Add(z);
            }
            if (selected == null || !zones.Contains(selected)) selected = zones.Count > 0 ? zones[0] : null;
        }

        void Select(SystemsRoomZone z)
        {
            selected = z;
            if (actionText != null) actionText.text = "";
            Refresh();
        }

        void Act(System.Func<ShipRoom, string> action)
        {
            if (selected == null || selected.Room == null || Battle == null) return;
            if (actionText != null) actionText.text = action(selected.Room);
            Refresh();
        }

        // ---------- Drawing ----------

        void DrawZones(ShipBattle b)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            var sb = new StringBuilder();
            foreach (var z in zones)
            {
                if (z == null || z.Room == null) continue;
                b.Alerts(z.Room, alerts);
                sb.Length = 0;
                Color tint = quietTint;
                for (int i = 0; i < alerts.Count; i++)
                {
                    var c = ShipBattle.AlertColour(alerts[i]);
                    if (i == 0) tint = new Color(c.r, c.g, c.b, 0.28f);
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(ShipBattle.AlertLabel(alerts[i])).Append("</color>");
                }
                z.Show(sb.ToString(), tint, z == selected, alerts.Count > 0 ? pulse : 1f);
            }
        }

        void Refresh()
        {
            var b = Battle;
            if (b == null) return;
            DrawZones(b);

            bool systems = b.Effect(Station.Systems) > 0f;
            int fires = b.FireCount, aboard = b.BoardersAlive;
            if (headerText != null)
                headerText.text = "<b>SHIP SYSTEMS</b>   Hull " + b.Hull + " / " + b.MaxHull +
                                  "   ·   Life support " + (b.LifeSupportOnline ? "<color=#59CC73>ON</color>" : "<color=#E64033>DOWN</color>") +
                                  "   ·   Systems " + Mathf.RoundToInt(b.Effect(Station.Systems) * 100f) + "%" +
                                  (fires > 0 ? "   ·   <color=#FF7319>" + fires + " fire" + (fires == 1 ? "" : "s") + "</color>" : "") +
                                  (aboard > 0 ? "   ·   <color=#E64033>" + aboard + " aboard</color>" : "");

            var room = selected != null ? selected.Room : null;
            if (room == null)
            {
                Set(roomTitle, "Tap a room");
                Set(roomInfo, "");
                foreach (var btn in new[] { sealButton, openDoorsButton, ventButton, restoreButton, sendCrewButton }) if (btn != null) btn.interactable = false;
                return;
            }
            Set(roomTitle, room.roomName.ToUpperInvariant());
            Set(roomInfo, RoomInfo(b, room));

            bool hasDoors = room.Doors.Count > 0;
            bool sealedNow = Rambler.UI.ShipMapRoom.AnySealed(room);
            bool breached = b.IsBreached(room);
            if (sealButton != null) sealButton.interactable = systems && hasDoors && !sealedNow;
            if (openDoorsButton != null) openDoorsButton.interactable = systems && sealedNow;
            if (ventButton != null) ventButton.interactable = systems && !room.IsVenting;
            if (restoreButton != null) restoreButton.interactable = systems && room.IsVenting && !breached;
            if (sendCrewButton != null) sendCrewButton.interactable = true;
        }

        //What the room is for, then everything wrong with it and how to fix it
        string RoomInfo(ShipBattle b, ShipRoom room)
        {
            var sb = new StringBuilder();
            var s = b.StationIn(room);
            if (s.HasValue)
            {
                var t = b.Terminal(s.Value);
                string who = t != null ? t.OperatorName : null;
                sb.Append(s.Value).Append(" station: ").Append(who != null ? who + (t.PlayerControlled ? " (you)" : " (AI)") : "<color=#E64033>unmanned</color>")
                  .Append(", ").Append(Mathf.RoundToInt(b.Effect(s.Value) * 100f)).Append("%\n");
                if (s.Value == Station.Systems) sb.Append("Runs life support, doors, shields and the medbay.\n");
            }
            if (room.roomName == "Medbay") sb.Append("Heals anyone inside: ").Append((b.MedbayRate * 100f).ToString("0.0")).Append("% a second.\n");
            if (room.roomName == "Weapons") sb.Append("Armoury: restocks ammo for anyone inside.\n");
            if (room.roomName == "Hold")
            {
                int left = 0;
                foreach (var c in CargoCrate.All) if (c != null && !c.Taken) left++;
                sb.Append("Cargo: ").Append(left).Append(" crate").Append(left == 1 ? "" : "s").Append(" aboard.\n");
            }
            sb.Append("Air ").Append(Mathf.RoundToInt(room.Oxygen)).Append("%   ·   Crew ").Append(b.CrewIn(room)).Append('\n');

            b.Alerts(room, alerts);
            if (alerts.Count == 0) sb.Append("\n<color=#59CC73>All clear.</color>");
            foreach (var a in alerts)
            {
                sb.Append("\n<color=#").Append(ColorUtility.ToHtmlStringRGB(ShipBattle.AlertColour(a))).Append("><b>").Append(ShipBattle.AlertLabel(a)).Append("</b></color>  ");
                switch (a)
                {
                    case RoomAlert.Fire: sb.Append(Mathf.RoundToInt(b.FireLevel(room) * 100f)).Append("%: send crew, or vent the room."); break;
                    case RoomAlert.Breach: sb.Append("the hull is open: crew inside patch it faster."); break;
                    case RoomAlert.Intruders: sb.Append("boarders: seal them in, vent them, or send crew."); break;
                    case RoomAlert.Emp: sb.Append("offline for ").Append(Mathf.CeilToInt(b.EmpLeft(room))).Append("s."); break;
                    case RoomAlert.Damaged: sb.Append("crew here repair it, or Engines does."); break;
                    case RoomAlert.NoPower: sb.Append("give it power at Engines."); break;
                    case RoomAlert.LowOxygen: sb.Append(b.LifeSupportOnline ? "the air is thin." : "life support is down."); break;
                }
            }
            return sb.ToString();
        }

        static void Set(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
    }
}
