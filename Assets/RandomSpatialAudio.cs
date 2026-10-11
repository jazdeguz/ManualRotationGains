
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class RandomSpatialAudio : MonoBehaviour
{
    [Header("VR Player")]
    public Transform playerHead;

    [Header("Sound")]
    public AudioClip[] soundClips;

    [Header("Horizontal Distance (Meters)")]
    public float minRadius = 40f;
    public float maxRadius = 640f;

    [Header("Height Above Player (Meters)")]
    public float minHeight = 0f;
    public float maxHeight = 6f;

    [Header("Pause Between Sounds (Seconds)")]
    public float minPause = 30f;
    public float maxPause = 120f;

    private AudioSource audioSource;

    private IEnumerator Start()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.dopplerLevel = 0f;
        audioSource.volume = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 40f;
        audioSource.maxDistance = 1500f;


        if (playerHead == null ||
            soundClips == null ||
            soundClips.Length == 0)
        {
            Debug.LogError(
                "Assign the VR player camera and sound clips."
            );
            yield break;
        }

        while (true)
        {
            // Wait 30 to 120 seconds
            float pause = Random.Range(minPause, maxPause);
            yield return new WaitForSeconds(pause);

            // Random horizontal angle (360 degrees)
            float angle = Random.Range(0f, Mathf.PI * 2f);

            // Uniform random position in the annulus
            float minSq = minRadius * minRadius;
            float maxSq = maxRadius * maxRadius;
            float radius = Mathf.Sqrt(
                Random.Range(minSq, maxSq)
            );

            // X and Z are horizontal, Y is vertical
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = Random.Range(minHeight, maxHeight);

            // Position relative to player's current location
            transform.position = playerHead.position +
                new Vector3(x, y, z);

            // Select a random sound clip
            AudioClip clip = soundClips[
                Random.Range(0, soundClips.Length)
            ];

            audioSource.clip = clip;
            audioSource.Play();

            Debug.Log("3D sound at: " + transform.position);

            // Wait for the sound to finish
            yield return new WaitForSeconds(clip.length);
        }
    }
}
