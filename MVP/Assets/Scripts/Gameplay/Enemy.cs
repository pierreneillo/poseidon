using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
  public bool wantSpeaches = false; 

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
  [SerializeField] protected AudioClip[] fireDesappering;
  [SerializeField] protected AudioClip[] fireSound;
  [SerializeField] private AudioClip[] ShoutingSounds;
  [SerializeField] private AudioClip[] AnecdoteSounds;
  private int randomCaracterSound;

  [Header("Fire Gameplay")]
  [SerializeField] protected float power = 5f;
  [SerializeField] protected GameObject fireParticlePrefab;
  [SerializeField] protected float fireSpawnInterval = 0.2f;
  protected float _fireTimer;

  [Header("VFX Feedback")]
  [SerializeField] protected ParticleSystem smokeParticleSystem;

  [Header("Audio Cooldown")]
  [SerializeField] protected float shoutCooldown = 1f; 
  protected float _nextShoutTime = 0f;
  protected float _nextBurningTime = 0f;

  // protected attributes
  protected Rigidbody2D _rb;
  protected bool _burning;
  protected float _nextHitSoundTime = 0f;
  protected List<AudioSource> currentVoiceSources;

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
    currentVoiceSources = new List<AudioSource>();

    if (smokeParticleSystem != null)
    {
        var emission = smokeParticleSystem.emission;
        emission.rateOverTime = 0f;
    }
    
    // Sound design 
    randomCaracterSound = Random.Range(0, ShoutingSounds.Length);
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
      

    // Sounds
      // Shout
      if(Time.time >= _nextShoutTime && wantSpeaches){
        AudioClip clipToPlay = ShoutingSounds[randomCaracterSound];
        currentVoiceSources.Add(SoundManager.instance.PlayVoice(clipToPlay,transform, 0.8f));
        _nextShoutTime = Time.time + clipToPlay.length + shoutCooldown;
      }
      // Burning
      if(Time.time >= _nextBurningTime){
        int randomFireSound = Random.Range(0, fireSound.Length);
        AudioClip clipToPlay = fireSound[randomFireSound];
        currentVoiceSources.Add(SoundManager.instance.PlayBurningSound(clipToPlay,transform, 0.8f));
        _nextBurningTime = Time.time + clipToPlay.length - 0.1f;
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


      if (damages > 0 && Time.time >= _nextHitSoundTime){
        // Play sound
        int rand = Random.Range(0, hitSounds.Length);
        AudioSource.PlayClipAtPoint(hitSounds[rand], transform.position, 0.5f);
        rand = Random.Range(0, fireDesappering.Length);
        SoundManager.instance.PlayFireSound(fireDesappering[rand], transform, 0.3f);
        // AudioSource.PlayClipAtPoint(fireDesappering[rand], transform.position, 0.3f);
        _nextHitSoundTime = Time.time + 0.5f;
      }

      if (hp <= 0)
      {
        Debug.Log("Enemy dead");
        FluidBridge.UnregisterObstacle(GPUObstacleID);
        DamageZone zone = GetComponentInChildren<DamageZone>();
        if (zone != null) Destroy(zone);
        if (hpBar != null) Destroy(hpBar.gameObject);
        if (fire != null) Destroy(fire.gameObject);

        _burning = false;

        // Sound Design
        for (int i = 0 ; i < currentVoiceSources.Count ; i++){
          Destroy(currentVoiceSources[i].gameObject);
        }
        currentVoiceSources = new List<AudioSource>();
        
        if (wantSpeaches){
          SoundEnnemiVoiceAnecdote localAnecdote = GetComponentInChildren<SoundEnnemiVoiceAnecdote>();
          if (localAnecdote != null) {
            localAnecdote.wantVoice = true;
            localAnecdote.isSafe = true;
            if (AnecdoteSounds != null && AnecdoteSounds.Length > 0) {
              localAnecdote.sound = AnecdoteSounds[randomCaracterSound];
            }
          }
        }
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
  }
}
