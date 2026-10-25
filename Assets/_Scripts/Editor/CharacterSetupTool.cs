using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI;
using Rambler.Attributes;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Movement;
using Rambler.Saving;

namespace Rambler.EditorTools
{
    //Builds a playable character prefab from a new model by copying the gameplay setup
    //(components, weapon rig, target ring) from a working character, then pointing every
    //copied reference at the new model's own bones and objects.
    public class CharacterSetupTool : EditorWindow
    {
        const string ProfileFolder = "Assets/_Prefabs/Characters/Profiles";

        CharacterType type = CharacterType.Miner;
        CharacterSetupProfile profile;
        GameObject model;
        string characterName = "";
        bool placeInScene = true;
        Vector2 logScroll;
        string log = "";

        [MenuItem("Tools/Character Setup")]
        static void Open()
        {
            GetWindow<CharacterSetupTool>("Character Setup");
        }

        void OnEnable()
        {
            profile = FindProfile(type);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            type = (CharacterType)EditorGUILayout.EnumPopup("Type", type);
            if (EditorGUI.EndChangeCheck()) profile = FindProfile(type);

            profile = (CharacterSetupProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(CharacterSetupProfile), false);
            if (profile == null)
            {
                EditorGUILayout.HelpBox("No setup profile for " + type + " yet.", MessageType.Info);
                if (GUILayout.Button("Create " + type + " Profile")) profile = CreateProfile(type);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            model = (GameObject)EditorGUILayout.ObjectField("Model (FBX or prefab)", model, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && model != null && string.IsNullOrEmpty(characterName)) characterName = model.name;
            characterName = EditorGUILayout.TextField("Prefab Name", characterName);
            placeInScene = EditorGUILayout.Toggle("Place In Open Scene", placeInScene);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(profile == null || profile.sourceCharacter == null || model == null || string.IsNullOrEmpty(characterName)))
            {
                if (GUILayout.Button("Build Character", GUILayout.Height(30))) Build();
            }

            if (!string.IsNullOrEmpty(log))
            {
                EditorGUILayout.Space();
                logScroll = EditorGUILayout.BeginScrollView(logScroll);
                EditorGUILayout.HelpBox(log, MessageType.None);
                EditorGUILayout.EndScrollView();
            }
        }

        // ---------- Profiles ----------

