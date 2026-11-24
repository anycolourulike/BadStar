using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Rambler.Combat;
using Rambler.Crew;
using Rambler.Stats;

namespace Rambler.EditorTools
{
    //Play-mode controls on the Company object (DontDestroyOnLoad > Company): hire, fire, deploy into the open
    //scene, finish a mission. Stands in for the Recruitment and Management screens until they exist.
    [CustomEditor(typeof(Company))]
    public class CompanyEditor : Editor
    {
        int payout = 500;
        int addCredits = 500;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var company = (Company)target;
            if (!Application.isPlaying) { EditorGUILayout.HelpBox("Hire, fire and deploy controls appear in play mode.", MessageType.Info); return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Credits: " + company.Credits + "    Crew: " + company.Crew.Count + "/" + company.MaxCrew, EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                addCredits = EditorGUILayout.IntField(addCredits);
                if (GUILayout.Button("Add Credits")) company.AddCredits(addCredits);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("For hire", EditorStyles.boldLabel);
            if (company.RecruitPool == null) EditorGUILayout.HelpBox("No Recruit Pool. Run Tools > Company > Create Starter Content.", MessageType.Warning);
            foreach (var r in new List<CharacterRecord>(company.Candidates))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(Describe(r), GUILayout.MinWidth(200));
                    bool ok = company.CanHire(r, out string reason);
                    using (new EditorGUI.DisabledScope(!ok))
                        if (GUILayout.Button(new GUIContent("Hire " + r.hireFee, reason), GUILayout.Width(90))) company.Hire(r);
                }
            }
            if (GUILayout.Button("New Recruit List")) company.RefreshCandidates();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Crew", EditorStyles.boldLabel);
            var deployer = FindObjectOfType<SquadDeployer>();
            foreach (var r in new List<CharacterRecord>(company.Crew))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(Describe(r) + "  [" + r.status + (r.missionsOut > 0 ? " " + r.missionsOut : "") + "]", GUILayout.MinWidth(200));
                    using (new EditorGUI.DisabledScope(deployer == null || !r.CanDeploy))
                        if (GUILayout.Button(new GUIContent("Deploy", deployer == null ? "No SquadDeployer in the scene" : ""), GUILayout.Width(60))) deployer.Deploy(r);
                    if (GUILayout.Button("Fire", GUILayout.Width(45))) company.Fire(r);
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                payout = EditorGUILayout.IntField("Payout", payout);
                if (GUILayout.Button("Complete Mission")) company.CompleteMission(payout);
            }

            if (company.Former.Count > 0 || company.Fallen.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Left / fallen", EditorStyles.boldLabel);
                foreach (var r in company.Former) EditorGUILayout.LabelField(r.displayName + "  [" + r.status + "]");
                foreach (var r in company.Fallen) EditorGUILayout.LabelField(r.displayName + "  [" + r.status + "]");
            }
        }

        static string Describe(CharacterRecord r)
        {
            var skills = new List<string>();
            foreach (var s in r.skills)
            {
                var def = Rambler.Ownables.CatalogAsset.GetFromID<SkillDefinition>(s.skillId);
                if (def != null && s.level > 0) skills.Add(def.DisplayName + " " + s.level);
            }
            return r.displayName + (r.generated ? "" : " *") + "  wage " + r.wage + (skills.Count > 0 ? "  (" + string.Join(", ", skills) + ")" : "");
        }

        // ---------- Starter content ----------

        const string Root = "Assets/_Scripts/Resources/Company";
        const string HayesPrefab = "Assets/_Prefabs/Characters/Humans/Hayes/Hayes.prefab";
        const string WeaponRoot = "Assets/_Scripts/Resources/";

        //Skills, Hayes as a named recruit, a generic Contractor body, two training courses and a recruit pool.
        //Existing assets are left alone, so it's safe to run again.
        [MenuItem("Tools/Company/Create Starter Content")]
        static void CreateStarterContent()
        {
            Folder(Root); Folder(Root + "/Skills"); Folder(Root + "/Characters"); Folder(Root + "/Training");

            var firearms = Skill("Firearms", "Accuracy and handling with guns.");
            var melee = Skill("Melee", "Fighting up close.");
            var fitness = Skill("Fitness", "Toughness: +5% health per level.", (Stat.Health, 0f, 5f));
            var hacking = Skill("Hacking", "Breaking into robots, doors and terminals.");
            var medic = Skill("Medic", "Patching up the squad.");
            var engineering = Skill("Engineering", "Repairs, demolitions and upgrades.");
            var piloting = Skill("Piloting", "Flying ships and drones.");
            var stealth = Skill("Stealth", "Moving unseen.");

            var hayesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HayesPrefab);
            var portrait = hayesPrefab != null && hayesPrefab.GetComponent<Rambler.Ship.CharacterIdentity>() != null ? hayesPrefab.GetComponent<Rambler.Ship.CharacterIdentity>().Portrait : null;

            var hayes = Asset<CharacterDefinition>(Root + "/Characters/Hayes.asset", so =>
            {
                Common(so, "Hayes", "Ex-marine. Reliable with a rifle and hard to put down.", portrait, 600);
                so.FindProperty("prefab").objectReferenceValue = hayesPrefab;
                so.FindProperty("unique").boolValue = true;
                so.FindProperty("wagePerMission").intValue = 60;
                Skills(so, (firearms, 4), (fitness, 3), (melee, 1));
                Weapons(so, "Rifle Hayes", "Pistol Hayes");
            });
            var contractor = Asset<CharacterDefinition>(Root + "/Characters/Contractor.asset", so =>
            {
                Common(so, "Contractor", "A hired gun. Body for generated recruits (uses the Hayes model until more are made).", portrait, 250);
                so.FindProperty("prefab").objectReferenceValue = hayesPrefab;
                so.FindProperty("unique").boolValue = false;
                so.FindProperty("wagePerMission").intValue = 30;
                Weapons(so, "Pistol Hayes");
            });

            Asset<TrainingCourse>(Root + "/Training/Firearms Course.asset", so =>
            {
                Common(so, "Firearms Course", "Range time with an instructor.", null, 300);
                so.FindProperty("skill").objectReferenceValue = firearms;
            });
            Asset<TrainingCourse>(Root + "/Training/Hacking Course.asset", so =>
            {
                Common(so, "Hacking Course", "Intrusion drills on old terminals.", null, 350);
                so.FindProperty("skill").objectReferenceValue = hacking;
            });

            Asset<RecruitPool>(Root + "/Recruit Pool.asset", so =>
            {
                var named = so.FindProperty("namedRecruits");
                named.arraySize = 1; named.GetArrayElementAtIndex(0).objectReferenceValue = hayes;
                var templates = so.FindProperty("templates");
                templates.arraySize = 2;
                Template(templates.GetArrayElementAtIndex(0), "Soldier", contractor, 3, new[] { firearms, melee, fitness, stealth }, "Pistol Hayes", "SMG Hayes", "Rifle Hayes", "Shotgun Hayes");
                Template(templates.GetArrayElementAtIndex(1), "Specialist", contractor, 1, new[] { hacking, medic, engineering, piloting, firearms }, "Pistol Hayes", "SMG Hayes");
            });

            CreateUpgrades(firearms);

            AssetDatabase.SaveAssets();
            Debug.Log("[Company] Starter content is in " + Root + ". Enter play mode and select DontDestroyOnLoad > Company to hire.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(Root));
        }

        // ---------- Upgrades and the shop ----------

        //Attachments, gear and a shop catalogue; also gives the Firearms skill its aim bonus (-4% spread,
        //-3% recoil a level) if it has none yet. Existing upgrade assets are left alone.
        static void CreateUpgrades(SkillDefinition firearms)
        {
            Folder(Root + "/Upgrades");
            if (firearms != null)
            {
                var fso = new SerializedObject(firearms);
                var per = fso.FindProperty("combatPerLevel");
                if (per.FindPropertyRelative("spread").floatValue == 0f && per.FindPropertyRelative("kick").floatValue == 0f)
                {
                    per.FindPropertyRelative("spread").floatValue = -4f;
                    per.FindPropertyRelative("kick").floatValue = -3f;
                    fso.FindProperty("description").stringValue = "Accuracy and handling with guns: -4% spread and -3% recoil a level.";
                    fso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(firearms);
                }
            }

            const WeaponFamily guns = WeaponFamily.Pistol | WeaponFamily.SMG | WeaponFamily.Rifle;
            var ups = new List<UpgradeDefinition>
            {
                Upgrade("Red-dot Sight", "A quick reflex sight.", UpgradeSlot.Scope, guns, 250, spread: -20),
                Upgrade("4x Scope", "Magnified optic for picking shots at range.", UpgradeSlot.Scope, WeaponFamily.Rifle, 400, spread: -35, fireRate: -10),
                Upgrade("Long Barrel", "Higher muzzle velocity, harder hits.", UpgradeSlot.Barrel, WeaponFamily.All & ~WeaponFamily.Launcher, 350, damage: 12, spread: -10),
                Upgrade("Compensator", "Vents gas to keep the muzzle down.", UpgradeSlot.Barrel, guns, 300, kick: -35),
                Upgrade("Extended Mag", "More rounds before a reload.", UpgradeSlot.Magazine, WeaponFamily.All & ~WeaponFamily.Launcher, 300, magazine: 50),
                Upgrade("Drum Mag", "A lot more rounds, a little more weight.", UpgradeSlot.Magazine, WeaponFamily.SMG | WeaponFamily.Rifle | WeaponFamily.Shotgun, 450, magazine: 100, fireRate: -5),
                Upgrade("Laser Sight", "A dot where the shot will land.", UpgradeSlot.Underbarrel, WeaponFamily.All & ~WeaponFamily.Launcher, 250, spread: -15),
                Upgrade("Foregrip", "A second hand on the gun.", UpgradeSlot.Underbarrel, WeaponFamily.SMG | WeaponFamily.Rifle | WeaponFamily.Shotgun, 250, kick: -25, spread: -5),
                Upgrade("Kevlar Vest", "Soft armour. Light, takes the edge off.", UpgradeSlot.Armour, WeaponFamily.None, 400, damageTaken: -20, speed: -3),
                Upgrade("Plate Carrier", "Hard plates front and back. Heavy.", UpgradeSlot.Armour, WeaponFamily.None, 750, damageTaken: -35, speed: -10),
                Upgrade("Reflex Implant", "Faster hands, steadier aim.", UpgradeSlot.Implant, WeaponFamily.None, 900, fireRate: 15, spread: -10),
                Upgrade("Stim Implant", "Keeps the body going when it should quit.", UpgradeSlot.Implant, WeaponFamily.None, 800, health: 25),
                Upgrade("Sprint Implant", "Boosted leg muscle.", UpgradeSlot.Implant, WeaponFamily.None, 700, speed: 15),
            };

            Asset<ShopCatalogue>(Root + "/Shop Catalogue.asset", so =>
            {
                var arr = so.FindProperty("upgrades");
                arr.arraySize = ups.Count;
                for (int i = 0; i < ups.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = ups[i];
                var forSale = new (string name, int price)[] { ("Pistol Hayes", 250), ("SMG Hayes", 550), ("Shotgun Hayes", 600), ("Rifle Hayes", 750) };
                var w = so.FindProperty("weapons");
                w.arraySize = 0;
                foreach (var f in forSale)
                {
                    var weapon = AssetDatabase.LoadAssetAtPath<Weapon>(WeaponRoot + f.name + ".asset");
                    if (weapon == null) continue;
                    w.arraySize++;
                    var e = w.GetArrayElementAtIndex(w.arraySize - 1);
                    e.FindPropertyRelative("weapon").objectReferenceValue = weapon;
                    e.FindPropertyRelative("price").intValue = f.price;
                }
            });
        }

        static UpgradeDefinition Upgrade(string name, string description, UpgradeSlot slot, WeaponFamily fits, int price,
            float damage = 0, float spread = 0, float kick = 0, float fireRate = 0, float magazine = 0, float damageTaken = 0, float health = 0, float speed = 0)
        {
            return Asset<UpgradeDefinition>(Root + "/Upgrades/" + name + ".asset", so =>
            {
                Common(so, name, description, null, price);
                so.FindProperty("slot").enumValueIndex = (int)slot;
                so.FindProperty("fits").intValue = (int)fits;
                var m = so.FindProperty("modifiers");
                m.FindPropertyRelative("damage").floatValue = damage;
                m.FindPropertyRelative("spread").floatValue = spread;
                m.FindPropertyRelative("kick").floatValue = kick;
                m.FindPropertyRelative("fireRate").floatValue = fireRate;
                m.FindPropertyRelative("magazine").floatValue = magazine;
                m.FindPropertyRelative("damageTaken").floatValue = damageTaken;
                m.FindPropertyRelative("health").floatValue = health;
                m.FindPropertyRelative("speed").floatValue = speed;
            });
        }

        static SkillDefinition Skill(string name, string description, params (Stat stat, float add, float pct)[] bonuses)
        {
            return Asset<SkillDefinition>(Root + "/Skills/" + name + ".asset", so =>
            {
                Common(so, name, description, null, -1);
                var arr = so.FindProperty("bonuses");
                arr.arraySize = bonuses.Length;
                for (int i = 0; i < bonuses.Length; i++)
                {
                    var e = arr.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("stat").enumValueIndex = (int)bonuses[i].stat;
                    e.FindPropertyRelative("additivePerLevel").floatValue = bonuses[i].add;
                    e.FindPropertyRelative("percentPerLevel").floatValue = bonuses[i].pct;
                }
            });
        }

        static void Common(SerializedObject so, string displayName, string description, Sprite icon, int price)
        {
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("icon").objectReferenceValue = icon;
            if (price >= 0) so.FindProperty("price").intValue = price;
        }

        static void Skills(SerializedObject so, params (SkillDefinition skill, int level)[] skills)
        {
            var arr = so.FindProperty("startingSkills");
            arr.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("skill").objectReferenceValue = skills[i].skill;
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("level").intValue = skills[i].level;
            }
        }

        static void Weapons(SerializedObject so, params string[] names) => FillWeapons(so.FindProperty("startingWeapons"), names);

        static void FillWeapons(SerializedProperty arr, string[] names)
        {
            var found = new List<Weapon>();
            foreach (var n in names)
            {
                var w = AssetDatabase.LoadAssetAtPath<Weapon>(WeaponRoot + n + ".asset");
                if (w != null) found.Add(w); else Debug.LogWarning("[Company] Weapon not found: " + WeaponRoot + n + ".asset");
            }
            arr.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }

        static void Template(SerializedProperty t, string label, CharacterDefinition body, int weight, SkillDefinition[] skills, params string[] weapons)
        {
            t.FindPropertyRelative("label").stringValue = label;
            t.FindPropertyRelative("body").objectReferenceValue = body;
            t.FindPropertyRelative("skillPoints").vector2IntValue = new Vector2Int(4, 8);
            t.FindPropertyRelative("feeMultiplier").vector2Value = new Vector2(0.8f, 1.2f);
            t.FindPropertyRelative("weight").intValue = weight;
            var s = t.FindPropertyRelative("skills");
            s.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++) s.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
            FillWeapons(t.FindPropertyRelative("weapons"), weapons);
        }

        static T Asset<T>(string path, System.Action<SerializedObject> fill) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
