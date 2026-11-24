using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rambler.Combat;
using Rambler.Control;
using Rambler.Ownables;

namespace Rambler.Crew.UI
{
    //The Home Terminal window (this class keeps its old name), opened at a CrewTerminal. Three tabs:
    //  Recruit  cards for everyone for hire, with a Hire button each
    //  Crew     the crew list; the selected person's Skills, Loadout (guns with attachment slots, gear slots,
    //           the stash of unfitted upgrades and the armoury) and Training, plus Deploy and Fire
    //  Shop     attachments, gear and guns for credits (bought items go to the stash or the armoury)
    //The game pauses while it is open. Built by Tools > Company > Build Home Terminal.
    public class CrewScreen : MonoBehaviour
    {
        enum Tab { Recruit, Crew, Shop }
        enum Detail { Skills, Loadout, Training }

        public static CrewScreen Instance { get; private set; }

        [SerializeField] GameObject window;
        [SerializeField] Button closeButton;
        [SerializeField] Button recruitTab;
        [SerializeField] Button crewTab;
        [SerializeField] Button shopTab;
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
        [SerializeField] CrewUIItem gearTemplate;
        [SerializeField] CrewUIItem stashTemplate;
        [SerializeField] TextMeshProUGUI stashEmptyText;
        [SerializeField] CrewUIItem armouryTemplate;
        [SerializeField] TextMeshProUGUI armouryEmptyText;
        [SerializeField] CrewUIItem courseTemplate;
        [SerializeField] TextMeshProUGUI trainingStatusText;
        [SerializeField] Button deployButton;
        [SerializeField] TextMeshProUGUI deployLabel;
        [SerializeField] Button fireButton;
        [SerializeField] TextMeshProUGUI fireLabel;

        [Header("Shop")]
        [SerializeField] GameObject shopPage;
        [SerializeField] CrewUIItem shopTemplate;

        [Header("Look")]
        [SerializeField] Color slotFilled = new Color(0.95f, 0.62f, 0.2f);
        [SerializeField] Color slotEmpty = new Color(0.12f, 0.17f, 0.23f);
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
            foreach (var t in new[] { candidateTemplate, crewRowTemplate, skillTemplate, carriedTemplate, gearTemplate, stashTemplate, armouryTemplate, courseTemplate, shopTemplate })
                if (t != null) t.gameObject.SetActive(false);
            Wire(closeButton, Close);
            Wire(recruitTab, () => ShowTab(Tab.Recruit));
            Wire(crewTab, () => ShowTab(Tab.Crew));
            Wire(shopTab, () => ShowTab(Tab.Shop));
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
            Tint(shopTab, tab == Tab.Shop);
            if (recruitPage != null) recruitPage.SetActive(tab == Tab.Recruit);
            if (crewPage != null) crewPage.SetActive(tab == Tab.Crew);
            if (shopPage != null) shopPage.SetActive(tab == Tab.Shop);

            if (tab == Tab.Recruit) DrawRecruits(company);
            else if (tab == Tab.Crew) DrawCrew(company);
            else DrawShop(company);
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
                CrewUIItem.SetText(item.body, SkillLines(r, "\n") + "\n\n" + WeaponName(r.Equipped != null ? r.Equipped.weaponId : null) + "\nWage " + r.wage + " / mission");
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

