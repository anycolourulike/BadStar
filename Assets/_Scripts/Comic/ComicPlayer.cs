using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Rambler.Comics
{
    //Plays a ComicSequence full screen: black backdrop, the still (fitted, with a slow zoom), an optional comic
    //caption box, tap anywhere for the next page, Back and Skip buttons. Pauses the game while it plays.
    //
    //Nothing to place in a scene: ComicPlayer.Play(sequence) creates the player the first time it is needed and
    //keeps it across scene loads (so a sequence can end by loading a level). Its canvas sits above the HUD and
    //below the Fader (sort order 999).
    public class ComicPlayer : MonoBehaviour
    {
        const int SortOrder = 900;
        const float TapCooldown = 0.25f; //Ignore taps this soon after a page appears, so one tap can't skip two

        static ComicPlayer instance;

        Canvas canvas;
        CanvasGroup rootGroup;
        Image backdrop;
        RectTransform imageRect;
        Image image;
        CanvasGroup pageGroup;
        RectTransform captionBox;
        TextMeshProUGUI captionText;
        GameObject backButton;
        GameObject skipButton;
        AudioSource audioSource;

        ComicSequence sequence;
        Action onFinished;
        bool requestNext, requestBack, requestSkip;
        bool hideOnNextSceneLoad;

        public static bool IsPlaying => instance != null && instance.sequence != null;

        //Plays the sequence. onFinished runs when it ends or is skipped (not if it loads another level).
        public static void Play(ComicSequence sequence, Action onFinished = null)
        {
            if (sequence == null || sequence.pages.Count == 0)
            {
                Debug.LogWarning("ComicPlayer: nothing to play");
                onFinished?.Invoke();
                return;
            }
            if (IsPlaying)
            {
                Debug.LogWarning("ComicPlayer: " + sequence.name + " asked to play while " + instance.sequence.name + " is playing; ignored");
                return;
            }
            if (instance == null) Create();
            instance.StartCoroutine(instance.Run(sequence, onFinished));
        }

        static void Create()
        {
            var go = new GameObject("ComicPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ComicPlayer>();
            instance.Build();
            SceneManager.sceneLoaded += instance.OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        //A sequence that ends by loading a level stays black until the next scene (the Loading screen) is in
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || !hideOnNextSceneLoad) return;
            hideOnNextSceneLoad = false;
            canvas.gameObject.SetActive(false);
        }

        IEnumerator Run(ComicSequence seq, Action finished)
        {
            sequence = seq;
            onFinished = finished;
            EnsureEventSystem();

            float previousTimeScale = Time.timeScale;
            if (seq.pauseGame) Time.timeScale = 0f;

            canvas.gameObject.SetActive(true);
            backButton.SetActive(false);
            skipButton.SetActive(seq.allowSkip);
            pageGroup.alpha = 0f;
            yield return FadeGroup(rootGroup, 0f, 1f, seq.fadeTime);

            int index = 0;
            while (index >= 0 && index < seq.pages.Count)
            {
                var page = seq.pages[index];
                ShowPage(page);
                backButton.SetActive(seq.allowBack && index > 0);
                yield return FadeGroup(pageGroup, 0f, 1f, seq.fadeTime);

                ClearRequests();
                float shownAt = Time.unscaledTime;
                float zoomTime = page.autoAdvance > 0f ? page.autoAdvance : seq.zoomTime;
                int step = 0;
                while (step == 0)
                {
                    float t = Time.unscaledTime - shownAt;
                    imageRect.localScale = Vector3.one * Mathf.Lerp(1f, seq.zoom, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / zoomTime)));

                    if (seq.allowSkip && Input.GetKeyDown(KeyCode.Escape)) requestSkip = true; //Android back button
                    bool ready = t >= TapCooldown;

                    if (requestSkip) step = 2;
                    else if (ready && requestBack && index > 0) step = -1;
                    else if (ready && requestNext) step = 1;
                    else if (page.autoAdvance > 0f && t >= page.autoAdvance) step = 1;
                    else { requestNext = requestBack = false; yield return null; }
                }

                yield return FadeGroup(pageGroup, 1f, 0f, seq.fadeTime);
                if (step == 2) break;
                index += step;
            }

            if (audioSource.isPlaying) audioSource.Stop();
            Time.timeScale = previousTimeScale;
            Action callback = onFinished;
            sequence = null;
            onFinished = null;

            if (seq.sceneRefAfter >= 0 && LevelManager.Instance != null)
            {
                //Stay black and hand over to the level change; hidden when the Loading scene arrives
                hideOnNextSceneLoad = true;
                LevelManager.Instance.sceneRef = seq.sceneRefAfter;
                LevelManager.Instance.StartCoroutine("LoadLoading");
                yield break;
            }

            yield return FadeGroup(rootGroup, 1f, 0f, seq.fadeTime);
            canvas.gameObject.SetActive(false);
            callback?.Invoke();
        }

        void ShowPage(ComicSequence.Page page)
        {
            image.sprite = page.image;
            image.enabled = page.image != null;
            imageRect.localScale = Vector3.one;

            bool hasCaption = !string.IsNullOrWhiteSpace(page.caption);
            captionBox.gameObject.SetActive(hasCaption);
            if (hasCaption)
            {
                captionText.text = page.caption;
                bool top = page.captionPosition == ComicSequence.CaptionPosition.Top;
                captionBox.anchorMin = captionBox.anchorMax = new Vector2(0.5f, top ? 1f : 0f);
                captionBox.pivot = new Vector2(0.5f, top ? 1f : 0f);
                captionBox.anchoredPosition = new Vector2(0f, top ? -48f : 48f);
            }

            if (page.sound != null) audioSource.PlayOneShot(page.sound);
        }

        void ClearRequests()
        {
            requestNext = requestBack = requestSkip = false;
        }

        static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float time)
        {
            if (time <= 0f) { group.alpha = to; yield break; }
            float t = 0f;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / time));
                yield return null;
            }
            group.alpha = to;
        }

        //The UI needs an EventSystem for taps; levels have one, but add one if this scene doesn't
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        // ---------- UI built in code ----------

        void Build()
        {
            var canvasGO = new GameObject("ComicCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); //Same as the HUD
            scaler.matchWidthOrHeight = 0.5f;
            rootGroup = canvasGO.GetComponent<CanvasGroup>();

            //Black backdrop; also the tap-anywhere "next" button
            backdrop = MakeImage("Backdrop", canvasGO.transform, Color.black);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            var tap = backdrop.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => requestNext = true);

            //The page: still + caption, faded together
            var pageGO = new GameObject("Page", typeof(RectTransform), typeof(CanvasGroup));
            pageGO.transform.SetParent(canvasGO.transform, false);
            Stretch((RectTransform)pageGO.transform);
            pageGroup = pageGO.GetComponent<CanvasGroup>();
            pageGroup.blocksRaycasts = false; //Taps fall through to the backdrop

            image = MakeImage("Still", pageGO.transform, Color.white);
            image.preserveAspect = true;
            imageRect = image.rectTransform;
            Stretch(imageRect);
            imageRect.offsetMin = new Vector2(24f, 24f);
            imageRect.offsetMax = new Vector2(-24f, -24f);

            BuildCaption(pageGO.transform);

            backButton = MakeButton("Back", "‹ BACK", new Vector2(0f, 0f), new Vector2(40f, 40f), () => requestBack = true);
            skipButton = MakeButton("Skip", "SKIP ›", new Vector2(1f, 1f), new Vector2(-40f, -40f), () => requestSkip = true);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true; //Plays while the game is paused

            canvasGO.SetActive(false);
        }

        //Classic comic narration box: pale yellow, black border, black capitals
        void BuildCaption(Transform parent)
        {
            var box = MakeImage("Caption", parent, new Color(1f, 0.93f, 0.62f));
            captionBox = box.rectTransform;
            captionBox.sizeDelta = new Vector2(1280f, 0f);
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -4f);
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 22, 24);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(captionBox, false);
            captionText = textGO.GetComponent<TextMeshProUGUI>();
            captionText.fontSize = 40f;
            captionText.color = new Color(0.08f, 0.08f, 0.08f);
            captionText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            captionText.alignment = TextAlignmentOptions.Center;
            captionText.enableWordWrapping = true;
            captionText.raycastTarget = false;
        }

        GameObject MakeButton(string name, string label, Vector2 corner, Vector2 offset, Action onClick)
        {
            var bg = MakeImage(name, canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            var rect = bg.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(220f, 84f); //Big enough for a thumb
            bg.raycastTarget = true;
            var button = bg.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var textGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(rect, false);
            Stretch((RectTransform)textGO.transform);
            var text = textGO.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 34f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 4f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return bg.gameObject;
        }

        static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
