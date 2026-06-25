using Unity.VisualScripting;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    // Public attributes
    [Header("Obstacle detection")]
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float groundCheckDistance = 1.0f;
    [SerializeField] private Vector2 groundCheckOffset;
    [SerializeField] private LayerMask obstacleLayer;


    [Header("Stats")]
    [SerializeField] private float speed = 2;
    [SerializeField] private float maxHp = 15f;
    private float hp;

    [Header("Rendering")]
    [SerializeField] private SpriteRenderer hpBar;
    [SerializeField] private SpriteRenderer fire;
    private Vector2 hpBarSize;
    private Vector2 fireSize;

    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private AudioClip[] ShoutingSounds;
    [SerializeField] private AudioClip[] AnecdoteSounds;
    private int randomCaracterSound;



    // Private attributes
    private Rigidbody2D _rb;
    private int _facingDirection = -1;
    private float _initScaleX;
    private bool _burning;

    public int GPUObstacleID { get; private set; } = -1;

    [Header("Fire Gameplay")]
    [SerializeField] private float power = 5f;
    [SerializeField] private GameObject fireParticlePrefab;
    [SerializeField] private float fireSpawnInterval = 0.2f;
    private float _fireTimer;

    [Header("VFX Feedback")]
    [SerializeField] private ParticleSystem smokeParticleSystem;

    void Start()
    {
        // Movement
        GPUObstacleID = FluidBridge.RegisterObstacle(this);
        _rb = GetComponent<Rigidbody2D>();
        _initScaleX = transform.localScale.x;

        // HP
        hp = maxHp;
        hpBarSize = hpBar.transform.localScale;
        fireSize = fire.transform.localScale;
        _burning = true;

        // Sound design 
        randomCaracterSound = Random.Range(0, ShoutingSounds.Length);

        if (smokeParticleSystem != null)
        {
            var emission = smokeParticleSystem.emission;
            emission.rateOverTime = 0f;
        }
    }

    void Update()
    {
        if (!SoundManager.instance.IsTalking() && _burning)
        {
            // AudioSource.PlayClipAtPoint(ShoutingSounds[randomCaracterSound],transform.position, 0.3f);
            SoundManager.instance.PlaySound(ShoutingSounds[randomCaracterSound], transform, 0.5f);
        }

        if (_burning)
        {
            bool hasWall = CheckWall();
            bool hasGround = CheckGround();

            if (hasWall || !hasGround)
            {
                _facingDirection *= -1;
                transform.localScale = new Vector2(-_facingDirection * _initScaleX, transform.localScale.y);
            }

            _rb.linearVelocityX = _facingDirection * speed;

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

    void SpawnFireParticle()
    {
        if (!_burning) return;
        Vector2 randomOffset = Random.insideUnitCircle * (power * 0.3f);
        Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

        if (fireParticlePrefab != null)
        {
            Instantiate(fireParticlePrefab, spawnPos, Quaternion.identity);
        }
    }

    bool CheckWall()
    {
        Vector2 direction = new Vector2(_facingDirection, 0);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, wallCheckDistance, obstacleLayer);
        return hit.collider != null;
    }

    bool CheckGround()
    {
        Vector2 direction = new Vector2(_facingDirection, 0);
        Vector2 origin = (Vector2)transform.position + groundCheckOffset + wallCheckDistance * direction;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, obstacleLayer);
        return hit.collider != null;
    }


    public bool InflictDamage(float damages)
    {
        if (_burning)
        {
            // Audio
            int rand = Random.Range(0, hitSounds.Length);
            AudioSource.PlayClipAtPoint(hitSounds[rand], transform.position, 0.5f);

            // HP management
            hp -= damages;


            if (hp <= 0)
            {
                Debug.Log("DEAD");
                FluidBridge.UnregisterObstacle(GPUObstacleID);
                Destroy(hpBar);
                Destroy(fire);

                _burning = false;
                SoundManager.instance.KillSound();
                SoundEnnemiVoiceAnecdote.instance.isSafe = true;
                SoundEnnemiVoiceAnecdote.instance.sound = AnecdoteSounds[randomCaracterSound];

                // SoundManager.instance.PlaySound(AnecdoteSounds[randomCaracterSound],transform, 0.4f);

                return true;
            }


            // HP bar and Fire
            hpBar.transform.localScale = Vector2.Lerp(new Vector2(0f, hpBarSize.y), hpBarSize, hp / maxHp);
            hpBar.color = Color.Lerp(Color.red, Color.green, hp / maxHp);
            fire.transform.localScale = Vector2.Lerp(Vector2.zero, fireSize, hp / maxHp);


            return false;
        }

        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector2 wallDirection = Application.isPlaying ? new Vector2(_facingDirection, 0) : Vector2.left;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + wallDirection * wallCheckDistance);

        Vector2 origin = (Vector2)transform.position + groundCheckOffset + wallCheckDistance * wallDirection;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
    }

    void OnDestroy()
    {
        SoundManager.instance.KillSound();
    }
}
