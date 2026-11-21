using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Rambler.Combat;

namespace Rambler.EditorTools
{
    //The import and fitting steps the Character Setup tool runs around its component copy, learned the hard way:
    //  PrepareModel     Humanoid avatar; real-world height via the import scale (rebuilding the avatar afterwards, or it keeps the
    //                   old skeleton and poses everything ~80x too big); explicit Mixamo bone mapping when Unity's auto-mapping
    //                   fails (e.g. hands with no ring/little fingers); materials converted to URP Lit with their textures.
    //  FitWeaponRig     Moves the copied RigLayers by the difference in chest height, so a shorter body doesn't hold the gun at its head.
    //  PackHandMapping  Works out where the TacticalMoves pack's guns sit in THIS skeleton's right hand (CoverKneelAim needs it).
    //  Validate         Avatar, height, hand socket, IK links, skinned mesh bones, materials.
    public static class CharacterPipeline
    {
        const string PackRoot = "Assets/Wolff's Studio/TacticalMoves/";

        // ---------- Prepare the model ----------

        public static Avatar PrepareModel(GameObject modelAsset, float targetHeight, StringBuilder r)
        {
            string path = AssetDatabase.GetAssetPath(modelAsset);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { r.AppendLine("Prepare: not a model file (" + path + "). Pick the FBX."); return null; }

            bool changed = false;
            if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; changed = true; }
            if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; }
            if (mi.importAnimation) { mi.importAnimation = false; changed = true; }
            if (changed) mi.SaveAndReimport();

            float h = MeasureHeight(path);
            r.AppendLine("Prepare: height at import scale " + mi.globalScale + " is " + h.ToString("F2") + " m.");
            if (targetHeight > 0f && h > 0.01f && Mathf.Abs(h - targetHeight) / targetHeight > 0.12f)
            {
                float before = mi.globalScale;
                mi.globalScale = before * targetHeight / h;
                ResetAvatar(mi);
                r.AppendLine("Prepare: import scale " + before + " -> " + mi.globalScale.ToString("0.####") + " for " + targetHeight + " m (avatar rebuilt from the new size).");
            }

            Avatar avatar = FindAvatar(path);
            if (avatar == null || !avatar.isHuman)
            {
                r.AppendLine("Prepare: Unity's automatic bone mapping failed; mapping the Mixamo bones by name.");
                MapMixamoBones(mi, path, r);
                avatar = FindAvatar(path);
            }
            r.AppendLine(avatar != null && avatar.isHuman ? "Prepare: Humanoid avatar OK (" + avatar.name + ")." : "Prepare: FAILED to make a Humanoid avatar. Check the bone names in the FBX.");

