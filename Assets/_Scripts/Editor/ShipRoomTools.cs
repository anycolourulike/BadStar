using System.Text;
using UnityEditor;
using UnityEngine;
using Rambler.Combat;
using Rambler.Ship;

namespace Rambler.EditorTools
{
    //Tools ▸ Ship: lay out ship rooms (a box collider trigger) with a security camera in each,
    //and check that every room has one.
    public static class ShipRoomTools
    {
        const string CameraPrefabPath = "Assets/_Prefabs/Ship/ShipCamera.prefab";
        const float WallInset = 0.4f;

        [MenuItem("Tools/Ship/Create Room")]
        static void CreateRoom()
        {
            var room = new GameObject("Room_New");
            Undo.RegisterCreatedObjectUndo(room, "Create Ship Room");
            var parent = Selection.activeTransform;
            if (parent != null && parent.GetComponent<ShipRoom>() != null) parent = parent.parent; //Rooms sit side by side
            if (parent != null) room.transform.SetParent(parent, false);

            //Drop it where the Scene view is looking, floor at ground level
            var view = SceneView.lastActiveSceneView;
            var position = view != null ? view.pivot : Vector3.zero;
            room.transform.position = new Vector3(position.x, 2f, position.z);

            var box = room.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10f, 4f, 10f);
            var shipRoom = room.AddComponent<ShipRoom>();
            shipRoom.roomName = "New Room";

            AddCamera(room.GetComponent<ShipRoom>());
            Selection.activeGameObject = room;
        }

        [MenuItem("Tools/Ship/Add Camera To Selected Rooms")]
        static void AddCameraToSelection()
        {
            int added = 0;
            foreach (var go in Selection.gameObjects)
            {
                var room = go.GetComponentInParent<ShipRoom>();
                if (room == null) continue;
                AddCamera(room);
                added++;
            }
            if (added == 0) Debug.LogWarning("[Ship] Select one or more rooms (objects with ShipRoom) first.");
        }

        [MenuItem("Tools/Ship/Check Rooms")]
        static void CheckRooms()
        {
            var report = new StringBuilder();
            int problems = 0;
            var rooms = Object.FindObjectsOfType<ShipRoom>();
            var cameras = Object.FindObjectsOfType<ShipCamera>();
            foreach (var room in rooms)
            {
                var box = room.GetComponent<BoxCollider>();
                if (!box.isTrigger)
                {
                    report.AppendLine(room.name + ": box collider should be a trigger");
                    problems++;
                }
                int count = 0;
                foreach (var cam in cameras)
                {
                    if (room.Contains(cam.transform.position)) count++;
                }
                if (count == 0)
                {
                    report.AppendLine(room.name + ": no camera inside the room (Tools ▸ Ship ▸ Add Camera To Selected Rooms)");
                    problems++;
                }
            }
            foreach (var cam in cameras)
            {
                bool inRoom = false;
                foreach (var room in rooms) inRoom |= room.Contains(cam.transform.position);
                if (!inRoom)
                {
                    report.AppendLine(cam.name + ": camera is outside every room");
                    problems++;
                }
            }
            if (problems == 0) Debug.Log("[Ship] " + rooms.Length + " rooms, " + cameras.Length + " cameras: all good.");
            else Debug.LogWarning("[Ship] " + problems + " problem(s):\n" + report);
        }

