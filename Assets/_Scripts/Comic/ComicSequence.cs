using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Comics
{
    //A run of comic-book stills shown full screen by ComicPlayer, one page at a time.
    //Create with Assets > Create > BadStar > Comic Sequence, add pages, then play it from a ComicTrigger
    //(or from code with ComicPlayer.Play).
    [CreateAssetMenu(fileName = "Comic Sequence", menuName = "BadStar/Comic Sequence", order = 0)]
    public class ComicSequence : ScriptableObject
    {
        public enum CaptionPosition { Bottom, Top }

        [Serializable]
        public class Page
        {
            [Tooltip("The still. Shown as large as fits the screen without cropping.")]
            public Sprite image;
            [Tooltip("Optional caption box text (comic narration). Leave empty for none.")]
            [TextArea(2, 4)] public string caption;
            public CaptionPosition captionPosition = CaptionPosition.Bottom;
            [Tooltip("Seconds before moving on by itself. 0 = wait for a tap.")]
            [Min(0f)] public float autoAdvance = 0f;
            [Tooltip("Optional sound played when the page appears")]
            public AudioClip sound;
        }

        public List<Page> pages = new List<Page>();

        [Header("Controls")]
        [Tooltip("Show a Skip button (and let the Android back button skip)")]
        public bool allowSkip = true;
        [Tooltip("Show a Back button to return to the previous page")]
        public bool allowBack = true;

        [Header("Look")]
        [Tooltip("Fade time (s) between pages and in/out of the sequence")]
        [Min(0f)] public float fadeTime = 0.35f;
        [Tooltip("Slow zoom on each still: 1 = none, 1.06 = a gentle push in")]
        [Range(1f, 1.3f)] public float zoom = 1.06f;
        [Tooltip("Seconds the zoom takes (used when the page waits for a tap)")]
        [Min(0.5f)] public float zoomTime = 8f;

        [Header("Game")]
        [Tooltip("Pause the game while the sequence plays")]
        public bool pauseGame = true;
        [Tooltip("Load this level when the sequence ends (LevelManager sceneRef: 0 Menu, 2 Crash Site, 3 Mine). -1 = stay in this level.")]
        public int sceneRefAfter = -1;
    }
}
