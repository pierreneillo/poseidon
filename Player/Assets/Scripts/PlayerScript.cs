using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerScript : MonoBehaviour
{
    // Public attributes
    [Header("Movement")]
	public InputSystem_Actions actions;
	public float speed;
	public float jumpStrength;
	public uint maxJumps;
    [Range(0,1)]
    public float jumpDamping;

    [Space(5)]
    [Header("Animation")]
    public Animator animator;
	public PhysicsMaterial2D zeroFrictionWallMaterial;	// To simplify: it can be directly changed in unity ; to make it cleaner, we can change that later

    // Private attribute
    private Rigidbody2D _rb;
	private float _hSpeed = 0;
	private uint _remainingJumps = 0;
	private bool _grounded = false;
    private Vector3 _scale;
	private bool _isFacingRight = true;

	// Projectile
	public ProjectileBehaviour Projectile;
	public Transform LaunchOffset;
	
	
	/* General pipeline : Awake -> OnEnable -> Start -> Update/FixedUpdate -> OnDisable -> OnDestroy  */
	
	// Start even if the object or the script is disable (??)
	void Awake()
	{
		actions = new InputSystem_Actions();
	}
	
	// Called when the object is enable -> useful because can be called multiple times, for example if an object appears/disappears (!= start that only starts once)
	void OnEnable()
	{
		actions.Player.Enable();	// Makes it possible to use actions inside action maps in Unity (predefined actions of the rendering engine) => we find in action maps : Player -> lot of actions (for instance Player -> Move for next line) ; thus we setup settings
		actions.Player.Move.performed += Movement;	// Assign the Method "Movement" written below to the performance of a movement
		actions.Player.Jump.performed += Jumping;   // Same thing with "Jumping"
		actions.Player.SpecialAttack1.performed += SpecialAttack1;
		actions.Player.Attack.performed += TriggerProjectile;

		actions.Player.Move.canceled += Movement;
		actions.Player.Jump.canceled += Jumping;
		actions.Player.SpecialAttack1.canceled += SpecialAttack1;
		actions.Player.Attack.canceled += TriggerProjectile;
	}
	
	// Opposite of OnEnable() -> cleaning usage (otherwise, the OnEnable() may be triggered once, then twice, ... then 10 times without freeing memory...)
	void OnDisable()
	{
		actions.Player.Disable();	
		actions.Player.Move.performed -= Movement;	
		actions.Player.Jump.performed -= Jumping;
		actions.Player.SpecialAttack1.performed -= SpecialAttack1;
		actions.Player.Attack.performed -= TriggerProjectile;
	}
	
	
	
	
	// Character movements
	void Movement(InputAction.CallbackContext ctx)
	{
        _hSpeed = speed * ctx.ReadValue<Vector2>().x;
	}
	
	void Jumping(InputAction.CallbackContext ctx)
	{
        // The action occurs when we trigger the space bar
		// and not when we release it !
        if (ctx.performed && _remainingJumps > 0){
            if(maxJumps == 1)
            {
                _rb.linearVelocityY = jumpStrength;
            }
            else if(maxJumps > 1)
            {
                _rb.linearVelocityY = jumpStrength * (1 - jumpDamping * ((maxJumps - _remainingJumps) / (maxJumps - 1)));
            }
            
            _remainingJumps--;
        }

	}

	void SpecialAttack1(InputAction.CallbackContext ctx)
	{
		if(ctx.performed){
			_rb.linearVelocityY = -jumpStrength;
		}
	}
	
	void TriggerProjectile(InputAction.CallbackContext ctx){
		if(ctx.performed){
			Instantiate(Projectile, LaunchOffset.position, transform.rotation);
		}
	}

	public void SetGrounded(bool _grounded)
	{
		this._grounded = _grounded;
		if(_grounded)
		{
            _remainingJumps = maxJumps;
		}
	}




	public bool getFacingDirection(){
		return _isFacingRight;
	}



	
	

	
	
	
    // Start is called once before the first execution of Update after the MonoBehavior is created
    // "The Game starts"
    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();		// Link the Rigidbody2D specified on the editor 
        _scale = transform.localScale;
    }

	// TO DO UPDATE
    // Update is called once per frame
    void Update()
    {
        _rb.linearVelocityX = _hSpeed;
        Flip(_rb.linearVelocityX);
        animator.SetBool("isMoving", (_hSpeed != 0));

		// So we can later stick on the walls by changing the material or the material's friction
		_rb.sharedMaterial = zeroFrictionWallMaterial;

    }


    private void Flip(float speed)
    {
        if(speed != 0)
        {	
			// We invert the booleen if we change the side
			if ((speed > 0 && !_isFacingRight) || (speed < 0 && _isFacingRight))
    		{   
				_isFacingRight = !_isFacingRight;
				Debug.Log("change");
			}
            transform.localScale = new Vector3(
                speed >= 0 ? _scale.x : -_scale.x,
                _scale.y,
            _scale.z);
        }
    }

}
