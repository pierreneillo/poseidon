using UnityEngine;

public class BushZone : MonoBehavior {
    [SerializeField] private float maxDamagePerSecond = 5f;
    [SerializeField] private float sigma = 3f;
    [SerializeField] private float radius = 5f;

    void Update() {
        PlayerScript player = Object.FindFirstObjectByType<PLayerScript>();
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.transform.position);

        if (distance <= radius) {
            float r2 = distance * distance;
            float gauss = Mathf.Exp(-r2/(2f * sigma * sigma));
            float damageThisFrame = maxDamagePerSecond * gauss * Time.deltaTime;

            if (damageThisFrame > 0.001f) {
                player.damagePlayer(damageThisFrame);
                player.RegisterBurnIntensity(gauss);
            }
        }
    }
}