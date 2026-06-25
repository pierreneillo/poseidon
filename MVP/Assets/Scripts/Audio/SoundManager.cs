using Unity.VisualScripting;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    // [SerializeField] private AudioClip soundObject;
    private AudioSource currentVoice = null;
    private AudioSource currentFireSound = null;
    private float talkingUntil;
    private AudioSource backgroundSound = null;
    [SerializeField] private AudioClip backgroundClip;

    public void Awake(){
        if (instance == null){
            instance = this;
        }
    }

    protected virtual void Start(){
        PlayBackground(backgroundClip, 0.04f);
    }



    public void PlayBackground(AudioClip audioClip, float volume)
    {
        if (backgroundSound != null) Destroy(backgroundSound.gameObject);
        
        backgroundSound = CreateLoopSoundBackground("BackgroundSound", audioClip, volume);
    }

    public void PlayVoice(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        if (currentVoice != null) Destroy(currentVoice.gameObject);
        
        currentVoice = CreateAudioSource("VoiceSound", audioClip, spawnTransform, volume);
        talkingUntil = Time.time + audioClip.length;
    }

    public void PlayFireSound(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        if (currentFireSound != null) Destroy(currentFireSound.gameObject);
        
        currentFireSound = CreateAudioSource("FireSound", audioClip, spawnTransform, volume);
    }





    public AudioSource CreateAudioSource(string name, AudioClip audioClip, Transform spawnTransform, float volume){
        // Create new entity
        AudioSource audioSource = new GameObject(name).AddComponent<AudioSource>();
        audioSource = Instantiate(audioSource, spawnTransform.position, Quaternion.identity);
        audioSource.clip = audioClip;
        audioSource.volume = volume;

         // Distance Audio Range Settings
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 2f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;


        audioSource.Play();

        float clipLength = audioSource.clip.length;
        Destroy(audioSource.gameObject, clipLength);

        return audioSource;
    }

     public AudioSource CreateLoopSoundBackground(string name, AudioClip audioClip, float volume){
        // Create new entity
        AudioSource audioSource = new GameObject(name).AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;   // Uniform sound in headset
        
        audioSource.loop = true;

        audioSource.Play();

        return audioSource;
    }









    public bool IsTalking(){
        return Time.time <= talkingUntil;
    }

    public void KillSounds(){
        if (currentVoice != null) Destroy(currentVoice.gameObject);
        if (currentFireSound != null) Destroy(currentFireSound.gameObject);
    }
}