        static CharacterSetupProfile FindProfile(CharacterType type)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterSetupProfile"))
            {
                var p = AssetDatabase.LoadAssetAtPath<CharacterSetupProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null && p.type == type) return p;
            }
            return null;
        }

        static CharacterSetupProfile CreateProfile(CharacterType type)
        {
            var p = CreateInstance<CharacterSetupProfile>();
            p.type = type;
            p.outputFolder = "Assets/_Prefabs/Characters/" + type + "s";
            switch (type)
            {
                case CharacterType.Miner:
                    //MeatBotLaw's AI and body animations, with the rig controller that has equip_pistol
                    p.sourceCharacter = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Characters/MeatBots/LawBot/MeatBotLaw.prefab");
                    p.bodyController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Prefabs/Weapons/Contollers/MeatBot Law.controller");
                    p.rigController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Prefabs/Characters/Humans/Security/AtlasAnims/AtlasMK3CharacterController.controller");
                    p.startingWeapon = AssetDatabase.LoadAssetAtPath<Weapon>("Assets/_Scripts/Resources/Pistol.asset");
                    break;
            }
            EnsureFolder(ProfileFolder);
            AssetDatabase.CreateAsset(p, AssetDatabase.GenerateUniqueAssetPath(ProfileFolder + "/" + type + " Setup.asset"));
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(p);
            return p;
        }

        // ---------- Build ----------

        void Build()
        {
            var report = new StringBuilder();
            GameObject source = profile.sourceCharacter;
            GameObject character = null;
            try
            {
                character = InstantiateModel(model);
                character.name = characterName;
                Transform root = character.transform;
                if (root.localScale != Vector3.one)
                    report.AppendLine("Note: the model's root scale is " + root.localScale + ". Set size with the import Scale Factor and keep the root at 1.");

                Avatar avatar = FindAvatar(model);
                if (avatar == null || !avatar.isHuman)
                    report.AppendLine("Warning: the model has no Humanoid avatar. Set Rig > Animation Type to Humanoid, or the body animations won't play.");

                //Children first, so copied components can be pointed at them
                var copiedChildren = new List<GameObject>();
                foreach (var childName in profile.childrenToCopy)
                {
                    Transform srcChild = source.transform.Find(childName);
                    if (srcChild == null) { report.AppendLine("Skipped child '" + childName + "': not on " + source.name + "."); continue; }
                    var existing = root.Find(childName);
                    if (existing != null) DestroyImmediate(existing.gameObject);
                    var copy = Instantiate(srcChild.gameObject, root);
                    copy.name = childName;
                    copy.transform.localPosition = srcChild.localPosition;
                    copy.transform.localRotation = srcChild.localRotation;
                    copy.transform.localScale = srcChild.localScale;
                    copiedChildren.Add(copy);
                }

                var copiedComponents = CopyRootComponents(source, character, report);
                foreach (var child in copiedChildren) copiedComponents.AddRange(child.GetComponentsInChildren<Component>(true));

                int remapped = RemapReferences(copiedComponents, source.transform, root, report);
                report.AppendLine("Repointed " + remapped + " references to " + characterName + ".");

                ApplyProfile(character, source, avatar, report);
                FitCollider(character, report);

                string path = SavePrefab(character, report);
                if (placeInScene)
                {
                    PlaceInScene(character);
                    Undo.RegisterCreatedObjectUndo(character, "Build " + characterName);
                    Selection.activeGameObject = character;
                    character = null; //Keep it
                }
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                report.AppendLine("Done: " + path);
            }
            catch (System.Exception e)
            {
                report.AppendLine("Build failed: " + e.Message);
                Debug.LogException(e);
            }
            finally
            {
                if (character != null) DestroyImmediate(character);
            }
            log = report.ToString();
            Debug.Log("[Character Setup] " + characterName + "\n" + log);
        }

        static GameObject InstantiateModel(GameObject model)
        {
            GameObject go;
            if (EditorUtility.IsPersistent(model)) go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            else go = Instantiate(model);
            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        static Avatar FindAvatar(GameObject model)
        {
            var anim = model.GetComponent<Animator>();
            if (anim != null && anim.avatar != null) return anim.avatar;
            string path = AssetDatabase.GetAssetPath(model);
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Avatar a) return a;
            return null;
        }

        //Copies every root component of the source onto the character, in the source's order
        static List<Component> CopyRootComponents(GameObject source, GameObject character, StringBuilder report)
        {
            var copied = new List<Component>();
            foreach (var srcComp in source.GetComponents<Component>())
            {
                if (srcComp == null) { report.AppendLine("Skipped a missing script on " + source.name + "."); continue; }
                if (srcComp is Transform) continue;

                var target = character.GetComponent(srcComp.GetType());
                ComponentUtility.CopyComponent(srcComp);
                if (target != null) ComponentUtility.PasteComponentValues(target);
                else
                {
                    ComponentUtility.PasteComponentAsNew(character);
                    var all = character.GetComponents(srcComp.GetType());
                    target = all[all.Length - 1];
                }
                copied.Add(target);
            }
            report.AppendLine("Copied " + copied.Count + " components from " + source.name + ".");
            return copied;
        }

        //Anything that still points inside the source character is moved to the same object on
        //the new character: first by path (RigLayers/...), then by name (bones such as mixamorig:RightHand)
        static int RemapReferences(List<Component> components, Transform sourceRoot, Transform root, StringBuilder report)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName.Add(t.name, t);

            int count = 0;
            var missing = new HashSet<string>();
            foreach (var comp in components)
            {
                if (comp == null || comp is Transform) continue;
                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                bool changed = false;
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = prop.propertyType == SerializedPropertyType.Generic;
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = prop.objectReferenceValue;
                    if (value == null || prop.name == "m_Script") continue;

                    Transform srcT = value is GameObject g ? g.transform : (value as Component)?.transform;
                    if (srcT == null || !srcT.IsChildOf(sourceRoot)) continue;

                    string path = AnimationUtility.CalculateTransformPath(srcT, sourceRoot);
                    Transform newT = path == "" ? root : root.Find(path);
                    if (newT == null) byName.TryGetValue(srcT.name, out newT);
                    if (newT == null) newT = CopyExtraObject(srcT, root, byName);

                    Object replacement = null;
                    if (newT != null) replacement = value is GameObject ? newT.gameObject : (Object)newT.GetComponent(value.GetType());
                    if (replacement == null) missing.Add(comp.GetType().Name + "." + prop.displayName + " (" + srcT.name + ")");

                    prop.objectReferenceValue = replacement;
                    changed = true;
                    if (replacement != null) count++;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var m in missing) report.AppendLine("Not found on the new model, left empty: " + m);
            return count;
        }

        //Helper objects the source added inside its skeleton (e.g. IK elbow hints under a shoulder)
        //don't exist on the new model, so copy them onto the matching bone
        static Transform CopyExtraObject(Transform srcT, Transform root, Dictionary<string, Transform> byName)
        {
            if (srcT.parent == null || srcT.GetComponentsInChildren<Component>(true).Length > srcT.GetComponentsInChildren<Transform>(true).Length)
                return null; //Only plain marker objects, never ones carrying components
            if (!byName.TryGetValue(srcT.parent.name, out Transform newParent)) return null;

            var copy = new GameObject(srcT.name).transform;
            copy.SetParent(newParent, false);
            copy.localPosition = srcT.localPosition;
            copy.localRotation = srcT.localRotation;
            copy.localScale = srcT.localScale;
            byName[copy.name] = copy;
            return copy;
        }

        void ApplyProfile(GameObject character, GameObject source, Avatar avatar, StringBuilder report)
        {
            Transform root = character.transform;
            if (!string.IsNullOrEmpty(profile.tag)) character.tag = profile.tag;
            int layer = LayerMask.NameToLayer(profile.layer);
            if (layer >= 0) character.layer = layer;

            var anim = character.GetComponent<Animator>();
            if (avatar != null) anim.avatar = avatar;
            if (profile.bodyController != null) anim.runtimeAnimatorController = profile.bodyController;
            anim.applyRootMotion = false; //The NavMeshAgent moves characters

            Transform rigLayers = root.Find("RigLayers");
            Animator rigAnim = rigLayers != null ? rigLayers.GetComponent<Animator>() : null;
            if (rigAnim != null && profile.rigController != null) rigAnim.runtimeAnimatorController = profile.rigController;

            var fighter = character.GetComponent<Fighter>();
            if (fighter != null)
            {
                var so = new SerializedObject(fighter);
                if (profile.startingWeapon != null) so.FindProperty("weaponConfig").objectReferenceValue = profile.startingWeapon;
                if (fighter.handTransform == null) so.FindProperty("handTransform").objectReferenceValue = FindHand(root, anim);
                if (rigAnim != null) so.FindProperty("rigController").objectReferenceValue = rigAnim;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (fighter.handTransform == null) report.AppendLine("Warning: no right hand bone found; set Fighter > Hand Transform by hand.");
            }

            var health = character.GetComponent<Health>();
            if (health != null)
            {
                var so = new SerializedObject(health);
                so.FindProperty("mover").objectReferenceValue = character.GetComponent<Mover>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var stats = character.GetComponent<Rambler.Stats.BaseStats>();
            if (stats != null)
            {
                var so = new SerializedObject(stats);
                var cls = so.FindProperty("characterClass");
                if (cls != null) cls.enumValueIndex = (int)profile.characterClass;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            //A blank ID makes each scene copy generate its own save ID
            var saveable = character.GetComponent<SaveableEntity>();
            if (saveable != null)
            {
                var so = new SerializedObject(saveable);
                so.FindProperty("uniqueIdentifier").stringValue = "";
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform icon = root.Find("SelectionIcon");
            if (icon != null) icon.gameObject.SetActive(false);
        }

        static Transform FindHand(Transform root, Animator anim)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("RightHandIndex1")) return t;
            if (anim != null && anim.isHuman)
            {
                var bone = anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                if (bone == null) bone = anim.GetBoneTransform(HumanBodyBones.RightHand);
                return bone;
            }
            return null;
        }

        //Size the capsule and NavMeshAgent to the model's mesh
        static void FitCollider(GameObject character, StringBuilder report)
        {
            var renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            Transform root = character.transform;
            float scale = Mathf.Max(0.0001f, root.lossyScale.y);
            float height = b.size.y / scale;
            float radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) / scale, 0.2f, height * 0.25f);

            var capsule = character.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.center = new Vector3(0f, (b.center.y - root.position.y) / scale, 0f);
                capsule.height = height;
                capsule.radius = radius;
            }
            var agent = character.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.height = height;
                agent.radius = radius;
                agent.baseOffset = 0f;
            }
            report.AppendLine("Collider fitted to the mesh: height " + height.ToString("F2") + ", radius " + radius.ToString("F2") + ".");
        }

        string SavePrefab(GameObject character, StringBuilder report)
        {
            EnsureFolder(profile.outputFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(profile.outputFolder + "/" + characterName + ".prefab");
            if (placeInScene) PrefabUtility.SaveAsPrefabAssetAndConnect(character, path, InteractionMode.AutomatedAction, out bool ok);
            else PrefabUtility.SaveAsPrefabAsset(character, path);
            return path;
        }

        static void PlaceInScene(GameObject character)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            //Drop it on the ground under the Scene view's focus point
            Vector3 pivot = view.pivot;
            if (Physics.Raycast(pivot + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f)) pivot = hit.point;
            character.transform.position = pivot;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
