using Unity.VisualScripting;
using UnityEngine;

public class SoundEnnemiVoiceAnecdote : MonoBehaviour
{
    public AudioClip sound;
    public float delay = 2f;

    private float timer;
    private bool playerInRange;
    private bool hasPlayed = false;
    public bool isSafe = false;

    public bool wantVoice = false;


    public void Awake(){

    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Entered Box");
            playerInRange = true;
            timer = 0f;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Exit Box");
            playerInRange = false;
            timer = 0f;
        }
    }

    private void Update()
    {
        if (isSafe && wantVoice){
            if (!playerInRange || hasPlayed)
                return;

            timer += Time.deltaTime;

            if (timer >= delay)
            {
                hasPlayed = true;
                SoundManager.instance.PlayVoice(sound,transform, 0.4f);
            }
        }
    }
}