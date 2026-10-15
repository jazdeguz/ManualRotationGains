using UnityEngine;

public class AudioManager : MonoBehaviour
{
   
   [SerializeField] AudioSource SFXSource;

   public AudioClip coinCollectSound;

   public void PlaySFX(AudioClip clip)
   {
        if (SFXSource != null && clip != null) SFXSource.PlayOneShot(clip);
   }
}
