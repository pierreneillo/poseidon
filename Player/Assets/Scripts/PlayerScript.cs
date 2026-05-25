using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerScript : MonoBehaviour
{
	// Public attributes
	public InputSystem_Actions actions;
	public float speed;
	public float jumpForce;
	
	// Private attribute
	private Rigidbody2D rb;
	private float h_speed;
	private bool grounded;
	
	
	/* General pipeline : Awake -> OnEnable -> Start -> Update/FixedUpdate -> OnDisable -> OnDestroy  */
	
	// Start even if the object or the script is disable (??)
	void Awake()
	{
		actions = new InputSystem_Actions();
	}
	
	// Called when the object is enable -> useful because can be called multiple times, for example if an object appears/desappears (!= start that only starts once)
	void OnEnable()
	{
		actions.Player.Enable();	// Makes it possible to use actions inside action maps in Unity (predefined actions of the rendering engine) => we find in action maps : Player -> lot of actions (for instance Player -> Move for next line) ; thus we setup settings
		actions.Player.Move.performed += Movement;	// Assign the Method "Movement" written below to the performance of a movement
		actions.Player.Jump.performed += Jumping;   // Same thing with "Jumping"
		actions.Player.SpecialAttack1.performed += SpecialAttack1;

		actions.Player.Move.canceled += Movement;
		actions.Player.Jump.canceled += Jumping;
		actions.Player.SpecialAttack1.canceled += SpecialAttack1;
	}
	
	// Opposite of OnEnable() -> cleaning usage (otherwise, the OnEnable() may be triggered once, then twice, ... then 10 times without freeing memory...)
	void OnDisable()
	{
		actions.Player.Disable();	
		actions.Player.Move.performed -= Movement;	
		actions.Player.Jump.performed -= Jumping;
		actions.Player.SpecialAttack1.performed -= SpecialAttack1;
	}
	
	
	
	
	// Character movements
	void Movement(InputAction.CallbackContext ctx)
	{
		h_speed = speed * ctx.ReadValue<Vector2>().x;
	}
	
	void Jumping(InputAction.CallbackContext ctx)
	{
		if(ctx.performed && grounded){			// Otherwise, the action occur when we trigger the space bar... and when we release it !
			rb.linearVelocityY = jumpForce;
		}

	}

	void SpecialAttack1(InputAction.CallbackContext ctx)
	{
		if(ctx.performed){
			rb.linearVelocityY = -jumpForce;
		}
	}

	public void SetGrounded(bool _grounded)
	{
		grounded = _grounded;
	}
	
	

	
	
	
    // Start is called once before the first execution of Update after the MonoBehavior is created
    // "The Game starts"
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();		// Link the Rigidbody2D specified on the editor 
    }

    // Update is called once per frame
    void Update()
    {
		rb.linearVelocityX = h_speed;
    }
}
