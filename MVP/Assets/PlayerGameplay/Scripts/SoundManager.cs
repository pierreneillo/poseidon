using Unity.VisualScripting;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    // [SerializeField] private AudioClip soundObject;
    private AudioSource audioSource = null;
    private float talkingUntil;

    public void Awake(){
        if (instance == null){
            instance = this;
        }
    }

    public void PlaySound(AudioClip audioClip, Transform spawnTransform, float volume){
        // Create new entoty
        AudioSource soundObject = new GameObject("Sound").AddComponent<AudioSource>();
        audioSource = Instantiate(soundObject, spawnTransform.position, Quaternion.identity);
        audioSource.clip = audioClip;
        audioSource.volume = volume;
        audioSource.Play();

        float clipLength = audioSource.clip.length;
        talkingUntil = Time.time + clipLength;

        Destroy(audioSource.gameObject, clipLength);
    }

    public bool IsTalking(){
        return Time.time <= talkingUntil;
    }

    public void KillSound(){
        Destroy(audioSource.gameObject);
    }
}