        //Left: the guns carried (with their attachment slots) and the gear slots. Right: the stash of unfitted
        //upgrades (Fit puts one on the equipped gun, or the first gun it fits, or in its gear slot) and the armoury.
        //Tapping a filled slot takes that upgrade off, back to the stash.
        void DrawLoadout(Company company, CharacterRecord r)
        {
            bool canEdit = company.CanEditLoadout(r, out string why);
            var guns = r.Weapons;
            var carried = Fill(carriedTemplate, guns.Count);
            for (int i = 0; i < carried.Count; i++)
            {
                var gun = guns[i];
                var item = carried[i];
                var w = gun.Weapon;
                item.SetPortrait(w != null ? w.GetIcon() : null, WeaponName(gun.weaponId));
                CrewUIItem.SetText(item.title, WeaponName(gun.weaponId));
                CrewUIItem.SetText(item.side, i == 0 ? "EQUIPPED" : "");
                var mods = gun.Modifiers;
                bool takesAttachments = UpgradeDefinition.FamilyOf(w) != WeaponFamily.None;
                CrewUIItem.SetText(item.body, !takesAttachments ? "Takes no attachments" : mods.IsEmpty ? "No attachments" : mods.Describe());
                for (int s = 0; s < item.slots.Length && s < UpgradeDefinition.WeaponSlots.Length; s++)
                {
                    var slot = UpgradeDefinition.WeaponSlots[s];
                    var fitted = gun.AttachmentIn(slot);
                    DrawSlot(item, s, slot, fitted, takesAttachments, canEdit, () => { if (company.RemoveAttachment(r, gun, slot)) Say(fitted.DisplayName + " back to the stash."); });
                }
                CrewUIItem.SetButton(item.action, item.actionLabel, "Equip", i > 0, canEdit, () => company.EquipWeapon(r, gun));
                CrewUIItem.SetButton(item.action2, item.action2Label, "Store", true, canEdit, () => company.StoreWeapon(r, gun));
            }

            //Gear (one row, a slot each for armour and implant)
            var gearRows = Fill(gearTemplate, 1);
            if (gearRows.Count > 0)
            {
                var g = gearRows[0];
                var gearMods = new ModifierSet();
                for (int s = 0; s < g.slots.Length && s < UpgradeDefinition.GearSlots.Length; s++)
                {
                    var slot = UpgradeDefinition.GearSlots[s];
                    var fitted = r.GearIn(slot);
                    if (fitted != null) gearMods += fitted.Modifiers;
                    DrawSlot(g, s, slot, fitted, true, canEdit, () => { if (company.RemoveGear(r, slot)) Say(fitted.DisplayName + " back to the stash."); });
                }
                CrewUIItem.SetText(g.body, gearMods.IsEmpty ? "No gear fitted" : gearMods.Describe());
            }

            //The stash: one row per kind of upgrade, with a count
            var kinds = new List<UpgradeDefinition>();
            foreach (var id in company.Stash)
            {
                var u = CatalogAsset.GetFromID<UpgradeDefinition>(id);
                if (u != null && !kinds.Contains(u)) kinds.Add(u);
            }
            kinds.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));
            if (stashEmptyText != null) stashEmptyText.gameObject.SetActive(kinds.Count == 0);
            var stashRows = Fill(stashTemplate, kinds.Count);
            for (int i = 0; i < stashRows.Count; i++)
            {
                var u = kinds[i];
                var item = stashRows[i];
                item.SetPortrait(u.Icon, u.DisplayName);
                CrewUIItem.SetText(item.title, u.DisplayName);
                CrewUIItem.SetText(item.side, "x" + company.StashCount(u));
                bool ok = company.CanFit(r, u, out var target, out string reason);
                CrewUIItem.SetText(item.body, SlotName(u.Slot) + (ok && target != null ? " → " + WeaponName(target.weaponId) : "") + (!ok && canEdit ? "  ·  " + reason : "") + "   " + u.Modifiers.Describe());
                CrewUIItem.SetButton(item.action, item.actionLabel, "Fit", true, ok, () =>
                {
                    if (company.Fit(r, u)) Say(u.DisplayName + " fitted" + (target != null ? " to the " + WeaponName(target.weaponId) : "") + ".");
                });
            }

