using Unity.VisualScripting;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    private AudioSource currentFireSound = null;
    private AudioSource backgroundSound = null;
    [SerializeField] private AudioClip backgroundClip;

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
        PlayBackground(backgroundClip, 10f);
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

    public void PlayFireSound(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        // Pour le feu, on peut garder la destruction pour éviter un brouhaha énorme
        if (currentFireSound != null) Destroy(currentFireSound.gameObject);
        currentFireSound = CreateAudioSource("FireSound", audioClip, spawnTransform, volume);
    }

    public AudioSource CreateAudioSource(string name, AudioClip audioClip, Transform spawnTransform, float volume)
    {
        // Création propre d'un seul GameObject (pas de Instantiate redondant)
        GameObject go = new GameObject(name);
        go.transform.position = spawnTransform.position;

        AudioSource audioSource = go.AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.volume = volume;

        // Paramètres Audio 3D corrigés
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 15f; // Modifié pour être audible de plus loin
        audioSource.rolloffMode = AudioRolloffMode.Linear; // Linear est plus naturel en 2D

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
        audioSource.spatialBlend = 0f; // 2D (dans tout le casque)
        audioSource.loop = true;
        audioSource.Play();

        return audioSource;
    }
}