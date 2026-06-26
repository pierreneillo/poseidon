using UnityEngine;

public class Friend : MonoBehaviour {
    [TextArea(3, 5)]
    public string friendText = "Welcome back Poseidon! Things have changed a lot in the past thousand years... All my friends are burning because of climate change! Can you help me save them?";
    public string helperText = "Be wary: your attacks using F consume water, the very thing that drives you! And don't get too close to enemies so you don't evaporate!";

    private bool hasSpoken = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasSpoken && other.CompareTag("Player"))
        {
            hasSpoken = true;
            DialogueManager.instance.ShowDialogue(friendText);
            DialogueManager.instance.ShowDialogueWithDelay(helperText, 1f);
        }
    }
}