        //Mounts a camera high in the room's far corner, looking down across the room
        public static ShipCamera AddCamera(ShipRoom room)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);
            if (prefab == null) prefab = BuildCameraPrefab();

            var box = room.GetComponent<BoxCollider>();
            var half = box.size * 0.5f;
            var local = box.center + new Vector3(half.x - WallInset, half.y - WallInset, half.z - WallInset);
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room.transform);
            Undo.RegisterCreatedObjectUndo(cam, "Add Ship Camera");
            cam.name = "Camera_" + room.name.Replace("Room_", "");
            cam.transform.position = room.transform.TransformPoint(local);
            var lookAt = room.transform.TransformPoint(box.center - new Vector3(0f, half.y * 0.5f, 0f));
            cam.transform.rotation = Quaternion.LookRotation(lookAt - cam.transform.position);
            return cam.GetComponent<ShipCamera>();
        }

        //A placeholder security camera: wall mount, lens, status light, and the shootable hitbox.
        //Swap the Model / Broken children for real art; keep the components on the root and Hitbox.
        static GameObject BuildCameraPrefab()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var body = MakeMaterial("ShipCameraBody", shader, new Color(0.22f, 0.24f, 0.27f), false);
            var lens = MakeMaterial("ShipCameraLens", shader, new Color(0.05f, 0.05f, 0.06f), false);
            var led = MakeMaterial("ShipCameraLED", shader, new Color(1f, 0.15f, 0.1f), true);

            var root = new GameObject("ShipCamera");
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            Part(PrimitiveType.Cube, "Housing", model.transform, new Vector3(0f, 0f, -0.12f), Vector3.zero, new Vector3(0.22f, 0.2f, 0.42f), body);
            Part(PrimitiveType.Cylinder, "Lens", model.transform, new Vector3(0f, 0f, 0.1f), new Vector3(90f, 0f, 0f), new Vector3(0.14f, 0.04f, 0.14f), lens);
            Part(PrimitiveType.Sphere, "LED", model.transform, new Vector3(0.07f, 0.08f, 0.06f), Vector3.zero, Vector3.one * 0.04f, led);
            Part(PrimitiveType.Cube, "Mount", model.transform, new Vector3(0f, 0.08f, -0.35f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.2f), body);

            var broken = new GameObject("Broken");
            broken.transform.SetParent(root.transform, false);
            Part(PrimitiveType.Cube, "Housing", broken.transform, new Vector3(0f, -0.08f, -0.14f), new Vector3(35f, 0f, 15f), new Vector3(0.22f, 0.2f, 0.42f), lens);
            Part(PrimitiveType.Cube, "Mount", broken.transform, new Vector3(0f, 0.08f, -0.35f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.2f), body);
            broken.SetActive(false);

            var lightGo = new GameObject("StatusLight");
            lightGo.transform.SetParent(model.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2.5f;
            light.intensity = 1.5f;
            light.color = Color.green;

            var hitbox = new GameObject("Hitbox");
            hitbox.transform.SetParent(root.transform, false);
            hitbox.transform.localPosition = Vector3.down * 0.9f;
            var capsule = hitbox.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.radius = 0.3f;
            capsule.height = 0.8f;
            hitbox.AddComponent<CombatTarget>();
            hitbox.AddComponent<ShipCameraHitbox>();

            var sensor = root.AddComponent<ShipSensor>();
            var sensorSo = new SerializedObject(sensor);
            sensorSo.FindProperty("targetMask").intValue = LayerMask.GetMask("Player");
            sensorSo.FindProperty("obstructionMask").intValue = LayerMask.GetMask("Obstruction");
            sensorSo.FindProperty("statusLight").objectReferenceValue = light;
            sensorSo.ApplyModifiedPropertiesWithoutUndo();

            var camera = root.AddComponent<ShipCamera>();
            var camSo = new SerializedObject(camera);
            camSo.FindProperty("intactModel").objectReferenceValue = model;
            camSo.FindProperty("brokenModel").objectReferenceValue = broken;
            camSo.FindProperty("hitbox").objectReferenceValue = hitbox.transform;
            var fx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonParticleFX/Prefabs/FX_Explosion_01.prefab");
            if (fx != null) camSo.FindProperty("destroyFX").objectReferenceValue = fx;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder("Assets/_Prefabs/Ship")) AssetDatabase.CreateFolder("Assets/_Prefabs", "Ship");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CameraPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Ship] Created " + CameraPrefabPath);
            return prefab;
        }

        static void Part(PrimitiveType shape, string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>()); //Only the hitbox should be hit
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Material MakeMaterial(string name, Shader shader, Color color, bool emissive)
        {
            string path = "Assets/_Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
