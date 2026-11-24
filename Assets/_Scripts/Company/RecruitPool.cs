using System;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;

namespace Rambler.Crew
{
    //Who turns up for hire: named characters, plus recruits generated from templates (a body, a spread of
    //skill points, a fee range and a weapon) so the list never runs dry.
    [CreateAssetMenu(fileName = "Recruit Pool", menuName = "Rambler/Crew/Recruit Pool")]
    public class RecruitPool : ScriptableObject
    {
        [Serializable]
        public class Template
        {
            public string label = "Soldier";
            [Tooltip("A non-unique Character to build the recruit on (prefab, portrait, base wage).")]
            public CharacterDefinition body;
            [Tooltip("Skill points shared out at random among the skills below.")]
            public Vector2Int skillPoints = new Vector2Int(4, 8);
            public SkillDefinition[] skills = new SkillDefinition[0];
            [Tooltip("One of these is equipped.")]
            public Weapon[] weapons = new Weapon[0];
            [Tooltip("Hire fee = the body's price x this, plus feePerSkillPoint per point.")]
            public Vector2 feeMultiplier = new Vector2(0.8f, 1.2f);
            [Min(1)] public int weight = 1;
        }

        [SerializeField] CharacterDefinition[] namedRecruits = new CharacterDefinition[0];
        [Tooltip("Chance each slot on the list goes to a named character still available.")]
        [Range(0f, 1f)] [SerializeField] float namedChance = 0.35f;
        [SerializeField] Template[] templates = new Template[0];
        [Min(0)] [SerializeField] int feePerSkillPoint = 25;
        [SerializeField] string[] firstNames = { "Ash", "Brook", "Cass", "Dex", "Ellis", "Flint", "Gray", "Harper", "Ira", "Jules", "Kai", "Lane", "Morgan", "Nico", "Oakley", "Parker", "Quinn", "Reese", "Sage", "Tate" };
        [SerializeField] string[] lastNames = { "Vance", "Okafor", "Reyes", "Kowalski", "Tanaka", "Byrne", "Haddad", "Novak", "Mbeki", "Larsen", "Castillo", "Ivanova", "Doyle", "Singh", "Moreau", "Kerr" };
        [Min(1)] [SerializeField] int listSize = 5;

        public int ListSize => listSize;

        //A fresh list of candidates. namedAvailable says whether a named character can still be offered
        //(not hired, dead or holding a grudge).
        public List<CharacterRecord> MakeCandidates(int count, Func<CharacterDefinition, bool> namedAvailable)
        {
            var list = new List<CharacterRecord>();
            var named = new List<CharacterDefinition>();
            foreach (var d in namedRecruits) if (d != null && namedAvailable(d)) named.Add(d);

            for (int i = 0; i < count; i++)
            {
                if (named.Count > 0 && (UnityEngine.Random.value < namedChance || templates.Length == 0))
                {
                    int n = UnityEngine.Random.Range(0, named.Count);
                    list.Add(CharacterRecord.FromDefinition(named[n]));
                    named.RemoveAt(n);
                    continue;
                }
                var generated = Generate();
                if (generated != null) list.Add(generated);
            }
            return list;
        }

        public CharacterRecord Generate()
        {
            var t = PickTemplate();
            if (t == null || t.body == null) return null;

            var r = CharacterRecord.FromDefinition(t.body);
            r.generated = true;
            r.displayName = Pick(firstNames, "Recruit") + " " + Pick(lastNames, "");
            r.displayName = r.displayName.Trim();

            int points = UnityEngine.Random.Range(t.skillPoints.x, t.skillPoints.y + 1);
            for (int p = 0; p < points && t.skills.Length > 0; p++)
            {
                var s = t.skills[UnityEngine.Random.Range(0, t.skills.Length)];
                if (s != null) r.SetSkill(s, r.GetSkill(s) + 1);
            }

            if (t.weapons.Length > 0)
            {
                var w = t.weapons[UnityEngine.Random.Range(0, t.weapons.Length)];
                r.weaponIds.Clear();
                if (w != null) r.weaponIds.Add(w.GetItemID());
            }

            float mult = UnityEngine.Random.Range(t.feeMultiplier.x, t.feeMultiplier.y);
            r.hireFee = Mathf.RoundToInt((t.body.Price * mult + points * feePerSkillPoint) / 5f) * 5;
            r.wage = Mathf.RoundToInt(t.body.WagePerMission * mult);
            return r;
        }

        Template PickTemplate()
        {
            int total = 0;
            foreach (var t in templates) if (t != null) total += t.weight;
            if (total <= 0) return null;
            int roll = UnityEngine.Random.Range(0, total);
            foreach (var t in templates)
            {
                if (t == null) continue;
                if (roll < t.weight) return t;
                roll -= t.weight;
            }
            return null;
        }

        static string Pick(string[] names, string fallback) =>
            names != null && names.Length > 0 ? names[UnityEngine.Random.Range(0, names.Length)] : fallback;
    }
}
