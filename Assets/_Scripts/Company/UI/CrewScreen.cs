using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Ownables;

namespace Rambler.Crew.UI
{
    //The Crew window, opened at a CrewTerminal. Two tabs:
    //  Recruit  cards for everyone for hire, with a Hire button each
    //  Crew     the crew list; the selected person's Skills, Loadout (with the Company armoury) and Training,
    //           plus Deploy (when the scene has a SquadDeployer) and Fire
    //The game pauses while it is open. Built by Tools > Company > Build Crew Screen.
    public class CrewScreen : MonoBehaviour
    {
        enum Tab { Recruit, Crew }
        enum Detail { Skills, Loadout, Training }

        public static CrewScreen Instance { get; private set; }

        [SerializeField] GameObject window;
        [SerializeField] Button closeButton;
        [SerializeField] Button recruitTab;
        [SerializeField] Button crewTab;
        [SerializeField] TextMeshProUGUI creditsText;
        [SerializeField] TextMeshProUGUI footerText;
        [SerializeField] TextMeshProUGUI messageText;

        [Header("Recruit")]
        [SerializeField] GameObject recruitPage;
        [SerializeField] CrewUIItem candidateTemplate;

        [Header("Crew")]
        [SerializeField] GameObject crewPage;
        [SerializeField] CrewUIItem crewRowTemplate;
        [SerializeField] TextMeshProUGUI emptyCrewText;
        [SerializeField] GameObject detailRoot;
        [SerializeField] CrewUIItem detailHeader;
        [SerializeField] Button skillsTab;
        [SerializeField] Button loadoutTab;
        [SerializeField] Button trainingTab;
        [SerializeField] GameObject skillsPage;
        [SerializeField] GameObject loadoutPage;
        [SerializeField] GameObject trainingPage;
        [SerializeField] CrewUIItem skillTemplate;
        [SerializeField] CrewUIItem carriedTemplate;
        [SerializeField] CrewUIItem armouryTemplate;
        [SerializeField] TextMeshProUGUI armouryEmptyText;
        [SerializeField] CrewUIItem courseTemplate;
        [SerializeField] TextMeshProUGUI trainingStatusText;
        [SerializeField] Button deployButton;
        [SerializeField] TextMeshProUGUI deployLabel;
        [SerializeField] Button fireButton;
        [SerializeField] TextMeshProUGUI fireLabel;

        [Header("Look")]
        [SerializeField] Color tabOn = new Color(0.95f, 0.62f, 0.2f);
        [SerializeField] Color tabOff = new Color(0.16f, 0.24f, 0.32f);
        [SerializeField] Color dimText = new Color(1f, 1f, 1f, 0.55f);

        readonly Dictionary<CrewUIItem, List<CrewUIItem>> spawned = new Dictionary<CrewUIItem, List<CrewUIItem>>();
        Tab tab = Tab.Recruit;
        Detail detail = Detail.Skills;
        string selectedId;
        string confirmFireId;
        float timeScaleBefore = 1f;
        bool subscribed;

        public bool IsOpen => window != null && window.activeSelf;