            //The armoury: every gun separately, since each can carry different attachments
            if (armouryEmptyText != null) armouryEmptyText.gameObject.SetActive(company.Armoury.Count == 0);
            var stock = Fill(armouryTemplate, company.Armoury.Count);
            for (int i = 0; i < stock.Count; i++)
            {
                var gun = company.Armoury[i];
                var item = stock[i];
                var w = gun.Weapon;
                item.SetPortrait(w != null ? w.GetIcon() : null, WeaponName(gun.weaponId));
                CrewUIItem.SetText(item.title, WeaponName(gun.weaponId));
                CrewUIItem.SetText(item.side, gun.attachmentIds.Count > 0 ? gun.attachmentIds.Count + " fitted" : "");
                bool ok = company.CanTakeWeapon(r, gun, out string reason);
                CrewUIItem.SetText(item.body, !ok && canEdit ? reason : "");
                CrewUIItem.SetButton(item.action, item.actionLabel, "Take", true, ok, () => company.TakeWeapon(r, gun));
            }
            if (!canEdit) Say(why);
        }

        //One slot chip: the fitted upgrade's name (tap to take it off), or the empty slot's name
        void DrawSlot(CrewUIItem item, int index, UpgradeSlot slot, UpgradeDefinition fitted, bool show, bool canEdit, UnityEngine.Events.UnityAction remove)
        {
            var b = item.slots[index];
            var label = index < item.slotLabels.Length ? item.slotLabels[index] : null;
            CrewUIItem.SetButton(b, label, fitted != null ? fitted.DisplayName : SlotName(slot), show, canEdit && fitted != null, fitted != null ? remove : null);
            if (b != null && b.targetGraphic != null) b.targetGraphic.color = fitted != null ? slotFilled : slotEmpty;
            if (label != null) label.color = fitted != null ? Color.white : dimText;
        }

        void DrawShop(Company company)
        {
            var shop = company.Shop;
            var rows = new List<(string title, string body, int price, Sprite icon, System.Func<bool> buy)>();
            if (shop != null)
            {
                foreach (var forSale in shop.Weapons)
                {
                    if (forSale.weapon == null) continue;
                    var w = forSale.weapon; int price = forSale.price;
                    rows.Add((WeaponName(w.GetItemID()), "Gun   ·   goes to the armoury", price, w.GetIcon(), () => company.BuyWeapon(w, price)));
                }
                var ups = new List<UpgradeDefinition>(shop.Upgrades);
                ups.RemoveAll(u => u == null);
                ups.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : a.Price.CompareTo(b.Price));
                foreach (var u in ups)
                {
                    var up = u;
                    string fits = u.IsAttachment ? "   ·   fits " + FitsText(u.Fits) : "";
                    int owned = company.StashCount(u);
                    rows.Add((u.DisplayName, SlotName(u.Slot) + fits + "   ·   " + u.Modifiers.Describe() + (owned > 0 ? "   ·   " + owned + " in stash" : ""), u.Price, u.Icon, () => company.Buy(up)));
                }
            }
            var items = Fill(shopTemplate, rows.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var row = rows[i];
                var item = items[i];
                item.SetPortrait(row.icon, row.title);
                CrewUIItem.SetText(item.title, row.title);
                CrewUIItem.SetText(item.body, row.body);
                CrewUIItem.SetText(item.side, row.price.ToString("N0"));
                bool ok = company.CanAfford(row.price);
                var title = row.title; var buy = row.buy;
                CrewUIItem.SetButton(item.action, item.actionLabel, "Buy", true, ok, () => { if (buy()) Say("Bought " + title + "."); });
            }
            if (rows.Count == 0) Say("The shop has nothing for sale. Run Tools > Company > Create Starter Content.");
        }

        static string SlotName(UpgradeSlot s) => s == UpgradeSlot.Underbarrel ? "Under-barrel" : s.ToString();

        static string FitsText(WeaponFamily f)
        {
            if (f == WeaponFamily.All) return "all guns";
            var parts = new List<string>();
            foreach (WeaponFamily v in System.Enum.GetValues(typeof(WeaponFamily)))
                if (v != WeaponFamily.None && v != WeaponFamily.All && (f & v) != 0) parts.Add(v == WeaponFamily.SMG ? "SMGs" : v + "s");
            return string.Join(", ", parts).ToLowerInvariant().Replace("smgs", "SMGs");
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
