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
    private Vector2 hpBarSize;

    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private AudioClip[] ShoutingSounds;
    private int randomCaracterSound;



    // Private attributes
    private Rigidbody2D _rb;
    private int _facingDirection = -1;
    private float _initScaleX;

    public int GPUObstacleID { get; private set; } = -1;

    void Start()
    {
        GPUObstacleID = FluidBridge.RegisterObstacle(this);
        _rb = GetComponent<Rigidbody2D>();
        _initScaleX = transform.localScale.x;
        hp = maxHp;
        hpBarSize = hpBar.transform.localScale;
        randomCaracterSound = Random.Range(0, ShoutingSounds.Length);
    }

    void Update()
    {
        if(!SoundManager.instance.IsTalking()){
            // AudioSource.PlayClipAtPoint(ShoutingSounds[randomCaracterSound],transform.position, 0.3f);
            SoundManager.instance.PlaySound(ShoutingSounds[randomCaracterSound],transform, 0.3f);
        }

        bool hasWall = CheckWall();
        bool hasGround = CheckGround();

        if (hasWall || !hasGround)
        {
            _facingDirection *= -1;
            transform.localScale = new Vector2(-_facingDirection * _initScaleX, transform.localScale.y);
        }

        _rb.linearVelocityX = _facingDirection * speed;

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
        int rand = Random.Range(0, hitSounds.Length);
        AudioSource.PlayClipAtPoint(hitSounds[rand],transform.position, 0.5f);
        hp -= damages;
        if (hp <= 0)
        {
            Debug.Log("DEAD");
            Destroy(gameObject);
            return true;
        }

        hpBar.transform.localScale = Vector2.Lerp(new Vector2(0f, hpBarSize.y), hpBarSize, hp/maxHp);
        hpBar.color = Color.Lerp(Color.red, Color.green, hp / maxHp);

        Debug.Log(hp);
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

    void OnDestroy() {
        FluidBridge.UnregisterObstacle(GPUObstacleID);
        SoundManager.instance.KillSound();
    }
}
