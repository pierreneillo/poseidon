using Unity.VisualScripting;
using UnityEngine;

public class Bush : Enemy
{

    // Public attributes
    [Header("Obstacle detection")]
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float groundCheckDistance = 1.0f;
    [SerializeField] private Vector2 groundCheckOffset;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Stats")]
    [SerializeField] private float speed = 2;


    private int _facingDirection = -1;
    private float _initScaleX;



    protected override void Start()
    {
        base.Start();
        _initScaleX = transform.localScale.x;
    }

    void Update()
    {
        base.Update();

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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector2 wallDirection = Application.isPlaying ? new Vector2(_facingDirection, 0) : Vector2.left;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + wallDirection * wallCheckDistance);

        Vector2 origin = (Vector2)transform.position + groundCheckOffset + wallCheckDistance * wallDirection;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}