        void Awake()
        {
            Instance = this;
            if (window != null) window.SetActive(false);
            foreach (var t in new[] { candidateTemplate, crewRowTemplate, skillTemplate, carriedTemplate, armouryTemplate, courseTemplate })
                if (t != null) t.gameObject.SetActive(false);
            Wire(closeButton, Close);
            Wire(recruitTab, () => ShowTab(Tab.Recruit));
            Wire(crewTab, () => ShowTab(Tab.Crew));
            Wire(skillsTab, () => ShowDetail(Detail.Skills));
            Wire(loadoutTab, () => ShowDetail(Detail.Loadout));
            Wire(trainingTab, () => ShowDetail(Detail.Training));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
            if (IsOpen) Time.timeScale = timeScaleBefore;
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        // ---------- Open / close ----------

        public void Open() => Open(Company.Instance != null && Company.Instance.Crew.Count > 0 && Company.Instance.Candidates.Count == 0);

        public void Open(bool crewFirst)
        {
            if (Company.Instance == null || window == null) return;
            if (!IsOpen)
            {
                timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            window.SetActive(true);
            Subscribe();
            Say("");
            ShowTab(crewFirst ? Tab.Crew : Tab.Recruit);
        }

        public void Close()
        {
            if (!IsOpen) return;
            window.SetActive(false);
            Unsubscribe();
            Time.timeScale = timeScaleBefore;
        }

        void Subscribe()
        {
            if (subscribed || Company.Instance == null) return;
            Company.Instance.Changed += Refresh;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            if (Company.Instance != null) Company.Instance.Changed -= Refresh;
            subscribed = false;
        }

        void ShowTab(Tab t)
        {
            tab = t;
            confirmFireId = null;
            Say("");
            Refresh();
        }

        void ShowDetail(Detail d)
        {
            detail = d;
            Refresh();
        }

        void Say(string message)
        {
            if (messageText != null) messageText.text = message;
        }

        // ---------- Drawing ----------

        void Refresh()
        {
            var company = Company.Instance;
            if (company == null || !IsOpen) return;

            CrewUIItem.SetText(creditsText, "Credits  " + company.Credits.ToString("N0"));
            CrewUIItem.SetText(footerText, "Crew " + company.Crew.Count + "/" + company.MaxCrew);
            Tint(recruitTab, tab == Tab.Recruit);
            Tint(crewTab, tab == Tab.Crew);
            if (recruitPage != null) recruitPage.SetActive(tab == Tab.Recruit);
            if (crewPage != null) crewPage.SetActive(tab == Tab.Crew);

            if (tab == Tab.Recruit) DrawRecruits(company);
            else DrawCrew(company);
        }

        void Tint(Button b, bool on)
        {
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = on ? tabOn : tabOff;
        }

        void DrawRecruits(Company company)
        {
            var items = Fill(candidateTemplate, company.Candidates.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var r = company.Candidates[i];
                var item = items[i];
                var def = r.Definition;
                item.SetPortrait(def != null ? def.Portrait : null, r.displayName);
                CrewUIItem.SetText(item.title, r.displayName);
                CrewUIItem.SetText(item.side, r.generated ? "" : "NAMED");
                CrewUIItem.SetText(item.body, SkillLines(r, "\n") + "\n\n" + WeaponName(r.weaponIds.Count > 0 ? r.weaponIds[0] : null) + "\nWage " + r.wage + " / mission");
                bool ok = company.CanHire(r, out string reason);
                var record = r;
                CrewUIItem.SetButton(item.action, item.actionLabel, ok ? "Hire  " + r.hireFee : reason, true, ok, () =>
                {
                    if (company.Hire(record)) { Say("Hired " + record.displayName + "."); selectedId = record.recordId; }
                });
            }
            if (company.Candidates.Count == 0) Say("Nobody for hire right now.");
        }

        void DrawCrew(Company company)
        {
            var crew = company.Crew;
            if (GetSelected(company) == null) selectedId = crew.Count > 0 ? crew[0].recordId : null;
            if (emptyCrewText != null) emptyCrewText.gameObject.SetActive(crew.Count == 0);

            var rows = Fill(crewRowTemplate, crew.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = crew[i];
                var row = rows[i];
                row.SetPortrait(r.Definition != null ? r.Definition.Portrait : null, r.displayName);
                CrewUIItem.SetText(row.title, r.displayName);
                CrewUIItem.SetText(row.side, StatusText(r));
                if (row.highlight != null) row.highlight.enabled = r.recordId == selectedId;
                var id = r.recordId;
                CrewUIItem.SetButton(row.select, null, null, true, true, () => { selectedId = id; confirmFireId = null; Refresh(); });
            }

            var sel = GetSelected(company);
            if (detailRoot != null) detailRoot.SetActive(sel != null);
            if (sel == null) return;

            detailHeader.SetPortrait(sel.Definition != null ? sel.Definition.Portrait : null, sel.displayName);
            CrewUIItem.SetText(detailHeader.title, sel.displayName);
            CrewUIItem.SetText(detailHeader.body, StatusText(sel) + "   ·   XP " + Mathf.RoundToInt(sel.experience) + "   ·   Missions " + sel.missionsServed + "   ·   Wage " + sel.wage);

            Tint(skillsTab, detail == Detail.Skills);
            Tint(loadoutTab, detail == Detail.Loadout);
            Tint(trainingTab, detail == Detail.Training);
            if (skillsPage != null) skillsPage.SetActive(detail == Detail.Skills);
            if (loadoutPage != null) loadoutPage.SetActive(detail == Detail.Loadout);
            if (trainingPage != null) trainingPage.SetActive(detail == Detail.Training);
            switch (detail)
            {
                case Detail.Skills: DrawSkills(sel); break;
                case Detail.Loadout: DrawLoadout(company, sel); break;
                case Detail.Training: DrawTraining(company, sel); break;
            }

            DrawDeployAndFire(company, sel);
        }

        void DrawSkills(CharacterRecord r)
        {
            var skills = new List<SkillDefinition>(CatalogAsset.All<SkillDefinition>());
            skills.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));
            var items = Fill(skillTemplate, skills.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var s = skills[i];
                int level = r.GetSkill(s);
                var item = items[i];
                CrewUIItem.SetText(item.title, s.DisplayName);
                CrewUIItem.SetText(item.side, level + " / " + s.MaxLevel);
                CrewUIItem.SetText(item.body, BonusText(s, level));
                if (item.bar != null) item.bar.fillAmount = s.MaxLevel > 0 ? (float)level / s.MaxLevel : 0f;
                if (item.title != null) item.title.color = level > 0 ? Color.white : dimText;
            }
        }

