using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Rambler.Ship;
using Rambler.Crew.Battle;

namespace Rambler.EditorTools
{
    //Assembles the Shearwater's interior (the armed freighter the ship battles are fought aboard) from the Cosmic Retro Modular Base Kit (2 m grid, walls 2.5 m high):
    //floors, walls, sliding doors (the kit door's two leaves), a ShipRoom per room, airlock boarding points and
    //an anchor per station. Saved as a prefab; Build Ship Battle Scene puts it in the battle.
    //Ship space: x across (starboard +), z along (tail 0, nose +29), floor top at y 0.
    //  Engine room z 0-6 | Hold z 6-12 (airlocks on both sides) | Hub z 12-24 with Weapons/Systems (starboard)
    //  and Quarters/Medbay (port) 6 m rooms beside it | Bridge z 24-29, a wedge with a front window.
    public static class ShearwaterBuilder
    {
        public const string PrefabPath = "Assets/_Prefabs/Ships/Shearwater_Interior.prefab";
        const string Kit = "Assets/Cosmic_Retro_Modular_BaseKit/Prefabs/";
        const string Wall = Kit + "Walls/CRM_Wall_Plain_01.prefab";
        const string WallHalf = Kit + "Walls/CRM_Wall_Plain_Half_01.prefab";
        const string Door = Kit + "Doors/CRM_Wall_Door_S_01.prefab";
        const string Window = Kit + "Windows/CRM_Wall_Window_S_01.prefab";
        const string Angled = Kit + "Corners/CRM_Wall_Angled_Half_01.prefab";
        const string Floor = Kit + "Floors/CRM_Floor_01.prefab";
        const string Column = Kit + "Column/CRM_Wall_Column_01.prefab";

        //Which side of a kit wall carries the panelling: +1 = the piece's +thickness side. Flip if walls face out.
        public static float FaceSign = 1f;
        public const float RoomHeight = 2.5f;

        struct Piece { public Vector3 a, b, normal; public float minY; }
        static readonly Dictionary<string, Piece> lines = new Dictionary<string, Piece>();

        class Rm { public string name; public Rect r; public ShipRoom room; public List<ShipDoor> doors = new List<ShipDoor>(); }
        static List<Rm> rooms;
        static Transform built, kitRoot;

