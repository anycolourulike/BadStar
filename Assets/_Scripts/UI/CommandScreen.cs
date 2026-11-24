using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Crew;
using Rambler.Crew.UI;
using Rambler.Movement;
using Rambler.SceneManagement;
using Rambler.Ship;

namespace Rambler.UI
{
    //The Command/Map screen, opened from the HUD's MAP button. The game pauses and a camera looks straight down
    //on the squad (drag to pan, pinch or mouse wheel to zoom). Markers float over each squad member, the enemies
    //the squad can see, and points of interest.
    //  Squad bar (left)   pick one unit, or ALL
    //  Tap the ground     move there and hold          Tap an enemy   attack it
    //  Buttons (right)    Follow, Hold, Attack (hint), Take Control
    //Orders run when the game resumes. Built by Tools > Company > Build Command Screen.
    public class CommandScreen : MonoBehaviour
    {
        public static CommandScreen Instance { get; private set; }

        [SerializeField] Button openButton;
        [SerializeField] GameObject window;
        [SerializeField] CommandTapArea tapArea;
        [SerializeField] RectTransform markerLayer;
        [SerializeField] Image lineTemplate;
        [SerializeField] CrewUIItem unitMarkerTemplate;
        [SerializeField] CrewUIItem enemyMarkerTemplate;
        [SerializeField] CrewUIItem poiMarkerTemplate;
        [SerializeField] CrewUIItem flagTemplate;
        [SerializeField] CrewUIItem squadRowTemplate;
        [SerializeField] Button allButton;
        [SerializeField] Button followButton;
        [SerializeField] Button holdButton;
        [SerializeField] Button attackButton;
        [SerializeField] Button controlButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Button closeButton;
        [SerializeField] TextMeshProUGUI selectedText;
        [SerializeField] TextMeshProUGUI hintText;

        [Header("Map camera")]
        [SerializeField] float height = 80f;
        [SerializeField] float minSize = 6f;
        [SerializeField] float maxSize = 80f;
        [Tooltip("Space kept around the squad when the map opens (metres).")]
        [SerializeField] float margin = 8f;

        [Header("What the squad can see")]
        [SerializeField] float sightRange = 22f;
        [SerializeField] float eyeHeight = 1.55f;
        [SerializeField] LayerMask sightBlockers = (1 << 0) | (1 << 11);

        [Header("Look")]
        [SerializeField] Color orderLine = new Color(0.95f, 0.62f, 0.2f, 0.9f);
        [SerializeField] Color attackLine = new Color(0.95f, 0.25f, 0.2f, 0.9f);
        [SerializeField] Color tabOn = new Color(0.95f, 0.62f, 0.2f);
        [SerializeField] Color tabOff = new Color(0.16f, 0.24f, 0.32f);

        const string DefaultHint = "Tap the ground to move there and hold. Tap an enemy to attack it. Orders start when you resume.";

        struct Poi { public Vector3 position; public PointOfInterest.Kind kind; public string label; }

        Camera mapCam;
        Camera gameCam; //Switched off while the map is open
        PlayerSelector selector;
        GameObject selected; //null = the whole squad
        float timeScaleBefore = 1f;
        float nextEnemyCheck;
        float lastPinch;
        readonly List<GameObject> visibleEnemies = new List<GameObject>();
        readonly List<Poi> pois = new List<Poi>();
        readonly Dictionary<Object, List<Component>> pools = new Dictionary<Object, List<Component>>();
        readonly Dictionary<Object, int> used = new Dictionary<Object, int>();

        public bool IsOpen => window != null && window.activeSelf;

        void Awake()
        {
            Instance = this;
            if (window != null) window.SetActive(false);
            foreach (var t in new Component[] { lineTemplate, unitMarkerTemplate, enemyMarkerTemplate, poiMarkerTemplate, flagTemplate, squadRowTemplate })
                if (t != null) t.gameObject.SetActive(false);
            if (tapArea != null) tapArea.screen = this;
            Wire(openButton, Open);
            Wire(resumeButton, Close);
            Wire(closeButton, Close);
            Wire(allButton, () => Select(null));
            Wire(followButton, () => ForSelection(UnitFollow, "Following"));
            Wire(holdButton, () => ForSelection(UnitHold, "Holding"));
            Wire(attackButton, () => Hint("Tap an enemy on the map to attack it."));
            Wire(controlButton, TakeControl);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) { Time.timeScale = timeScaleBefore; RestoreGameCamera(); HideHud(false); }
            if (mapCam != null) Destroy(mapCam.gameObject);
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        // ---------- Open / close ----------