        void DrawLoadout(Company company, CharacterRecord r)
        {
            bool canEdit = company.CanEditLoadout(r, out string why);
            var carried = Fill(carriedTemplate, r.weaponIds.Count);
            for (int i = 0; i < carried.Count; i++)
            {
                string id = r.weaponIds[i];
                var item = carried[i];
                var w = Weapon.GetFromID(id);
                item.SetPortrait(w != null ? w.GetIcon() : null, WeaponName(id));
                CrewUIItem.SetText(item.title, WeaponName(id));
                CrewUIItem.SetText(item.side, i == 0 ? "EQUIPPED" : "");
                CrewUIItem.SetButton(item.action, item.actionLabel, "Equip", i > 0, canEdit, () => company.EquipWeapon(r, id));
                CrewUIItem.SetButton(item.action2, item.action2Label, "Store", true, canEdit, () => company.StoreWeapon(r, id));
            }

            //The armoury, one row per kind of weapon with a count
            var kinds = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var id in company.Armoury)
            {
                if (!counts.ContainsKey(id)) { counts[id] = 0; kinds.Add(id); }
                counts[id]++;
            }
            if (armouryEmptyText != null) armouryEmptyText.gameObject.SetActive(kinds.Count == 0);
            var stock = Fill(armouryTemplate, kinds.Count);
            for (int i = 0; i < stock.Count; i++)
            {
                string id = kinds[i];
                var item = stock[i];
                var w = Weapon.GetFromID(id);
                item.SetPortrait(w != null ? w.GetIcon() : null, WeaponName(id));
                CrewUIItem.SetText(item.title, WeaponName(id));
                CrewUIItem.SetText(item.side, "x" + counts[id]);
                bool ok = company.CanTakeWeapon(r, id, out string reason);
                CrewUIItem.SetButton(item.action, item.actionLabel, "Take", true, ok, () => company.TakeWeapon(r, id));
                if (!ok && canEdit) CrewUIItem.SetText(item.body, reason);
                else CrewUIItem.SetText(item.body, "");
            }
            if (!canEdit) Say(why);
        }

        void DrawTraining(Company company, CharacterRecord r)
        {
            var course = r.Training;
            CrewUIItem.SetText(trainingStatusText, r.status == CrewStatus.Training && course != null
                ? "On the " + course.DisplayName + ": " + r.missionsOut + " mission" + (r.missionsOut == 1 ? "" : "s") + " to go."
                : "Training takes the trainee out of the next mission" + (company.Crew.Count > 0 ? "s" : "") + " it lasts.");

            var courses = new List<TrainingCourse>(CatalogAsset.All<TrainingCourse>());
            courses.Sort((a, b) => a.Price.CompareTo(b.Price));
            var items = Fill(courseTemplate, courses.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var c = courses[i];
                var item = items[i];
                bool ok = company.CanTrain(r, c, out string reason);
                CrewUIItem.SetText(item.title, c.DisplayName);
                CrewUIItem.SetText(item.body, (c.Skill != null ? c.Skill.DisplayName : "?") + " +" + c.LevelsGained + "   ·   " + c.Missions + " mission" + (c.Missions == 1 ? "" : "s") + (ok ? "" : "   ·   " + reason));
                CrewUIItem.SetText(item.side, c.Price.ToString("N0"));
                var course2 = c;
                CrewUIItem.SetButton(item.action, item.actionLabel, "Train", true, ok, () =>
                {
                    if (company.StartTraining(r, course2)) Say(r.displayName + " starts the " + course2.DisplayName + ".");
                });
            }
        }

