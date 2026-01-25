using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.SceneManagement
{
    public class Fader : MonoBehaviour
    {
        public Canvas canvas;
        public CanvasGroup canvasGroup;
        Coroutine currentActiveFade = null;

        void Start()
        {
            canvas.sortingOrder = 2;
        }

        public void FadeToBlackImmediate()
        {
            canvasGroup.alpha = 1;
        }        

        public Coroutine FadeToBlack(float time)
        {
            return Fade(1, time);
        }

        public Coroutine FadeToClear(float time)
        {
            return Fade(0, time);
        }

        public Coroutine Fade(float target, float time)
        {
            if (currentActiveFade != null)
            {
                StopCoroutine(currentActiveFade);
            }
            currentActiveFade = StartCoroutine(FadeRoutine(target, time));
            return currentActiveFade;
        }

        private IEnumerator FadeRoutine(float target, float time)
        {
            canvas.sortingOrder = 2;
            while (!Mathf.Approximately(canvasGroup.alpha, target))
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.deltaTime / time);
                yield return null;
            }
        }    
    }
}