        public void Open()
        {
            selector = FindObjectOfType<PlayerSelector>();
            if (selector == null || window == null || IsOpen) return;
            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            window.SetActive(true);
            HideHud(true);
            selected = selector.activePlayer;
            SetUpCamera();
            FrameSquad();
            GatherPois();
            RefreshEnemies();
            Hint(DefaultHint);
        }

        public void Close()
        {
            if (!IsOpen) return;
            window.SetActive(false);
            HideHud(false);
            RestoreGameCamera();
            Time.timeScale = timeScaleBefore;
        }

        //The rest of the HUD (ammo, abilities, joystick, portraits) would show through the map: fade it out meanwhile
        CanvasGroup hudGroup;
        float hudAlpha = 1f;
        bool hudBlocks = true;

        void HideHud(bool hide)
        {
            var parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            var hud = parentCanvas != null ? parentCanvas.rootCanvas : null;
            if (hud == null) return;
            if (hudGroup == null)
            {
                hudGroup = hud.GetComponent<CanvasGroup>();
                if (hudGroup == null) hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
                var own = GetComponent<CanvasGroup>();
                if (own == null) own = gameObject.AddComponent<CanvasGroup>();
                own.ignoreParentGroups = true; //This screen stays visible
            }
            if (hide)
            {
                hudAlpha = hudGroup.alpha; hudBlocks = hudGroup.blocksRaycasts;
                hudGroup.alpha = 0f; hudGroup.blocksRaycasts = false;
            }
            else
            {
                hudGroup.alpha = hudAlpha; hudGroup.blocksRaycasts = hudBlocks;
            }
        }

        void SetUpCamera()
        {
            var main = Camera.main;
            if (mapCam == null)
            {
                mapCam = new GameObject("CommandCamera").AddComponent<Camera>();
                DontDestroyOnLoad(mapCam.gameObject);
            }
            mapCam.gameObject.SetActive(true);
            mapCam.orthographic = true;
            mapCam.nearClipPlane = 0.3f;
            mapCam.farClipPlane = height * 2f + 100f;
            if (main != null)
            {
                mapCam.clearFlags = main.clearFlags;
                mapCam.backgroundColor = main.backgroundColor;
                mapCam.cullingMask = main.cullingMask;
                mapCam.depth = main.depth + 20f;
            }
            //Straight down, turned to match the game camera so left and right stay where the player expects
            //(the camera's own heading: CameraManager.Yaw is its pivot's, which faces the other way)
            float yaw = main != null ? main.transform.eulerAngles.y : 0f;
            mapCam.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            //Only one camera renders while the map is open; the map camera stands in as the main camera meanwhile
            if (main != null && main != mapCam)
            {
                gameCam = main;
                gameCam.enabled = false;
                mapCam.tag = "MainCamera";
            }
        }

        void RestoreGameCamera()
        {
            if (mapCam != null) { mapCam.tag = "Untagged"; mapCam.gameObject.SetActive(false); }
            if (gameCam != null) gameCam.enabled = true;
            gameCam = null;
        }