        [MenuItem("Tools/Company/Build Shearwater Interior")]
        public static GameObject Build()
        {
            lines.Clear();
            var root = new GameObject("Shearwater_Interior");
            try
            {
                kitRoot = new GameObject("Kit").transform; kitRoot.SetParent(root.transform, false);
                built = new GameObject("Rooms").transform; built.SetParent(root.transform, false);
                rooms = new List<Rm>
                {
                    R("Engine Room", -5, 0, 10, 6), R("Hold", -7, 6, 14, 6), R("Hub", -3, 12, 6, 12),
                    R("Weapons", 3, 12, 4, 6), R("Systems", 3, 18, 4, 6),
                    R("Quarters", -7, 12, 4, 6), R("Medbay", -7, 18, 4, 6), R("Bridge", -4, 24, 8, 5),
                };

                //Floors (the bridge wedge is tiled to its window)
                FloorRect(-5, 0, 10, 6); FloorRect(-7, 6, 14, 6); FloorRect(-7, 12, 14, 12); FloorRect(-4, 24, 8, 2);
                FloorRect(-3, 26, 6, 2); FloorRect(-1, 28, 2, 2);

                //Outer hull walls (inside = ship centre line)
                Seg(-5, 0, 5, 0);                                   //engine rear
                Seg(5, 0, 5, 6); Seg(-5, 0, -5, 6);                 //engine sides
                Seg(5, 6, 7, 6); Seg(-5, 6, -7, 6);                 //hold rear shoulders
                Seg(7, 6, 7, 24, 2); Seg(-7, 6, -7, 24, 2);         //hold (airlock door at z 8-10) + side rooms
                Seg(7, 24, 4, 24); Seg(-7, 24, -4, 24);             //side room fronts
                Seg(4, 24, 4, 26); Seg(-4, 24, -4, 26);             //bridge sides
                AngledWall(new Vector3(4, 0, 26), new Vector3(1, 0, 29));
                AngledWall(new Vector3(-4, 0, 26), new Vector3(-1, 0, 29));
                Seg(-1, 29, 1, 29, -1, true);                       //front window

                //Interior bulkheads (door slot = metres along the segment)
                Seg(-5, 6, 5, 6, 4);                                //engine | hold
                Seg(-7, 12, 7, 12, 6);                              //hold | hub
                Seg(-4, 24, -1, 24); Seg(-1, 24, 1, 24, 0); Seg(1, 24, 4, 24);  //hub | bridge (door in the middle)
                Seg(3, 12, 3, 24, 2, false, 8);                     //hub | weapons, systems
                Seg(-3, 12, -3, 24, 2, false, 8);                   //hub | quarters, medbay
                Seg(3, 18, 7, 18); Seg(-3, 18, -7, 18);             //weapons | systems, quarters | medbay

                //Columns on the outside corners hide the wall joints
                foreach (var c in new[] { new Vector2(5, 0), new Vector2(-5, 0), new Vector2(7, 6), new Vector2(-7, 6), new Vector2(7, 24), new Vector2(-7, 24) })
                    Place(Column, new Vector3(c.x, 0, c.y), Quaternion.identity, kitRoot);

                Furnish(root.transform);

                //Rooms (air) linked to their doors
                foreach (var rm in rooms)
                {
                    var go = new GameObject(rm.name);
                    go.transform.SetParent(built, false);
                    go.transform.localPosition = new Vector3(rm.r.center.x, RoomHeight * 0.5f + 0.05f, rm.r.center.y);
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true; box.size = new Vector3(rm.r.width - 0.4f, RoomHeight, rm.r.height - 0.4f);
                    rm.room = go.AddComponent<ShipRoom>();
                    rm.room.roomName = rm.name;
                }
                foreach (var d in Object.FindObjectsOfType<ShipDoor>())
                {
                    if (!d.transform.IsChildOf(root.transform)) continue;
                    foreach (var rm in rooms)
                    {
                        var p = d.transform.position;
                        var grown = new Rect(rm.r.x - 0.6f, rm.r.y - 0.6f, rm.r.width + 1.2f, rm.r.height + 1.2f);
                        if (grown.Contains(new Vector2(p.x, p.z))) rm.doors.Add(d);
                    }
                }
                foreach (var rm in rooms)
                {
                    var so = new SerializedObject(rm.room);
                    var list = so.FindProperty("doors");
                    list.arraySize = rm.doors.Count;
                    for (int i = 0; i < rm.doors.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = rm.doors[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                //Boarders come in at the hold airlocks
                var points = new GameObject("Boarding Points").transform; points.SetParent(root.transform, false);
                foreach (var p in new[] { new Vector3(6.0f, 0, 9), new Vector3(-6.0f, 0, 9), new Vector3(5.6f, 0, 7.6f), new Vector3(-5.6f, 0, 7.6f) })
                {
                    var sp = new GameObject("Boarding Point").AddComponent<ShipSpawnPoint>();
                    sp.transform.SetParent(points, false);
                    sp.transform.localPosition = p;
                    sp.transform.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sign(p.x), 0, 0));
                }

                //Where each station's console goes (local position, yaw), and where the crew start
                var anchors = new GameObject("Station Anchors").transform; anchors.SetParent(root.transform, false);
                //(a console's forward points at where its crew stands)
                Anchor(anchors, "Navigation", new Vector3(0, 0, 27.4f), 180f);
                Anchor(anchors, "Weapons", new Vector3(6.2f, 0, 15f), -90f);
                Anchor(anchors, "Systems", new Vector3(6.2f, 0, 21f), -90f);
                Anchor(anchors, "Engines", new Vector3(0, 0, 0.9f), 0f);
                Anchor(anchors, "Player Start", new Vector3(0, 0, 21f), 0f);
                Anchor(anchors, "Squad Start", new Vector3(0, 0, 15f), 0f);

                if (!AssetDatabase.IsValidFolder("Assets/_Prefabs/Ships")) AssetDatabase.CreateFolder("Assets/_Prefabs", "Ships");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Company] Shearwater interior saved to " + PrefabPath + " (" + kitRoot.childCount + " kit pieces, " + rooms.Count + " rooms).");
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ---------- Furniture (prop packs already in the project) ----------
        //Each prop is placed by its bounds, not its pivot: centred on (x, z) in ship space, its base at y, turned so
        //its front (+z) faces yaw. Yaw 0 = toward the nose, 90 = starboard, 180 = tail, -90 = port.
        static Transform furniture;
        static readonly Dictionary<string, string> propPaths = new Dictionary<string, string>();

        static void Furnish(Transform root)
        {
            furniture = new GameObject("Furniture").transform; furniture.SetParent(root, false);

            //Bridge: side consoles with a seat at each (the nav console's own spot stays clear for whoever mans it)
            var bridge = Group("Bridge");
            Prop("CR_Chair_Medium", -2.35f, 25f, -90, bridge);
            Prop("CR_Chair_Medium", 2.35f, 25f, 90, bridge);
            Prop("CR_Computer_PanelOnly", -3.35f, 25f, 90, bridge); Prop("CR_Monitor_Small_1", -3.4f, 25f, 90, bridge, 0.93f);
            Prop("CR_Computer_PanelOnly", 3.35f, 25f, -90, bridge); Prop("CR_Monitor_Small_1", 3.4f, 25f, -90, bridge, 0.93f);

            //Hub: the holo-table (a table with the projector on it), seats at its ends, machines on the walls
            var hub = Group("Hub");
            Prop("Cosmic_retro_mesa_table_small_square", 0, 18f, 0, hub);
            Prop("Cosmic_retro_mesa_Projector", 0, 18f, 0, hub, 0.85f);
            Prop("Cosmic_retro_mesa_chair_comfy_orange", 0, 16.8f, 0, hub);
            Prop("Cosmic_retro_mesa_chair_comfy_orange", 0, 19.2f, 180, hub);
            Prop("Cosmic_retro_mesa_vendor_snack", -2.45f, 17.4f, 90, hub);
            Prop("Cosmic_retro_mesa_dispenser_drink", -2.45f, 18.6f, 90, hub);
            Prop("osmic_retro_mesa_tv_flat_wall", 2.72f, 18f, -90, hub, 1.25f);
            Prop("Cosmic_retro_mesa_bench_short", 2.5f, 18f, -90, hub);

            //Weapons + armoury: lockers along the tail wall, a workbench and ammo by the divider
            var weapons = Group("Weapons");
            foreach (float x in new[] { 4.1f, 4.8f, 5.5f, 6.2f }) Prop("CR_Locker_Emergency_Red", x, 12.45f, 0, weapons);
            Prop("CR_Table_Small_2", 5.6f, 17.2f, 180, weapons);
            Prop("CR_Tool_Hammer", 5.6f, 17.2f, 30, weapons, 0.69f);
            Prop("CR_StorageCrate_Large_1", 3.9f, 17.4f, 0, weapons); Prop("CR_StorageCrate_Large_1", 3.9f, 17.4f, 10, weapons, 0.5f);

            //Systems: the ship's AI core in the corner, a desk terminal, control panels on the walls
            var systems = Group("Systems");
            Prop("Retro_laboratory_Analyzer_DoubleScreen", 3.7f, 23.2f, 180, systems);
            Prop("CR_Lab2_Desk_01", 5.5f, 18.75f, 0, systems);
            Prop("CR_Lab2_Computer_01", 5.5f, 18.6f, 0, systems, 0.81f);
            Prop("CR_Lab2_Stool", 5.5f, 19.7f, 0, systems);
            Prop("CR_WallPanel_Control_2", 5.5f, 23.72f, 180, systems, 1.2f);
            Prop("CR_Computer_PanelOnly", 5.6f, 23.35f, 180, systems);

            //Quarters: two bunks along the hull, a bench, a locker
            var quarters = Group("Quarters");
            foreach (float z in new[] { 13.4f, 16.6f })
            {
                Prop("Cosmic_retro_mesa_bed_simple", -6.2f, z, 90, quarters);
                Prop("Cosmic_retro_mesa_bed_simple", -6.2f, z, 90, quarters, 1.1f);
            }
            Prop("Cosmic_retro_mesa_bench_short", -4.6f, 17.45f, 180, quarters);
            Prop("CR_Locker_Emergency_Red", -3.6f, 12.45f, 0, quarters);
            Prop("Cosmic_retro_mesa_shelf_wall_2slot", -4.6f, 12.45f, 0, quarters, 1.0f);

            //Medbay: two beds along the hull, the healing capsule, a terminal
            var medbay = Group("Medbay");
            Prop("Retro_laboratory_Medical_Bed_Compact_2", -6.05f, 19.5f, 0, medbay);
            Prop("Retro_laboratory_Medical_Bed_Compact_2", -6.05f, 22.6f, 0, medbay);
            Prop("CR_Lab2_Capsule_01", -3.95f, 23.05f, 180, medbay);
            Prop("Retro_laboratory_Wall_Terminal_Thin", -5.0f, 23.5f, 180, medbay);
            Prop("Retro_laboratory_Biohazard_Barrel_2", -3.6f, 18.6f, 0, medbay);

            //Hold: cargo stacked in the four corners, clear of the centre aisle and the airlock lane (z 8-10)
            var cargo = Group("Cargo");
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float z in new[] { 6.65f, 7.25f, 10.75f, 11.35f })
                    foreach (float x in new[] { 2.4f, 3.5f, 4.6f })
                    {
                        Crate(Prop("CR_StorageCrate_Large_1", sx * x, z, 0, cargo));
                        if (x < 4f) Crate(Prop("CR_StorageCrate_Large_1", sx * x, z, 0, cargo, 0.5f));
                    }
                Prop("Retro_laboratory_Biohazard_Barrel_2", sx * 6.2f, 6.75f, 0, cargo);
                Prop("Retro_laboratory_Biohazard_Barrel_2", sx * 6.2f, 11.3f, 0, cargo);
                Prop("CR_StorageCrate_Large_1", sx * 5.6f, 11.3f, 90, cargo);
            }

            //Engine room: four reactor tubes, banks of wall boxes, pipes and cables, wall fans
            var engine = Group("Engine Room");
            foreach (float sx in new[] { -1f, 1f })
            {
                Prop("Retro_laboratory_Reactor_Core_Tube", sx * 3.9f, 1.2f, 0, engine);
                Prop("Retro_laboratory_Reactor_Core_Tube", sx * 3.9f, 3.4f, 0, engine);
                Prop("CRM_Wall_Box_02", sx * 2.4f, 0.45f, 0, engine, 0.9f);
                Prop("CRM_Wall_Box_05", sx * 1.5f, 0.45f, 0, engine, 1.4f);
                Prop("CRM_Pipe_01", sx * 4.6f, 4.9f, sx * -90, engine, 0.2f);
                Prop("CRM_Cables_03", sx * 2.6f, 5.6f, 180, engine, 1.3f);
                Prop("Retro_laboratory_Biohazard_Barrel_2", sx * 3.6f, 5.3f, 0, engine);
            }
            Prop("CRM_Wall_Fan_01", -4.75f, 2.2f, 90, engine, 1.0f);
            Prop("CRM_Wall_Fan_01", 4.75f, 2.2f, -90, engine, 1.0f);
        }

