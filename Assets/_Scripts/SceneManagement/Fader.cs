using System.Collections;
using UnityEngine;

namespace Rambler.SceneManagement
{
    public class Fader : MonoBehaviour
    {
        [Header("References")]
        public Canvas canvas;
        public CanvasGroup canvasGroup;
        
        private Coroutine currentActiveFade = null;
        
        private void Awake()
        {
            Debug.Assert(canvas != null, nameof(canvas));
            Debug.Assert(canvasGroup != null, nameof(canvasGroup));
            // Ensure the fader is always on top. 
            // Consider if 2 is high enough; usually faders use 999.
            canvas.sortingOrder = 999;
        }

        public void FadeToBlackImmediate()
        {
            CancelCurrentFade();
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = true;
        }

        public Coroutine FadeToBlack(float time) => Fade(1, time);
        public Coroutine FadeToClear(float time) => Fade(0, time);

        public Coroutine Fade(float target, float time)
        {
            CancelCurrentFade();
            currentActiveFade = StartCoroutine(FadeRoutine(target, time));
            return currentActiveFade;
        }

        private void CancelCurrentFade()
        {
            if (currentActiveFade != null)
            {
                StopCoroutine(currentActiveFade);
                currentActiveFade = null;
            }
        }

        private IEnumerator FadeRoutine(float target, float time)
        {
            // Block clicks during fade or if we are staying black
            canvasGroup.blocksRaycasts = true;

            // Handle instantaneous fade to avoid division by zero
            if (time <= 0)
            {
                canvasGroup.alpha = target;
            }
            else
            {
                while (!Mathf.Approximately(canvasGroup.alpha, target))
                {
                    canvasGroup.alpha =
                         Mathf.MoveTowards(canvasGroup.alpha, target, 
                             Time.unscaledDeltaTime / time);
                    yield return null;
                }
            }

            // Final snap to target
            canvasGroup.alpha = target;

            // If clear, stop blocking raycasts
            if (target <= 0)
            {
                canvasGroup.blocksRaycasts = false;
            }
            
            currentActiveFade = null;
        }
    }
}