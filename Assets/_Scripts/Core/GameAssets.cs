using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rambler.Core
{
    public class GameAssets : MonoBehaviour
    {
        private static GameAssets _i;

        public static GameAssets i
        {
            get
            {
                if (_i == null) _i = Instantiate(Resources.Load<GameAssets>("GameAssets"));
                return _i;
            }
        }

        public WeaponAudioClip[] weaponAudioClipArray;
        public EnviromentAudioClip[] enviromentAudioClipArray;
        public HumanAudioClip[] humanAudioClipArray;
        public AmbientAudioClip[] ambientAudioClipArray;
        public RockerAudioClip[] rockerAudioClipArray;
        public TimbertoesAudioClip[] timbertoesAudioClipArray;

        [System.Serializable]
        public class WeaponAudioClip
        {
            public AudioManager.WeaponSound weaponSFX;
            public AudioClip weaponClip;
        }

        [System.Serializable]
        public class EnviromentAudioClip
        {
            public AudioManager.EnvironmentSound enviromentSFX;
            public AudioClip enviromentClip;
        }

        [System.Serializable]
        public class AmbientAudioClip
        {
            public AudioManager.AmbientSound ambientSFX;
            public AudioClip ambientClip;
        }

        [System.Serializable]
        public class RockerAudioClip
        {
            public AudioManager.RockerSound rockerSFX;
            public AudioClip rockerClip;
        }

        [System.Serializable]
        public class HumanAudioClip
        {
            public AudioManager.HumanSound humanSFX;
            public AudioClip humanClip;
        }

        [System.Serializable]
        public class TimbertoesAudioClip
        {
            public AudioManager.TimbertoesSound timbertoesSFX;
            public AudioClip timbertoesClip;
        }
    }
}