using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Rambler.Quests
{
    //Shows the current objective on screen: the first unfinished objective of the newest unfinished quest.
    //Pops when it changes and flashes "Objective complete" when one is done. Hidden when there is nothing to do.
    //
    //Nothing to set up: if no ObjectiveHUD is in a level, one is added under /PlayerCore/HUD on load and builds its
    //own box. To restyle, add this component to your own UI object and fill in the references; it then uses yours.
    public class ObjectiveHUD : MonoBehaviour
    {
        [Header("Optional: your own UI (left empty, a box is built at runtime)")]
        [SerializeField] RectTransform box;
        [SerializeField] TextMeshProUGUI headerText;
        [SerializeField] TextMeshProUGUI objectiveText;

        [Header("Built box (only used when the references above are empty)")]
        [SerializeField] Vector2 anchor = new Vector2(0.5f, 1f); //Top centre
        [SerializeField] Vector2 offset = new Vector2(0f, -36f);
        [SerializeField] float width = 640f;
        [SerializeField] Color panelColor = new Color(0.05f, 0.06f, 0.08f, 0.72f);
        [SerializeField] Color headerColor = new Color(1f, 0.78f, 0.25f);
        [SerializeField] Color textColor = Color.white;

        [Header("Feel")]
        [SerializeField] float popScale = 1.12f;
        [SerializeField] float popTime = 0.35f;
        [SerializeField] float completeFlashTime = 1.2f;

        const string HudPath = "/PlayerCore/HUD";

        QuestList questList;
        string shownObjective;
        Coroutine popRoutine;
        float nextFind;

        void Start()
        {
            if (box == null) BuildBox();
            box.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            Unhook();
        }

        void Update()
        {
            //QuestList lives on the player and can appear after us; look for it twice a second until found
            if (questList != null || Time.unscaledTime < nextFind) return;
            nextFind = Time.unscaledTime + 0.5f;
            questList = QuestList.Find();
            if (questList == null) return;
            questList.onUpdate += Refresh;
            questList.onQuestCompleted += OnQuestCompleted;
            Refresh();
        }

        void Unhook()
        {
            if (questList == null) return;
            questList.onUpdate -= Refresh;
            questList.onQuestCompleted -= OnQuestCompleted;
            questList = null;
        }

        void Refresh()
        {
            if (box == null || questList == null) return;
            var status = questList.GetCurrentStatus();
            var objective = status != null ? status.GetCurrentObjective() : null;

            if (objective == null)
            {
                shownObjective = null;
                //Leave the box up while a completion flash is still playing; it hides itself after
                if (popRoutine == null) box.gameObject.SetActive(false);
                return;
            }

            string text = string.IsNullOrWhiteSpace(objective.description) ? objective.reference : objective.description;
            string id = status.GetQuest().name + "/" + objective.reference;
            if (id == shownObjective && box.gameObject.activeSelf) return;

            bool isNew = id != shownObjective;
            shownObjective = id;
            if (headerText != null) headerText.text = status.GetQuest().GetTitle().ToUpperInvariant();
            objectiveText.text = text;
            box.gameObject.SetActive(true);
            if (isNew) Pop();
        }

        void OnQuestCompleted(Quest quest)
        {
            if (box == null) return;
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(CompleteFlash(quest));
        }

        IEnumerator CompleteFlash(Quest quest)
        {
            box.gameObject.SetActive(true);
            if (headerText != null) headerText.text = quest.GetTitle().ToUpperInvariant();
            objectiveText.text = "Objective complete";
            yield return PopRoutine();
            //Unscaled, so it still plays while dialogue has the game paused
            float until = Time.unscaledTime + completeFlashTime;
            while (Time.unscaledTime < until) yield return null;
            popRoutine = null;
            shownObjective = null;
            Refresh();
        }

        void Pop()
        {
            if (popRoutine != null) return; //A completion flash is showing; Refresh runs again when it ends
            StartCoroutine(PopRoutine());
        }

        IEnumerator PopRoutine()
        {
            float t = 0f;
            while (t < popTime)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / popTime);
                box.localScale = Vector3.one * Mathf.Lerp(1f, popScale, k * k);
                yield return null;
            }
            box.localScale = Vector3.one;
        }

        //A dark strip with a small coloured header (quest title) over the objective text
        void BuildBox()
        {
            var go = new GameObject("ObjectiveBox", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            box = go.GetComponent<RectTransform>();
            box.SetParent(transform, false);
            box.anchorMin = box.anchorMax = anchor;
            box.pivot = new Vector2(0.5f, 1f);
            box.anchoredPosition = offset;
            box.sizeDelta = new Vector2(width, 0f);

            go.GetComponent<Image>().color = panelColor;
            go.GetComponent<Image>().raycastTarget = false; //Never block touches on the HUD
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 14, 16);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            headerText = MakeText("Header", 24f, headerColor, FontStyles.Bold);
            headerText.characterSpacing = 6f;
            objectiveText = MakeText("Objective", 36f, textColor, FontStyles.Normal);
        }

        TextMeshProUGUI MakeText(string name, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(box, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        //Adds an ObjectiveHUD to each level's HUD unless the level already has one
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FindObjectOfType<ObjectiveHUD>(true) != null) return;
            var hud = GameObject.Find(HudPath);
            if (hud == null) return; //Menu, Loading, or an area scene: no player HUD
            var holder = new GameObject("ObjectiveHUD", typeof(RectTransform));
            var rect = holder.GetComponent<RectTransform>();
            rect.SetParent(hud.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            holder.AddComponent<ObjectiveHUD>();
        }
    }
}
