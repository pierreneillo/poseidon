using UnityEngine;

public class PulseUI : MonoBehaviour {
    [Header("Pulse Settings")]
    public float speed = 3f;
    public float minScale = 0.9f;
    public float maxScale = 1.1f;

    private Vector3 initialScale;

    void Start() {
        initialScale = transform.localScale;
    }

    void Update() {
        float pingPong = Mathf.PingPong(Time.unscaledTime * speed, 1f);
        float currentScale = Mathf.Lerp(minScale, maxScale, pingPong);
        transform.localScale = initialScale * currentScale;
    }
}