        void DrawDeployAndFire(Company company, CharacterRecord r)
        {
            var deployer = FindObjectOfType<SquadDeployer>();
            var selector = FindObjectOfType<PlayerSelector>();
            bool squadFull = selector != null && selector.IsFull;
            bool canDeploy = deployer != null && r.CanDeploy && !squadFull;
            string label = r.status == CrewStatus.OnMission ? "Deployed" : squadFull && r.CanDeploy ? "Squad full" : "Deploy";
            CrewUIItem.SetButton(deployButton, deployLabel, label, deployer != null, canDeploy, () =>
            {
                if (deployer.Deploy(r) != null) Say(r.displayName + " joins the squad.");
            });

            bool canFire = r.status != CrewStatus.OnMission;
            bool confirming = confirmFireId == r.recordId;
            CrewUIItem.SetButton(fireButton, fireLabel, confirming ? "Confirm fire" : "Fire", true, canFire, () =>
            {
                if (confirmFireId != r.recordId) { confirmFireId = r.recordId; Refresh(); return; }
                confirmFireId = null;
                company.Fire(r);
                Say(r.status == CrewStatus.Hostile ? r.displayName + " storms out. Watch your back." : r.displayName + " packs up and leaves.");
            });
        }

        // ---------- Helpers ----------

        CharacterRecord GetSelected(Company company)
        {
            foreach (var r in company.Crew) if (r.recordId == selectedId) return r;
            return null;
        }

        //Copies of the template, one per entry; extra copies are hidden, not destroyed
        List<CrewUIItem> Fill(CrewUIItem template, int count)
        {
            if (template == null) return new List<CrewUIItem>();
            if (!spawned.TryGetValue(template, out var list)) spawned[template] = list = new List<CrewUIItem>();
            while (list.Count < count)
            {
                var copy = Instantiate(template, template.transform.parent);
                copy.name = template.name.Replace("Template", "") + " " + list.Count;
                list.Add(copy);
            }
            var shown = new List<CrewUIItem>(count);
            for (int i = 0; i < list.Count; i++)
            {
                list[i].gameObject.SetActive(i < count);
                if (i < count) shown.Add(list[i]);
            }
            return shown;
        }

        static string StatusText(CharacterRecord r)
        {
            switch (r.status)
            {
                case CrewStatus.Hired: return "Ready";
                case CrewStatus.OnMission: return "On mission";
                case CrewStatus.Training: return "Training (" + r.missionsOut + ")";
                case CrewStatus.Injured: return "Injured (" + r.missionsOut + ")";
                default: return r.status.ToString();
            }
        }

        static string SkillLines(CharacterRecord r, string separator)
        {
            var parts = new List<string>();
            var sorted = new List<SkillRecord>(r.skills);
            sorted.Sort((a, b) => b.level.CompareTo(a.level));
            foreach (var s in sorted)
            {
                var def = CatalogAsset.GetFromID<SkillDefinition>(s.skillId);
                if (def != null && s.level > 0) parts.Add(def.DisplayName + "  " + s.level);
            }
            return parts.Count > 0 ? string.Join(separator, parts) : "No skills";
        }

        static string BonusText(SkillDefinition s, int level)
        {
            if (level <= 0) return "";
            var parts = new List<string>();
            foreach (var b in s.Bonuses)
            {
                if (b.percentPerLevel != 0f) parts.Add("+" + (b.percentPerLevel * level).ToString("0.#") + "% " + b.stat);
                if (b.additivePerLevel != 0f) parts.Add("+" + (b.additivePerLevel * level).ToString("0.#") + " " + b.stat);
            }
            return string.Join("  ", parts);
        }

        static string WeaponName(string id)
        {
            var w = Weapon.GetFromID(id);
            if (w == null) return "Unarmed";
            string n = !string.IsNullOrEmpty(w.GetDisplayName()) ? w.GetDisplayName() : w.name;
            return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : n;
        }
    }
}
