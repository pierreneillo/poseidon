using UnityEngine;

public class Enemy : MonoBehaviour
{
    // Public attributes
    [Header("Obstacle detection")]
    public float wallCheckDistance = 0.5f;
    public float groundCheckDistance = 1.0f;
    public Vector2 groundCheckOffset;
    public LayerMask obstacleLayer;

    [Header("Movement settings")]
    public float speed = 2;

    // Private attributes
    private Rigidbody2D _rb;
    private int _facingDirection = -1;
    private float _initScaleX;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _initScaleX = transform.localScale.x;
    }

    void Update()
    {

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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector2 wallDirection = Application.isPlaying ? new Vector2(_facingDirection, 0) : Vector2.left;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + wallDirection * wallCheckDistance);

        Vector2 origin = (Vector2)transform.position + groundCheckOffset + wallCheckDistance * wallDirection;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
    }
}