        //A crate boarders can carry off (ShipBattle's looters)
        static void Crate(GameObject go) { if (go != null) go.AddComponent<Rambler.Crew.Battle.CargoCrate>(); }

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform; t.SetParent(furniture, false); return t;
        }

        static GameObject Prop(string name, float x, float z, float yaw, Transform parent, float y = 0f)
        {
            if (!propPaths.TryGetValue(name, out var path))
            {
                path = null;
                foreach (var g in AssetDatabase.FindAssets(name + " t:Prefab"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (System.IO.Path.GetFileNameWithoutExtension(p) == name) { path = p; break; }
                }
                propPaths[name] = path;
            }
            if (path == null) { Debug.LogWarning("[Company] Prop not found: " + name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * go.transform.localRotation;
            go.transform.localPosition = Vector3.zero;
            var b = WorldBounds(go);
            var local = parent.InverseTransformPoint(b.center);
            float bottom = parent.InverseTransformPoint(b.min).y;
            go.transform.localPosition = new Vector3(x - local.x, y - bottom, z - local.z);
            return go;
        }

        static Rm R(string name, float x, float z, float w, float d) => new Rm { name = name, r = new Rect(x, z, w, d) };

        static void Anchor(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        static void FloorRect(float x, float z, float w, float d)
        {
            for (float i = x; i < x + w - 0.01f; i += 2f)
                for (float k = z; k < z + d - 0.01f; k += 2f)
                    PlaceFloor(new Vector3(i + 1f, 0, k + 1f));
        }

        //The kit floor is a 2 m tile centred on its pivot with its top at the pivot (measured from the bounds anyway)
        static void PlaceFloor(Vector3 centre)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Floor);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(kitRoot, false);
            var b = LocalBounds(go);
            go.transform.localPosition = centre - new Vector3(b.center.x, b.max.y, b.center.z);
        }

        //A straight wall from (x1,z1) to (x2,z2) in 2 m pieces (1 m to finish). doorAt/door2At = metres along it
        //where a 2 m door goes; window puts a window piece at 0.
        static void Seg(float x1, float z1, float x2, float z2, float doorAt = -1, bool window = false, float door2At = -1)
        {
            Vector3 a = new Vector3(x1, 0, z1), b = new Vector3(x2, 0, z2);
            float len = Vector3.Distance(a, b);
            Vector3 dir = (b - a) / len;
            float t = 0f;
            while (t < len - 0.01f)
            {
                string piece; float w;
                bool isDoor = Mathf.Abs(t - doorAt) < 0.01f || Mathf.Abs(t - door2At) < 0.01f;
                if (isDoor) { piece = Door; w = 2f; }
                else if (window && t < 0.01f) { piece = Window; w = 2f; }
                else if (len - t >= 1.99f) { piece = Wall; w = 2f; }
                else { piece = WallHalf; w = 1f; }
                var go = PlaceAlong(piece, a + dir * t, a + dir * (t + w));
                if (isDoor) MakeDoor(go, a + dir * (t + 1f), dir);
                t += w;
            }
        }

        static void AngledWall(Vector3 a, Vector3 b) => PlaceAlong(Angled, a, b);

        //Puts a wall piece so its line runs from a to b, turned whichever way puts its panelled face toward the
        //ship's centre line (x = 0); tries the four right-angle yaws
        static GameObject PlaceAlong(string path, Vector3 a, Vector3 b)
        {
            var line = LineOf(path);
            Vector3 want = (b - a).normalized;
            Vector3 mid = (a + b) * 0.5f;
            //Toward the middle of the ship, measured across the wall (so a bulkhead faces the same way on both sides of its door)
            Vector3 toMid = new Vector3(0, 0, 14.5f) - mid;
            Vector3 across = Vector3.Cross(Vector3.up, want);
            Vector3 inward = across * Mathf.Sign(Vector3.Dot(toMid, across));
            if (Vector3.Dot(toMid, across) == 0f) inward = across;
            float best = -999f; Quaternion bestRot = Quaternion.identity; Vector3 bestPos = a;
            for (int k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(0, 90f * k, 0);
                Vector3 la = rot * line.a, lb = rot * line.b, n = rot * line.normal;
                Vector3 d = (lb - la).normalized;
                foreach (bool reversed in new[] { false, true })
                {
                    Vector3 dd = reversed ? -d : d;
                    if (Vector3.Dot(dd, want) < 0.95f) continue;
                    float score = Vector3.Dot(n, inward) * FaceSign;
                    if (score > best)
                    {
                        best = score; bestRot = rot;
                        bestPos = (reversed ? a - lb : a - la) + Vector3.up * -line.minY;
                    }
                }
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(kitRoot, false);
            go.transform.localRotation = bestRot * go.transform.localRotation;
            go.transform.localPosition = bestPos;
            return go;
        }

        //Turns the kit door's two leaves into a ShipDoor that slides them apart along the wall
        static void MakeDoor(GameObject piece, Vector3 centre, Vector3 along)
        {
            var leaves = new List<Transform>();
            foreach (var t in piece.GetComponentsInChildren<Transform>(true))
                if (t != piece.transform && t.name.StartsWith("Wall_Door")) leaves.Add(t);
            var go = new GameObject("Door");
            go.transform.SetParent(built, false);
            go.transform.localPosition = centre + Vector3.up * 1.2f;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, along));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(1.8f, 2.4f, 1.6f);
            var door = go.AddComponent<ShipDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("displayName").stringValue = "Door";
            var sp = so.FindProperty("slidingPanels");
            var op = so.FindProperty("openOffsets");
            sp.arraySize = leaves.Count; op.arraySize = leaves.Count;
            for (int i = 0; i < leaves.Count; i++)
            {
                var leaf = leaves[i];
                var lb = WorldBounds(leaf.gameObject);
                float side = Mathf.Sign(Vector3.Dot(lb.center - (piece.transform.parent.TransformPoint(centre)), along));
                if (side == 0) side = i == 0 ? -1 : 1;
                //openOffsets are in the leaf's parent space
                Vector3 worldOffset = along * side * 0.85f;
                sp.GetArrayElementAtIndex(i).objectReferenceValue = leaf;
                op.GetArrayElementAtIndex(i).vector3Value = leaf.parent.InverseTransformVector(worldOffset);
                foreach (var c in leaf.GetComponentsInChildren<Collider>()) c.enabled = true;
                //The NavMesh runs through the doorway; a shut door carves it with the obstacle below
                var mod = leaf.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
                mod.ignoreFromBuild = true;
            }
            var obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true; obstacle.size = new Vector3(1.8f, 2.4f, 0.3f); obstacle.enabled = false;
            so.FindProperty("obstacle").objectReferenceValue = obstacle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        //A piece's wall line in its own space: for straight walls the long axis at mid-thickness, for the angled
        //piece the diagonal through its mesh; plus the panelled-face normal (the +thickness side) and its base height
        static Piece LineOf(string path)
        {
            if (lines.TryGetValue(path, out var cached)) return cached;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.position = Vector3.zero; //keeps the prefab's own root rotation, which PlaceAlong keeps too
            var verts = new List<Vector3>();
            foreach (var mf in tmp.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices) verts.Add(mf.transform.TransformPoint(v));
            }
            Object.DestroyImmediate(tmp);
            var b = new Bounds(verts[0], Vector3.zero);
            foreach (var v in verts) b.Encapsulate(v);
            Piece p; p.minY = b.min.y;
            bool angled = path == Angled;
            if (!angled)
            {
                bool alongX = b.size.x >= b.size.z;
                //Wall length is a whole number of metres; the pivot end is the near end
                if (alongX)
                {
                    float len = Mathf.Round(b.size.x);
                    float x0 = Mathf.Abs(b.min.x) < Mathf.Abs(b.max.x) ? 0f : -len;
                    p.a = new Vector3(x0, 0, b.center.z); p.b = new Vector3(x0 + len, 0, b.center.z); //mid-thickness
                    p.normal = Mathf.Abs(b.max.z) >= Mathf.Abs(b.min.z) ? Vector3.forward : Vector3.back;
                }
                else
                {
                    float len = Mathf.Round(b.size.z);
                    float z0 = Mathf.Abs(b.min.z) < Mathf.Abs(b.max.z) ? 0f : -len;
                    p.a = new Vector3(b.center.x, 0, z0); p.b = new Vector3(b.center.x, 0, z0 + len);
                    p.normal = Mathf.Abs(b.max.x) >= Mathf.Abs(b.min.x) ? Vector3.right : Vector3.left;
                }
            }
            else
            {
                //Which diagonal holds the mesh: count vertices near each pair of opposite corners
                Vector3 c00 = new Vector3(b.min.x, 0, b.min.z), c11 = new Vector3(b.max.x, 0, b.max.z);
                Vector3 c01 = new Vector3(b.min.x, 0, b.max.z), c10 = new Vector3(b.max.x, 0, b.min.z);
                int main = 0, anti = 0;
                foreach (var v in verts)
                {
                    var f = new Vector3(v.x, 0, v.z);
                    float dMain = DistToLine(f, c00, c11), dAnti = DistToLine(f, c01, c10);
                    if (dMain < dAnti) main++; else anti++;
                }
                //The wall runs from the pivot (0,0) out to the far corner on that diagonal; its 3 m legs are whole
                float s = Mathf.Round(Mathf.Max(b.size.x, b.size.z) - 0.1f);
                Vector3 far = main >= anti ? (Mathf.Abs(b.max.x) > Mathf.Abs(b.min.x) ? new Vector3(s, 0, s) : new Vector3(-s, 0, -s))
                                           : (Mathf.Abs(b.max.x) > Mathf.Abs(b.min.x) ? new Vector3(s, 0, -s) : new Vector3(-s, 0, s));
                p.a = Vector3.zero; p.b = far;
                //The thick side: the bounds stick out past the line on the panelled side
                Vector3 n = Vector3.Cross(Vector3.up, far).normalized;
                Vector3 centreOff = new Vector3(b.center.x, 0, b.center.z) - far * 0.5f;
                p.normal = Vector3.Dot(centreOff, n) >= 0 ? n : -n;
            }
            lines[path] = p;
            return p;
        }

        static float DistToLine(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 d = (b - a).normalized;
            return Vector3.Cross(d, p - a).magnitude;
        }

        static Bounds LocalBounds(GameObject go)
        {
            var parent = go.transform.parent;
            bool any = false; var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var wb = r.bounds;
                var c = parent != null ? parent.InverseTransformPoint(wb.center) : wb.center;
                var lb = new Bounds(c - go.transform.localPosition, wb.size);
                if (!any) { b = lb; any = true; } else b.Encapsulate(lb);
            }
            return b;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject Place(string path, Vector3 pos, Quaternion rot, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot * go.transform.localRotation;
            return go;
        }
    }
}
