using Unity.VisualScripting;
using UnityEngine;

public class Enemy : MonoBehaviour
{
  // Public attributes
  [Header("Stats")]
  [SerializeField] protected float maxHp = 15f;
  [SerializeField] protected float waterDamage = 1f;
  protected float hp;

  [Header("Rendering")]
  [SerializeField] protected SpriteRenderer hpBar;
  [SerializeField] protected SpriteRenderer fire;
  protected Vector2 hpBarSize;
  protected Vector2 fireSize;
  [SerializeField] protected float min_fire_scale = 0f;

  [SerializeField] protected AudioClip[] hitSounds;

  [Header("Fire Gameplay")]
  [SerializeField] protected float power = 5f;
  [SerializeField] protected GameObject fireParticlePrefab;
  [SerializeField] protected float fireSpawnInterval = 0.2f;
  protected float _fireTimer;

  [Header("VFX Feedback")]
  [SerializeField] protected ParticleSystem smokeParticleSystem;

  // protected attributes
  protected Rigidbody2D _rb;
  protected bool _burning;

  public int GPUObstacleID { get; private set; } = -1;

  protected virtual void Start()
  {
    // Movement
    GPUObstacleID = FluidBridge.RegisterObstacle(this);
    _rb = GetComponent<Rigidbody2D>();

    // HP
    hp = maxHp;
    hpBarSize = hpBar.transform.localScale;
    fireSize = fire.transform.localScale;
    _burning = true;

    if (smokeParticleSystem != null)
    {
        var emission = smokeParticleSystem.emission;
        emission.rateOverTime = 0f;
    }
  }

  protected virtual void Update()
  {
    if (_burning)
    {
      _fireTimer += Time.deltaTime;
      if (_fireTimer >= fireSpawnInterval)
      {
          _fireTimer = 0f;
          SpawnFireParticle();
      }
    }
  }

  public void GenerateSteam(uint particleHitCount)
  {
    if (smokeParticleSystem == null || !_burning) return;
    var emission = smokeParticleSystem.emission;

    if (particleHitCount > 0)
    {
      float emissionRate = Mathf.Min(particleHitCount * 5f, 50f);
      emission.rateOverTime = emissionRate;
    }
    else
    {
      emission.rateOverTime = 0f;
    }
  }

  protected void SpawnFireParticle()
  {
    if (!_burning) return;
    Vector2 randomOffset = Random.insideUnitCircle * (power * 0.3f);
    Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

    if (fireParticlePrefab != null)
    {
      Instantiate(fireParticlePrefab, spawnPos, Quaternion.identity);
    }
  }

  public bool InflictDamage(float damages)
  {
    if (_burning)
    {
      // HP management
      hp -= damages * waterDamage;

      if (hp <= 0)
      {
        Debug.Log("Enemy dead");
        FluidBridge.UnregisterObstacle(GPUObstacleID);
        DamageZone zone = GetComponentInChildren<DamageZone>();
        if (zone != null) Destroy(zone);
        if (hpBar != null) Destroy(hpBar.gameObject);
        if (fire != null) Destroy(fire.gameObject);

        _burning = false;
        if (SoundManager.instance != null) SoundManager.instance.KillSound();

        return true;
      }

      // HP bar and Fire
      if (hpBar != null)
      {
        hpBar.transform.localScale = Vector2.Lerp(new Vector2(0f, hpBarSize.y), hpBarSize, hp / maxHp);
        hpBar.color = Color.Lerp(Color.red, Color.green, hp / maxHp);
      }
      if (fire != null)
      {
        fire.transform.localScale = Vector2.Lerp(new Vector2(min_fire_scale, min_fire_scale), fireSize, hp / maxHp);
      }
      
      return false;
    }

    return true;
  }

  protected virtual void OnDestroy()
  {
    if (SoundManager.instance != null)
      SoundManager.instance.KillSound();
  }
}
