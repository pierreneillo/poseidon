using UnityEngine;

public class BushZone : MonoBehaviour
{
    [SerializeField] private float maxDamagePerSecond = 5f;
    [SerializeField] private float sigma = 3f;
    [SerializeField] private float radius = 5f;

    private Enemy _associatedEnemy;

    void Start()
    {
        _associatedEnemy = GetComponentInParent<Enemy>();
    }

    void Update()
    {
        if (_associatedEnemy != null && _associatedEnemy.InflictDamage(0) == true) {
            return;
        }
        PlayerScript player = Object.FindFirstObjectByType<PlayerScript>();
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.transform.position);
        UnityEngine.Debug.Log($"Distance to player : {distance}");
        if (distance <= radius)
        {
            float r2 = distance * distance;
            float gauss = Mathf.Exp(-r2 / (2f * sigma * sigma));
            float damageThisFrame = maxDamagePerSecond * gauss * Time.deltaTime;

            if (damageThisFrame > 0.001f)
            {
                player.damagePlayer(damageThisFrame);
                player.RegisterBurnIntensity(gauss);
            }
        }
    }
}