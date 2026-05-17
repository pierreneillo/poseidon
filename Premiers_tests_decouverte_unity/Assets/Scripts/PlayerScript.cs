using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerScript : MonoBehaviour
{

	public InputSystem_Actions actions;
	public float speed;
	public float jumpForce;
	float move;
	Rigidbody2D rb;
	
	/* General pipeline : Awake -> OnEnable -> Start -> Update/FixedUpdate -> OnDisable -> OnDestroy  */
	
	// Start even if the object or the script is disable (??)
	void Awake()
	{
		actions = new InputSystem_Actions();
	}
	
	// Called when the object is enable -> usefull because can be called multiple times, for example if an object appears/desappears (!= start that only starts once)
	void OnEnable()
	{
		actions.Player.Enable();	// Makes it possible to use actions inside action maps in Unity (predefined actions of the rendering engine) => we find in action maps : Player -> lot of actions (for instance Player -> Move for next line) ; thus we setup settings
		actions.Player.Move.performed += Movement;	// Assign the Method "Movement" written below to the performance of a movement
		actions.Player.Jump.performed += Jumping;	// Same thing with "Jumping"
		
		actions.Player.Move.canceled += Movement;
		actions.Player.Jump.canceled += Jumping;
	}
	
	// Opposite of OnEnable() -> cleaning usage (otherwise, the OnEnable() may be triggered once, then twice, ... then 10 times without freeing memory...)
	void OnDisable()
	{
		actions.Player.Disable();	
		actions.Player.Move.performed -= Movement;	
		actions.Player.Jump.performed -= Jumping;
	}
	
	
	
	
	// two methods needed above
	
	void Movement(InputAction.CallbackContext ctx)
	{
		move = ctx.ReadValue<Vector2>().x;
	}
	
	void Jumping(InputAction.CallbackContext ctx)
	{
		if(ctx.performed){			// Otherwise, the action occur when we trigger the space bar... and when we release it !
			rb.linearVelocityY = jumpForce;
		}
	}
	
	
	
	

	
	
	
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    // "The Game starts"
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();		// Lie rb à la propriété "Rigidbody2D" qu'on a spécifié sur le joueur dans Unity 
    }

    // Update is called once per frame
    void Update()
    {
    	// To print debug : Debug.Log("test\n");
     	rb.linearVelocityX = move * speed;
    }
}
