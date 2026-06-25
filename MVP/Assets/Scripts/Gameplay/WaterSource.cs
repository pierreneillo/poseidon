using UnityEngine;

public class WaterSource : MonoBehaviour
{
    [SerializeField] private uint nbOfParticles = 500;
    [SerializeField] private float spawnRadius = 1f;
    private void OnEnable()
    {
        FluidBridge.RegisterWaterSource(this);
    }

    public uint getNbOfParticles()
    {
        return nbOfParticles;
    }

    public float getSpawnRadius()
    {
        return spawnRadius;
    }
}
