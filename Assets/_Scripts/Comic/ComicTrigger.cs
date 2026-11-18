using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Rambler.Control;
using Rambler.Quests;
using Rambler.Saving;

namespace Rambler.Comics
{
    //Plays a ComicSequence at a story moment:
    //  On Level Start      - when the level has loaded and the save is restored (an intro)
    //  On Quest Completed  - when the chosen quest's last objective is done
    //  On Player Enter     - when the player walks into this object's trigger collider
    //  Manually            - call Play() from any UnityEvent (DialogueTrigger, ObjectiveTrigger, a button)
    //
    //"Once" stops it replaying. Add a SaveableEntity to this object as well and that is remembered in the save,
    //so an intro doesn't replay when the level is loaded again.
    public class ComicTrigger : MonoBehaviour, ISaveable
    {
        public enum When { Manually, OnLevelStart, OnQuestCompleted, OnPlayerEnter }

        [SerializeField] ComicSequence sequence;
        [SerializeField] When when = When.Manually;
        [Tooltip("On Quest Completed: the quest to wait for")]
        [SerializeField] Quest quest;
        [Tooltip("Only ever play once (remembered in the save if this object has a SaveableEntity)")]
        [SerializeField] bool once = true;
        [Tooltip("Runs after the sequence ends or is skipped, e.g. give the next quest or start a dialogue")]
        [SerializeField] UnityEvent onFinished;

        bool played;
        QuestList questList;

        IEnumerator Start()
        {
            if (when == When.OnLevelStart)
            {
                yield return WaitForLevelReady();
                Play();
            }
            else if (when == When.OnQuestCompleted)
            {
                //QuestList is on the player and may arrive after us
                while ((questList = QuestList.Find()) == null) yield return new WaitForSecondsRealtime(0.5f);
                questList.onQuestCompleted += OnQuestCompleted;
            }
        }

        void OnDestroy()
        {
            if (questList != null) questList.onQuestCompleted -= OnQuestCompleted;
        }

        //Wait until LevelManager has restored the save (so "played" is known) and placed the player.
        //Without a LevelManager (a level opened straight in the editor) play after a moment.
        static IEnumerator WaitForLevelReady()
        {
            float waited = 0f;
            while (LevelManager.Instance != null && !LevelManager.LevelReady && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
        }

        void OnQuestCompleted(Quest completed)
        {
            if (completed == quest) Play();
        }

        void OnTriggerEnter(Collider other)
        {
            if (when != When.OnPlayerEnter) return;
            if (other.GetComponentInParent<PlayerController>() == null && !other.CompareTag("Player")) return;
            Play();
        }

        public void Play()
        {
            if (once && played) return;
            if (sequence == null) return;
            played = true;
            if (ComicPlayer.IsPlaying) StartCoroutine(PlayWhenFree()); //Another sequence is up: queue behind it
            else ComicPlayer.Play(sequence, () => onFinished?.Invoke());
        }

        IEnumerator PlayWhenFree()
        {
            while (ComicPlayer.IsPlaying) yield return null;
            ComicPlayer.Play(sequence, () => onFinished?.Invoke());
        }

        object ISaveable.CaptureState()
        {
            return played;
        }

        void ISaveable.RestoreState(object state)
        {
            if (state is bool wasPlayed) played = wasPlayed;
        }
    }
}