            ConvertMaterials(mi, path, r);
            return avatar;
        }

        static float MeasureHeight(string path)
        {
            var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true)) { lo = Mathf.Min(lo, rend.bounds.min.y); hi = Mathf.Max(hi, rend.bounds.max.y); }
            Object.DestroyImmediate(go);
            return hi > lo ? hi - lo : 0f;
        }

        //Clears the stored mapping and skeleton so the avatar is rebuilt from the model as it imports now
        static void ResetAvatar(ModelImporter mi)
        {
            mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar; mi.SaveAndReimport();
            var hd = mi.humanDescription; hd.human = new UnityEngine.HumanBone[0]; hd.skeleton = new SkeletonBone[0]; mi.humanDescription = hd;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mi.SaveAndReimport();
        }

        static Avatar FindAvatar(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) return a;
            return null;
        }

        //Unity human bone -> Mixamo bone (without the "mixamorig:" prefix). Fingers are optional.
        static readonly string[,] BodyMap = {
            {"Hips","Hips"},{"Spine","Spine"},{"Chest","Spine1"},{"UpperChest","Spine2"},{"Neck","Neck"},{"Head","Head"},
            {"LeftShoulder","LeftShoulder"},{"LeftUpperArm","LeftArm"},{"LeftLowerArm","LeftForeArm"},{"LeftHand","LeftHand"},
            {"RightShoulder","RightShoulder"},{"RightUpperArm","RightArm"},{"RightLowerArm","RightForeArm"},{"RightHand","RightHand"},
            {"LeftUpperLeg","LeftUpLeg"},{"LeftLowerLeg","LeftLeg"},{"LeftFoot","LeftFoot"},{"LeftToes","LeftToeBase"},
            {"RightUpperLeg","RightUpLeg"},{"RightLowerLeg","RightLeg"},{"RightFoot","RightFoot"},{"RightToes","RightToeBase"} };

        static void MapMixamoBones(ModelImporter mi, string path, StringBuilder r)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var byShort = new Dictionary<string, string>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string s = t.name.Contains(":") ? t.name.Substring(t.name.LastIndexOf(':') + 1) : t.name;
                if (!byShort.ContainsKey(s)) byShort[s] = t.name;
            }
            var human = new List<UnityEngine.HumanBone>();
            System.Action<string, string> Add = (h, m) => { if (byShort.TryGetValue(m, out var full)) { var hb = new UnityEngine.HumanBone { humanName = h, boneName = full }; hb.limit.useDefaultValues = true; human.Add(hb); } };
            for (int i = 0; i < BodyMap.GetLength(0); i++) Add(BodyMap[i, 0], BodyMap[i, 1]);
            string[] seg = { "Proximal", "Intermediate", "Distal" };
            foreach (var side in new[] { "Left", "Right" })
                foreach (var f in new[] { ("Thumb", "Thumb"), ("Index", "Index"), ("Middle", "Middle"), ("Ring", "Ring"), ("Little", "Pinky") })
                    for (int i = 0; i < 3; i++) Add(side + " " + f.Item1 + " " + seg[i], side + "Hand" + f.Item2 + (i + 1));
            int body = 0; foreach (var hb in human) if (!hb.humanName.Contains(" ")) body++;
            r.AppendLine("Prepare: mapped " + body + " body bones and " + (human.Count - body) + " finger bones by name.");
            var skel = new List<SkeletonBone>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                skel.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            var hd = mi.humanDescription; hd.human = human.ToArray(); hd.skeleton = skel.ToArray();
            hd.armStretch = 0.05f; hd.legStretch = 0.05f; hd.upperArmTwist = 0.5f; hd.lowerArmTwist = 0.5f; hd.upperLegTwist = 0.5f; hd.lowerLegTwist = 0.5f;
            mi.humanDescription = hd; mi.SaveAndReimport();
        }

        //Embedded/legacy materials become URP Lit materials beside the model, keeping their colour and texture
        static void ConvertMaterials(ModelImporter mi, string path, StringBuilder r)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) return;
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            var existing = mi.GetExternalObjectMap();
            int made = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is Material src)) continue;
                var id = new AssetImporter.SourceAssetIdentifier(src);
                if (existing.ContainsKey(id)) continue;
                if (src.shader == urp) continue;
                Texture tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
                if (tex == null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                Color col = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
                string matPath = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + src.name + ".mat");
                var m = new Material(urp) { name = src.name };
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", col); m.SetFloat("_Smoothness", 0.15f); m.SetFloat("_Metallic", 0f);
                AssetDatabase.CreateAsset(m, matPath);
                mi.AddRemap(id, m); made++;
            }
            if (made > 0) { mi.SaveAndReimport(); r.AppendLine("Prepare: made " + made + " URP material(s) next to the model."); }
        }

        // ---------- Fit the copied weapon rig ----------

        //The shared weapon poses place the hands relative to RigLayers, tuned for the source's body. Shift RigLayers by the
        //difference in chest height so the gun ends up at the new body's chest.
        public static void FitWeaponRig(GameObject character, GameObject source, StringBuilder r)
        {
            var rig = character.transform.Find("RigLayers");
            if (rig == null) return;
            float srcY = ChestHeight(source), newY = ChestHeight(character);
            if (srcY <= 0f || newY <= 0f) { r.AppendLine("Weapon rig: couldn't find the chest bones to compare; RigLayers left as copied."); return; }
            float delta = (newY - srcY) / Mathf.Max(0.0001f, character.transform.lossyScale.y);
            if (Mathf.Abs(delta) < 0.02f) { r.AppendLine("Weapon rig: chest heights match; RigLayers unchanged."); return; }
            rig.localPosition += new Vector3(0f, delta, 0f);
            r.AppendLine("Weapon rig: chest " + newY.ToString("F2") + " m vs source " + srcY.ToString("F2") + " m; RigLayers moved " + delta.ToString("+0.00;-0.00") + " (check the gun height in play).");
        }

        //Chest height above the feet, in metres, with the body in the same neutral humanoid pose (all muscles at zero) so different
        //rest poses (A-pose, T-pose) don't skew the comparison. Works on prefab assets (measured on a temporary copy).
        static float ChestHeight(GameObject go)
        {
            var src = go.GetComponent<Animator>();
            if (src == null || src.avatar == null || !src.avatar.isHuman) return 0f;
            var tmp = (GameObject)Object.Instantiate(go, new Vector3(0f, -500f, 0f), Quaternion.identity);
            try
            {
                var an = tmp.GetComponent<Animator>();
                var h = new HumanPoseHandler(an.avatar, tmp.transform);
                var pose = new HumanPose(); h.GetHumanPose(ref pose);
                for (int i = 0; i < pose.muscles.Length; i++) pose.muscles[i] = 0f;
                h.SetHumanPose(ref pose); h.Dispose();
                Transform chest = FindHumanBone(tmp, an.avatar, "UpperChest") ?? FindHumanBone(tmp, an.avatar, "Chest");
                Transform lf = FindHumanBone(tmp, an.avatar, "LeftFoot"), rf = FindHumanBone(tmp, an.avatar, "RightFoot");
                if (chest == null || lf == null || rf == null) return 0f;
                float feet = Mathf.Min(lf.position.y, rf.position.y);
                return chest.position.y - feet;
            }
            finally { Object.DestroyImmediate(tmp); }
        }

        static Transform FindHumanBone(GameObject go, Avatar avatar, string humanName)
        {
            string bone = null;
            foreach (var hb in avatar.humanDescription.human) if (hb.humanName == humanName) bone = hb.boneName;
            if (bone == null) return null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == bone) return t;
            return null;
        }

        // ---------- TacticalMoves gun-in-hand mapping ----------

        public static void PackHandMapping(GameObject character, StringBuilder r)
        {
            var aim = character.GetComponent<CoverKneelAim>();
            var an = character.GetComponent<Animator>();
            if (aim == null) return;
            if (an == null || an.avatar == null || !an.avatar.isHuman) { r.AppendLine("Pack mapping: skipped (no Humanoid avatar)."); return; }
            if (!AssetDatabase.IsValidFolder(PackRoot.TrimEnd('/'))) { r.AppendLine("Pack mapping: TacticalMoves pack not found; kept the copied values."); return; }

            //Remember the skeleton so the prefab is saved in its rest pose
            var bones = character.GetComponentsInChildren<Transform>(true);
            var saved = new (Vector3, Quaternion, Vector3)[bones.Length];
            for (int i = 0; i < bones.Length; i++) saved[i] = (bones[i].localPosition, bones[i].localRotation, bones[i].localScale);
            Vector3 rootPos = character.transform.position; Quaternion rootRot = character.transform.rotation;
            var so = new SerializedObject(aim);
            try
            {
                character.transform.SetPositionAndRotation(new Vector3(610, 0, 600), Quaternion.identity);
                foreach (var set in new[] { ("HandGun", "P_Gun", "packBarrelInHand", "packUpInHand"), ("SMG", "P_SMG", "packSmgBarrelInHand", "packSmgUpInHand") })
                {
                    if (!PackAxesInHand(set.Item1, set.Item2, character, an, out Vector3 barrel, out Vector3 up)) { r.AppendLine("Pack mapping: " + set.Item1 + " clip or prop missing; kept the copied values."); continue; }
                    so.FindProperty(set.Item3).vector3Value = barrel; so.FindProperty(set.Item4).vector3Value = up;
                    r.AppendLine("Pack mapping: " + set.Item1 + " barrel in hand " + barrel.ToString("F2") + ".");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                for (int i = 0; i < bones.Length; i++) { bones[i].localPosition = saved[i].Item1; bones[i].localRotation = saved[i].Item2; bones[i].localScale = saved[i].Item3; }
                character.transform.SetPositionAndRotation(rootPos, rootRot);
            }
        }

        //Poses the pack's own skeleton in its standing aim clip with its prop gun on the right hand (identity offset, as in the pack),
        //copies that exact human pose onto the character, and expresses the prop's barrel and up in the character's hand-bone space.
        static bool PackAxesInHand(string weapon, string prop, GameObject character, Animator an, out Vector3 barrel, out Vector3 up)
        {
            barrel = Vector3.forward; up = Vector3.up;
            string clipPath = PackRoot + "Animations/" + weapon + "/Aim/Anim_" + weapon + "Aim01.fbx";
            var mf = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + "Props/" + prop + ".fbx")?.GetComponentInChildren<MeshFilter>();
            AnimationClip clip = null; Avatar packAvatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(clipPath)) { if (o is AnimationClip c && !c.name.StartsWith("__")) clip = c; if (o is Avatar a) packAvatar = a; }
            if (clip == null || packAvatar == null || mf == null) return false;

            var sam = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(clipPath), new Vector3(600, 0, 600), Quaternion.identity);
            try
            {
                var san = sam.GetComponent<Animator>(); if (san == null) san = sam.AddComponent<Animator>(); san.avatar = packAvatar;
                AnimationMode.StartAnimationMode();
                try { AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(sam, clip, clip.length * 0.5f); AnimationMode.EndSampling(); }
                finally { }
                var sh = san.GetBoneTransform(HumanBodyBones.RightHand);
                var sight = ((sh.position + san.GetBoneTransform(HumanBodyBones.LeftHand).position) * 0.5f - san.GetBoneTransform(HumanBodyBones.Head).position).normalized;
                //Barrel = the prop axis along the sight line; up = the most vertical of the rest
                var mr = mf.transform.localRotation; Vector3[] ax = { Vector3.right, Vector3.up, Vector3.forward };
                int a = 0; float best = -1f;
                for (int i = 0; i < 3; i++) { float d = Mathf.Abs(Vector3.Dot(sh.rotation * (mr * ax[i]), sight)); if (d > best) { best = d; a = i; } }
                Vector3 bS = mr * ax[a]; if (Vector3.Dot(sh.rotation * bS, sight) < 0f) bS = -bS;
                int b = -1; best = -1f;
                for (int i = 0; i < 3; i++) { if (i == a) continue; float d = Mathf.Abs(Vector3.Dot(sh.rotation * (mr * ax[i]), Vector3.up)); if (d > best) { best = d; b = i; } }
                Vector3 uS = mr * ax[b]; if (Vector3.Dot(sh.rotation * uS, Vector3.up) < 0f) uS = -uS;
                //The same human pose on the character, then the rotation between the two hand bones
                var hs = new HumanPoseHandler(packAvatar, sam.transform); var pose = new HumanPose(); hs.GetHumanPose(ref pose); hs.Dispose();
                AnimationMode.StopAnimationMode();
                var hc = new HumanPoseHandler(an.avatar, character.transform); hc.SetHumanPose(ref pose); hc.Dispose();
                var ch = an.GetBoneTransform(HumanBodyBones.RightHand);
                if (ch == null) return false;
                var D = Quaternion.Inverse(ch.rotation) * sh.rotation;
                barrel = (D * bS).normalized; up = (D * uS).normalized;
                return true;
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(sam);
            }
        }

        // ---------- Validate ----------

        public static void Validate(GameObject character, StringBuilder r)
        {
            int problems = 0;
            System.Action<bool, string> Check = (ok, msg) => { r.AppendLine((ok ? "  OK   " : "  FIX  ") + msg); if (!ok) problems++; };
            r.AppendLine("Checks:");
            var an = character.GetComponent<Animator>();
            Check(an != null && an.avatar != null && an.avatar.isHuman && an.avatar.isValid, "Humanoid avatar" + (an != null && an.avatar != null ? " (" + an.avatar.name + ")" : ""));
            var smrs = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var s in smrs) { lo = Mathf.Min(lo, s.bounds.min.y); hi = Mathf.Max(hi, s.bounds.max.y); }
            float h = hi > lo ? hi - lo : 0f;
            Check(h > 1.3f && h < 2.6f, "Height " + h.ToString("F2") + " m (expected 1.3 to 2.6)");
            int badBones = 0, badMats = 0;
            foreach (var s in smrs)
            {
                foreach (var b in s.bones) if (b == null || !b.IsChildOf(character.transform)) badBones++;
                foreach (var m in s.sharedMaterials) if (m == null || !m.shader.name.StartsWith("Universal Render Pipeline")) badMats++;
            }
            Check(smrs.Length > 0 && badBones == 0, smrs.Length + " skinned mesh(es), " + badBones + " bone(s) pointing outside the character");
            Check(badMats == 0, badMats + " material slot(s) empty or not URP");
            var f = character.GetComponent<Fighter>();
            if (f != null) Check(f.handTransform != null && f.handTransform.IsChildOf(character.transform), "Fighter hand socket" + (f.handTransform != null ? " (" + f.handTransform.name + ")" : ""));
            foreach (var ik in character.GetComponentsInChildren<TwoBoneIKConstraint>(true))
            {
                var d = ik.data;
                Check(d.root != null && d.mid != null && d.tip != null && d.root.IsChildOf(character.transform), "IK '" + ik.name + "' bones" + (d.tip != null ? " (tip " + d.tip.name + ")" : ""));
            }
            var aim = character.GetComponent<CoverKneelAim>();
            if (aim != null)
            {
                var so = new SerializedObject(aim);
                Check(so.FindProperty("packBarrelInHand").vector3Value != Vector3.forward, "CoverKneelAim pack hand mapping set");
            }
            r.AppendLine(problems == 0 ? "All checks passed." : problems + " check(s) need attention.");
        }
    }
}
