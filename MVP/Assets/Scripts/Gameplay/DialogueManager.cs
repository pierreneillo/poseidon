using UnityEngine;
using TMPro;
using System.Collections;

public class DialogueManager : MonoBehaviour {
    public static DialogueManager instance;

    [Header("UI Elements")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;

    private bool isDialogueActive = false;

    void Awake() {
        if (instance == null) instance = this;
    }

    void Start() {
        dialoguePanel.SetActive(false);
    }

    void Update() {
        if (isDialogueActive && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) {
            CloseDialogue();
        }
    }

    public void ShowDialogue(string text) {
        isDialogueActive = true;
        dialoguePanel.SetActive(true);
        dialogueText.text = text;

        Time.timeScale = 0f;
    }

    public void CloseDialogue() {
        isDialogueActive = false;
        dialoguePanel.SetActive(false);

        Time.timeScale = 1f;
    }

    public void ShowDialogueWithDelay(string text, float delay) {
        StartCoroutine(DelayedDialogueRoutine(text, delay));
    }

    private IEnumerator DelayedDialogueRoutine(string text, float delay) {
        yield return new WaitForSeconds(delay);
        ShowDialogue(text);
    }
}