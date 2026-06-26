using UnityEngine;

public class AutoDialogueTrigger : MonoBehaviour {
    [TextArea(3, 5)]
    public string introText = "Welcome to Poseidon! Use ZQSD to move and space to jump out of the water.";

    void Start() {
        Invoke("TriggerIntro", 1f);
    }

    void TriggerIntro() {
        DialogueManager.instance.ShowDialogue(introText);
        Destroy(this);
    }
}