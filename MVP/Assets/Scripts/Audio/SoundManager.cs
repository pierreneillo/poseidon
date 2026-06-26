using Unity.VisualScripting;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    private AudioSource currentFireSound = null;
    private AudioSource backgroundSound = null;
    [SerializeField] private AudioClip backgroundClip;
    private AudioSource music = null;
    [SerializeField] private AudioClip musicClip;

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    protected virtual void Start()
    {
        if (backgroundClip == null)
        {
            Debug.LogError("Warning: Background Clip missing!");
            return;
        }
        PlayBackground(backgroundClip, 0.04f);
        StartCoroutine(LaunchMusicWithDelay(10f));
    }

    private System.Collections.IEnumerator LaunchMusicWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay); 
        
        PlayBackground(musicClip, 0.4f);
    }

    public void LaunchMusic(){
        PlayBackground(musicClip, 0.4f);
    }

    public void PlayBackground(AudioClip audioClip, float volume)
    {
        if (backgroundSound != null) Destroy(backgroundSound.gameObject);
        backgroundSound = CreateLoopSoundBackground("BackgroundSound", audioClip, volume);
    }

    public AudioSource PlayVoice(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        // On autorise les voix multiples, on ne détruit plus rien ici !
        return CreateAudioSource("VoiceSound", audioClip, spawnTransform, volume);
    }

    public AudioSource PlayBurningSound(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        return CreateAudioSource("BurningSound", audioClip, spawnTransform, volume);
    }
    
    public void PlaySplass(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        CreateAudioSource("Splass", audioClip, spawnTransform, volume, 1f);
    }

    public void PlayPace(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        CreateAudioSource("Pace", audioClip, spawnTransform, volume, 0.06f);
    }

    public void PlayFireSound(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        // Pour le feu, on peut garder la destruction pour éviter un brouhaha énorme
        if (currentFireSound != null) Destroy(currentFireSound.gameObject);
        currentFireSound = CreateAudioSource("FireSound", audioClip, spawnTransform, volume);
    }

    public AudioSource CreateAudioSource(string name, AudioClip audioClip, Transform spawnTransform, float volume, float start = 0.0f)
    {
        // Création propre d'un seul GameObject (pas de Instantiate redondant)
        GameObject go = new GameObject(name);
        go.transform.position = spawnTransform.position;

        AudioSource audioSource = go.AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.volume = volume;

        // Distance Audio Range Settings
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 20f;
        audioSource.rolloffMode = AudioRolloffMode.Linear; 

        // To crop the start
        audioSource.time = start;

        audioSource.Play();

        // Autodestruction à la fin du clip
        Destroy(go, audioClip.length);

        return audioSource;
    }

    public AudioSource CreateLoopSoundBackground(string name, AudioClip audioClip, float volume)
    {
        GameObject go = new GameObject(name);
        AudioSource audioSource = go.AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f; // Uniform sound in headset
        audioSource.loop = true;
        audioSource.Play();

        return audioSource;
    }
}