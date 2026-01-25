using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Rambler.Core 
{
  public static class AudioManager
  {
    public enum WeaponSound
    {
        pistolShoot, 
        SMGShoot, 
        RifleShoot, 
        ShotgunShoot, 
        outOfAmmo,
        shieldHit,
        humanImpact,
        robotImpact,
        weaponReload,
        ShieldUp,
        DeathScreen,
    }
    
    public enum EnvironmentSound
    {       
          
        ObjectiveRecevived,    
        ObjectiveComplete,
        RadioMessageStart,
        RadioMessageFinish,
        RadioTalk1,
        RadioTalk2,
        RadioTalk3,
        DoorOpen,
        DoorClosed,
        Explosion1,
        Explosion2,
        alarm,        
    }

    public enum HumanSound
    {
        Death1,
        Death2,
        Death3,
        Death4,

        Hit1,
        Hit2,
        Hit3,
        Hit4,

        HumanHitSFX1,
        HumanHitSFX2,
        HumanAlert1,
        HumanAlert2,
        HumanHitGroundDeath,
    }

    public enum RockerSound
    {
        Flex,
        Jump,
        JumpLand,
        BoulderDust,
        BoulderLand,
        BoulderThrow,
        BoulderHit,
        SandFall
    }

    public enum TimbertoesSound
    {
        droneTarget,
        droneLaunch,
        droneExplosion,   
        timbertoesWins,
    }

    public enum AmbientSound
    {
        ambientMusic,
        SpaceShipBackground,
        RigBackground,
        CaveBackground,
        SurfaceBackground,
        SuspenseMusic,
        DeathScreen
    }

    public static void PlayAmbientSound(AmbientSound ambientSFX)
    {
        GameObject soundGameObject = new GameObject("AmbientSFX"); 
        AudioSource audioSource = soundGameObject.AddComponent<AudioSource>();
        audioSource.clip = GetAmbientClip(ambientSFX);
        audioSource.spatialBlend = 0.0f;
        audioSource.volume = 0.1f;
        audioSource.loop = true;
        audioSource.Play();
    } 

    private static AudioClip GetAmbientClip(AmbientSound ambientSound)
    {
        foreach(GameAssets.AmbientAudioClip ambientClip in GameAssets.i.ambientAudioClipArray)
        {
            if(ambientClip.ambientSFX == ambientSound)
            {
                return ambientClip.ambientClip;
            }
        }
        Debug.Log(ambientSound + " not found!");
        return null;
    }

    public static void PlayRockerSound(RockerSound rockerSFX, Vector3 position)
    {
        GameObject soundGameObject = new GameObject("RockerSFX");
        soundGameObject.transform.position = position;
        AudioSource rockeraudioSource = soundGameObject.AddComponent<AudioSource>();
        rockeraudioSource.clip = GetRockerClip(rockerSFX);
        rockeraudioSource.maxDistance = 100f;
        rockeraudioSource.spatialBlend = 1f;
        rockeraudioSource.rolloffMode = AudioRolloffMode.Linear;
        rockeraudioSource.dopplerLevel = 0f;
        rockeraudioSource.Play();
        UnityEngine.Object.Destroy(rockeraudioSource, 5f);
    }

    private static AudioClip GetRockerClip(RockerSound rockerSound)
    {
        foreach (GameAssets.RockerAudioClip rockerClip in GameAssets.i.rockerAudioClipArray)
        {
            if (rockerClip.rockerSFX == rockerSound)
            {
                return rockerClip.rockerClip;
            }
        }
        Debug.Log(rockerSound + " not found!");
        return null;
    }

        public static void PlayTimbertoesSound(TimbertoesSound timbertoesSFX, Vector3 position)
        {
            GameObject soundGameObject = new GameObject("TimbertoesSFX");
            soundGameObject.transform.position = position;
            AudioSource timbertoesaudioSource = soundGameObject.AddComponent<AudioSource>();
            timbertoesaudioSource.clip = GetTimbertoesClip(timbertoesSFX);
            timbertoesaudioSource.maxDistance = 100f;
            timbertoesaudioSource.spatialBlend = 1f;
            timbertoesaudioSource.rolloffMode = AudioRolloffMode.Linear;
            timbertoesaudioSource.dopplerLevel = 0f;
            timbertoesaudioSource.Play();
            UnityEngine.Object.Destroy(timbertoesaudioSource, 5f);
        }

        private static AudioClip GetTimbertoesClip(TimbertoesSound timbertoesSound)
        {
            foreach (GameAssets.TimbertoesAudioClip timbertoesClip in GameAssets.i.timbertoesAudioClipArray)
            {
                if (timbertoesClip.timbertoesSFX == timbertoesSound)
                {
                    return timbertoesClip.timbertoesClip;
                }
            }
            Debug.Log(timbertoesSound + " not found!");
            return null;
        }


        public static void PlayWeaponSound(WeaponSound weaponSFX, Vector3 position)
    {
        GameObject soundGameObject = new GameObject("WeaponSFX");
        soundGameObject.transform.position = position;
        AudioSource weaponAudioSource = soundGameObject.AddComponent<AudioSource>();
        weaponAudioSource.clip = GetWeaponClip(weaponSFX);
        weaponAudioSource.maxDistance = 100f;
        weaponAudioSource.spatialBlend = 1f;
        weaponAudioSource.rolloffMode = AudioRolloffMode.Linear;
        weaponAudioSource.dopplerLevel = 0f;        
        weaponAudioSource.Play();
        UnityEngine.Object.Destroy(weaponAudioSource, 0.5f);       
    }  

    private static AudioClip GetWeaponClip(WeaponSound weaponSound)
    {
        foreach(GameAssets.WeaponAudioClip weaponClip in GameAssets.i.weaponAudioClipArray)
        {
            if(weaponClip.weaponSFX == weaponSound)
            {
                return weaponClip.weaponClip;
            }
        }
        Debug.Log(weaponSound + " not found!");
        return null;
    }

    public static void PlayEnvironmentSound(EnvironmentSound environmentSFX, Vector3 position)
    {
        GameObject soundGameObject = new GameObject("EnvironmentSFX");
        soundGameObject.transform.position = position;
        AudioSource audioSource = soundGameObject.AddComponent<AudioSource>();
        audioSource.clip = GetEnvironmentClip(environmentSFX);
        audioSource.maxDistance = 100f;
        audioSource.spatialBlend = 0.5f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.dopplerLevel = 0f;        
        audioSource.Play();
    } 

    private static AudioClip GetEnvironmentClip(EnvironmentSound environmentSound)
    {
        foreach(GameAssets.EnviromentAudioClip environmentClip in GameAssets.i.enviromentAudioClipArray)
        {
            if(environmentClip.enviromentSFX == environmentSound)
            {
                return environmentClip.enviromentClip;
            }
        }
        Debug.Log(environmentSound + " not found!");
        return null;
    }     

     public static void PlayHumanSound(HumanSound humanSound, Vector3 position)
    {
        GameObject soundGameObject = new GameObject("HumanSound");
        soundGameObject.transform.position = position;
        AudioSource audioSource = soundGameObject.AddComponent<AudioSource>();
        audioSource.clip = GetHumanClip(humanSound);
        audioSource.maxDistance = 100f;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.dopplerLevel = 0f;        
        audioSource.Play();
    } 

    private static AudioClip GetHumanClip(HumanSound humanSound)
    {
        foreach(GameAssets.HumanAudioClip humanClip in GameAssets.i.humanAudioClipArray)
        {
            if(humanClip.humanSFX == humanSound)
            {
                return humanClip.humanClip;
            }
        }
        Debug.Log(humanSound + " not found!");
        return null;
    }
  }
} 