        //Centre on the living squad, zoomed to fit them with some room around
        void FrameSquad()
        {
            var squad = Living();
            if (squad.Count == 0) return;
            Vector3 right = mapCam.transform.right, up = mapCam.transform.up;
            Vector3 centre = Vector3.zero;
            foreach (var m in squad) centre += m.transform.position;
            centre /= squad.Count;
            float halfW = 0f, halfH = 0f;
            foreach (var m in squad)
            {
                Vector3 d = m.transform.position - centre;
                halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(d, right)));
                halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(d, up)));
            }
            mapCam.transform.position = centre + Vector3.up * height;
            mapCam.orthographicSize = Mathf.Clamp(Mathf.Max(halfH, halfW / Mathf.Max(0.1f, mapCam.aspect)) + margin, minSize, maxSize);
        }

        // ---------- Map input (from CommandTapArea) ----------

        public void Pan(Vector2 screenDelta)
        {
            if (mapCam == null) return;
            float worldPerPixel = 2f * mapCam.orthographicSize / Mathf.Max(1, Screen.height);
            mapCam.transform.position -= (mapCam.transform.right * screenDelta.x + mapCam.transform.up * screenDelta.y) * worldPerPixel;
        }

        public void Zoom(float amount)
        {
            if (mapCam == null) return;
            mapCam.orthographicSize = Mathf.Clamp(mapCam.orthographicSize * (1f + amount), minSize, maxSize);
        }

        public void Tap(Vector2 screenPos)
        {
            if (mapCam == null) return;
            Ray ray = mapCam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, height * 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null && health.CompareTag("Enemy") && visibleEnemies.Contains(health.gameObject))
                {
                    Attack(health.gameObject);
                    return;
                }
                MoveTo(hit.point);
                return;
            }
            //Nothing under the finger (e.g. off the level's colliders): use the squad's ground height
            float y = selector != null && selector.activePlayer != null ? selector.activePlayer.transform.position.y : 0f;
            if (new Plane(Vector3.up, new Vector3(0, y, 0)).Raycast(ray, out float enter)) MoveTo(ray.GetPoint(enter));
        }

        void Update()
        {
            if (!IsOpen) return;
            //Pinch to zoom
            if (Input.touchCount == 2)
            {
                float d = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
                if (Input.GetTouch(1).phase == TouchPhase.Began) lastPinch = d;
                else if (lastPinch > 0f) Zoom((lastPinch - d) / Mathf.Max(1f, lastPinch));
                lastPinch = d;
            }
            else lastPinch = 0f;

            if (Time.unscaledTime >= nextEnemyCheck) RefreshEnemies();
        }

        void LateUpdate()
        {
            if (IsOpen) Draw();
        }

        // ---------- Orders ----------

        void Select(GameObject unit)
        {
            selected = unit;
            Hint(DefaultHint);
        }

        void MoveTo(Vector3 point)
        {
            var units = Selection();
            if (units.Count == 0) return;
            for (int i = 0; i < units.Count; i++)
            {
                Vector3 dest = point + Spread(i, units.Count);
                if (NavMesh.SamplePosition(dest, out var hit, 3f, NavMesh.AllAreas)) dest = hit.position;
                UnitMove(units[i], dest);
            }
            Hint(Who(units) + (units.Count == 1 ? " moves" : " move") + " there and holds.");
        }

        void Attack(GameObject enemy)
        {
            var units = Selection();
            foreach (var u in units) UnitAttack(u, enemy);
            if (units.Count > 0) Hint(Who(units) + (units.Count == 1 ? " attacks " : " attack ") + enemy.name + ".");
        }

        void ForSelection(System.Action<GameObject> order, string done)
        {
            var units = Selection();
            foreach (var u in units) order(u);
            if (units.Count > 0) Hint(Who(units) + ": " + done + ".");
        }

        void TakeControl()
        {
            if (selected == null || !IsAlive(selected) || selected == selector.activePlayer) return;
            selector.SwitchTo(selected);
            Hint("You control " + selected.name + ".");
        }

        bool Controlled(GameObject unit) => selector != null && selector.activePlayer == unit;

        void UnitMove(GameObject u, Vector3 dest)
        {
            if (Controlled(u)) { var mover = u.GetComponent<Mover>(); if (mover != null) mover.StartMoveAction(dest, 1f); return; }
            var brain = u.GetComponent<CompanionBrain>();
            if (brain != null) brain.OrderMove(dest);
        }

        void UnitHold(GameObject u)
        {
            if (Controlled(u)) { var mover = u.GetComponent<Mover>(); if (mover != null) mover.CancelNav(); return; }
            var brain = u.GetComponent<CompanionBrain>();
            if (brain != null) brain.OrderHold();
        }

        void UnitFollow(GameObject u)
        {
            if (Controlled(u)) return; //Everyone else follows this one
            var brain = u.GetComponent<CompanionBrain>();
            if (brain != null) brain.OrderFollow();
        }

        void UnitAttack(GameObject u, GameObject enemy)
        {
            if (Controlled(u))
            {
                var fighter = u.GetComponent<Fighter>();
                if (fighter == null) return;
                fighter.CombatTarget = enemy.GetComponent<CombatTarget>();
                fighter.TargetCap = enemy.GetComponent<CapsuleCollider>();
                fighter.Attack(enemy);
                fighter.HoldTarget = true; //Keeps firing until the target is down or the next tap, as with a normal tap
                return;
            }
            var brain = u.GetComponent<CompanionBrain>();
            if (brain != null) brain.OrderAttack(enemy);
        }

        //A loose ring so a group order doesn't stack everyone on one spot
        static Vector3 Spread(int i, int count)
        {
            if (count <= 1 || i == 0) return Vector3.zero;
            float a = (i - 1) * Mathf.PI * 2f / (count - 1);
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f;
        }

        // ---------- Drawing ----------

        void Draw()
        {
            used.Clear();
            var squad = Squad();

            //Squad bar
            Tint(allButton, selected == null);
            foreach (var m in squad)
            {
                var row = Take(squadRowTemplate);
                FillUnit(row, m);
                if (row.highlight != null) row.highlight.enabled = m == selected;
                var unit = m;
                CrewUIItem.SetButton(row.select, null, null, true, IsAlive(m), () => Select(unit));
            }

            //Order lines and flags under the markers
            foreach (var m in squad)
            {
                if (!IsAlive(m)) continue;
                Vector3 from = m.transform.position;
                var brain = m.GetComponent<CompanionBrain>();
                if (Controlled(m))
                {
                    var agent = m.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath && !agent.isStopped)
                        Line(from, agent.destination, orderLine, true, "M");
                    var f = m.GetComponent<Fighter>();
                    if (f != null && f.CombatTarget != null) Line(from, f.CombatTarget.transform.position, attackLine, false, null);
                }
                else if (brain != null)
                {
                    if (brain.HasPost) Line(from, brain.OrderPosition, orderLine, true, brain.Order == SquadOrder.Move ? "M" : "H");
                    else if (brain.Order == SquadOrder.Attack && brain.OrderTarget != null) Line(from, brain.OrderTarget.transform.position, attackLine, false, null);
                }
            }

            //Points of interest
            foreach (var p in pois)
            {
                var item = Take(poiMarkerTemplate);
                if (!Place(item, p.position)) continue;
                if (item.portrait != null) item.portrait.color = PointOfInterest.Colour(p.kind);
                CrewUIItem.SetText(item.initials, PointOfInterest.Glyph(p.kind));
                CrewUIItem.SetText(item.title, p.label);
            }

            //Enemies the squad can see
            foreach (var e in visibleEnemies)
            {
                if (e == null || !IsAlive(e)) continue;
                var item = Take(enemyMarkerTemplate);
                if (!Place(item, e.transform.position)) continue;
                var h = e.GetComponent<Health>();
                if (item.bar != null) item.bar.fillAmount = h != null ? h.GetFraction() : 1f;
                var enemy = e;
                CrewUIItem.SetButton(item.select, null, null, true, true, () => Attack(enemy));
            }

            //Squad markers on top
            foreach (var m in squad)
            {
                var item = Take(unitMarkerTemplate);
                if (!Place(item, m.transform.position)) continue;
                FillUnit(item, m);
                if (item.highlight != null) item.highlight.enabled = IsAlive(m) && (selected == null || m == selected);
                var unit = m;
                CrewUIItem.SetButton(item.select, null, null, true, IsAlive(m), () => Select(unit));
            }

            HideUnused();

            //Buttons and texts
            var sel = Selection();
            bool anyCompanion = false;
            foreach (var u in sel) if (!Controlled(u) && u.GetComponent<CompanionBrain>() != null) anyCompanion = true;
            if (followButton != null) followButton.interactable = anyCompanion;
            if (holdButton != null) holdButton.interactable = sel.Count > 0;
            if (attackButton != null) attackButton.interactable = sel.Count > 0 && visibleEnemies.Count > 0;
            if (controlButton != null) controlButton.interactable = selected != null && IsAlive(selected) && !Controlled(selected);
            CrewUIItem.SetText(selectedText, "Selected: " + (selected == null ? "Whole squad" : selected.name));
        }

        void FillUnit(CrewUIItem item, GameObject m)
        {
            var identity = m.GetComponent<CharacterIdentity>();
            item.SetPortrait(identity != null ? identity.Portrait : null, m.name);
            CrewUIItem.SetText(item.title, m.name);
            var h = m.GetComponent<Health>();
            if (item.bar != null) item.bar.fillAmount = h != null ? h.GetFraction() : 1f;
            CrewUIItem.SetText(item.side, OrderLabel(m));
            if (item.portrait != null && !IsAlive(m)) item.portrait.color = new Color(0.3f, 0.3f, 0.3f);
        }

        string OrderLabel(GameObject m)
        {
            if (!IsAlive(m)) return "Down";
            if (Controlled(m)) return "You";
            var brain = m.GetComponent<CompanionBrain>();
            if (brain == null) return "";
            switch (brain.Order)
            {
                case SquadOrder.Hold: return "Hold";
                case SquadOrder.Move: return "Moving";
                case SquadOrder.Attack: return "Attack";
                default: return "Follow";
            }
        }

        void Tint(Button b, bool on)
        {
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = on ? tabOn : tabOff;
        }

        //A line between two world points, optionally with a flag marker at the far end
        void Line(Vector3 a, Vector3 b, Color colour, bool flag, string glyph)
        {
            if (!ToCanvas(a, out Vector2 pa) || !ToCanvas(b, out Vector2 pb)) return;
            var img = Take(lineTemplate);
            var rt = img.rectTransform;
            Vector2 d = pb - pa;
            rt.anchoredPosition = (pa + pb) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, rt.sizeDelta.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            img.color = colour;
            if (!flag || flagTemplate == null) return;
            var f = Take(flagTemplate);
            ((RectTransform)f.transform).anchoredPosition = pb;
            CrewUIItem.SetText(f.initials, glyph);
        }

        bool Place(CrewUIItem item, Vector3 world)
        {
            if (!ToCanvas(world, out Vector2 p)) { item.gameObject.SetActive(false); return false; }
            ((RectTransform)item.transform).anchoredPosition = p;
            return true;
        }

        bool ToCanvas(Vector3 world, out Vector2 local)
        {
            local = default;
            Vector3 sp = mapCam.WorldToScreenPoint(world);
            if (sp.z < 0f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(markerLayer, sp, null, out local);
        }

        //Reuses copies of a template each frame; copies not used this frame are hidden
        T Take<T>(T template) where T : Component
        {
            if (!pools.TryGetValue(template, out var list)) pools[template] = list = new List<Component>();
            used.TryGetValue(template, out int n);
            used[template] = n + 1;
            if (n >= list.Count)
            {
                var copy = Instantiate(template, template.transform.parent);
                copy.name = template.name.Replace("Template", "") + " " + n;
                list.Add(copy);
            }
            var c = (T)list[n];
            c.gameObject.SetActive(true);
            //Later items draw on top, so markers stay above lines (each pool sits in its own layer object)
            return c;
        }

        void HideUnused()
        {
            foreach (var kv in pools)
            {
                used.TryGetValue(kv.Key, out int n);
                for (int i = n; i < kv.Value.Count; i++) if (kv.Value[i] != null) kv.Value[i].gameObject.SetActive(false);
            }
        }

        // ---------- World state ----------

        List<GameObject> Squad()
        {
            var list = new List<GameObject>();
            if (selector == null) return list;
            foreach (var m in selector.players) if (m != null) list.Add(m);
            return list;
        }

        List<GameObject> Living()
        {
            var list = new List<GameObject>();
            foreach (var m in Squad()) if (IsAlive(m)) list.Add(m);
            return list;
        }

        List<GameObject> Selection()
        {
            if (selected != null) return IsAlive(selected) ? new List<GameObject> { selected } : new List<GameObject>();
            return Living();
        }

        static string Who(List<GameObject> units) => units.Count == 1 ? units[0].name : "The squad";

        static bool IsAlive(GameObject g)
        {
            if (g == null) return false;
            var h = g.GetComponent<Health>();
            return h == null || !h.IsDead();
        }

        //Enemies any living squad member can see from where they stand
        void RefreshEnemies()
        {
            nextEnemyCheck = Time.unscaledTime + 0.5f;
            visibleEnemies.Clear();
            var squad = Living();
            foreach (var e in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                if (!IsAlive(e)) continue;
                Vector3 target = e.transform.position + Vector3.up * 1.2f;
                foreach (var m in squad)
                {
                    Vector3 eye = m.transform.position + Vector3.up * eyeHeight;
                    if (Vector3.Distance(eye, target) > sightRange) continue;
                    if (Physics.Linecast(eye, target, sightBlockers, QueryTriggerInteraction.Ignore)) continue;
                    visibleEnemies.Add(e);
                    break;
                }
            }
        }

        void GatherPois()
        {
            pois.Clear();
            foreach (var p in PointOfInterest.All)
                if (p != null && p.ShowOnMap) pois.Add(new Poi { position = p.transform.position, kind = p.Type, label = p.Label });
            foreach (var t in ShipTerminal.All)
                if (t != null && !t.IsHacked && t.GetComponent<PointOfInterest>() == null)
                    pois.Add(new Poi { position = t.transform.position, kind = PointOfInterest.Kind.Terminal, label = t.TerminalName });
            foreach (var c in FindObjectsOfType<CrewTerminal>())
                if (c.GetComponent<PointOfInterest>() == null)
                    pois.Add(new Poi { position = c.transform.position, kind = PointOfInterest.Kind.Crew, label = "Crew" });
            foreach (var p in FindObjectsOfType<Portal>())
                if (p.GetComponent<PointOfInterest>() == null)
                    pois.Add(new Poi { position = p.transform.position, kind = PointOfInterest.Kind.Exit, label = "Exit" });
        }

        void Hint(string text) => CrewUIItem.SetText(hintText, text);
    }
}
