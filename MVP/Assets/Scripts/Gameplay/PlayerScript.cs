using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerScript : MonoBehaviour
{
	// Public attributes
	[Header("Movement")]
	[SerializeField] private InputSystem_Actions actions;
	[SerializeField] private float speed;
	[SerializeField] private float jumpStrength;
	[SerializeField] private uint maxJumps;
	[Range(0, 1)]
	[SerializeField] private float jumpDamping;

	[Space(5)]
	[Header("Animation")]
	[SerializeField] private Animator animator;
	[SerializeField] private PhysicsMaterial2D zeroFrictionWallMaterial;  // To simplify: it can be directly changed in unity ; to make it cleaner, we can change that later

	[Header("HP")]
	[SerializeField] private Image hpBar;
	[SerializeField] private float maxHp = 15f;
	private Vector2 hpBarSize;
	private float hp;
	private Color init_c;
	private Color end_c = new Color32(25, 25, 112, 255);

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

	[Header("VFX Feedback")]
	[SerializeField] private ParticleSystem smokeParticleSystem;
	private float _burnIntensity;

	public void RegisterBurnIntensity(float intensity)
	{
		_burnIntensity = Mathf.Max(_burnIntensity, intensity);
	}


	/* General pipeline : Awake -> OnEnable -> Start -> Update/FixedUpdate -> OnDisable -> OnDestroy  */

	// Start even if the object or the script is disable (??)
	void Awake()
	{
		actions = new InputSystem_Actions();
	}

	// Called when the object is enable -> useful because can be called multiple times, for example if an object appears/disappears (!= start that only starts once)
	void OnEnable()
	{
		actions.Player.Enable();  // Makes it possible to use actions inside action maps in Unity (predefined actions of the rendering engine) => we find in action maps : Player -> lot of actions (for instance Player -> Move for next line) ; thus we setup settings
		actions.Player.Move.performed += Movement;  // Assign the Method "Movement" written below to the performance of a movement
		actions.Player.Jump.performed += Jumping;   // Same thing with "Jumping"
		actions.Player.Attack.performed += TriggerProjectile;
		actions.Player.ThrowWater.performed += OnThrowWaterInput;

		actions.Player.Move.canceled += Movement;
		actions.Player.Jump.canceled += Jumping;
		actions.Player.Attack.canceled += TriggerProjectile;
		actions.Player.ThrowWater.canceled += OnThrowWaterInput;
	}

	// Opposite of OnEnable() -> cleaning usage (otherwise, the OnEnable() may be triggered once, then twice, ... then 10 times without freeing memory...)
	void OnDisable()
	{
		actions.Player.Disable();
		actions.Player.Move.performed -= Movement;
		actions.Player.Jump.performed -= Jumping;
		actions.Player.Attack.performed -= TriggerProjectile;

		actions.Player.ThrowWater.performed -= OnThrowWaterInput;
		actions.Player.ThrowWater.canceled -= OnThrowWaterInput;
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
		if (ctx.performed && _remainingJumps > 0)
		{
			if (maxJumps == 1)
			{
				_rb.linearVelocityY = jumpStrength;
			}
			else if (maxJumps > 1)
			{
				_rb.linearVelocityY = jumpStrength * (1 - jumpDamping * ((maxJumps - _remainingJumps) / (maxJumps - 1)));
			}

			_remainingJumps--;
		}

	}

	void TriggerProjectile(InputAction.CallbackContext ctx)
	{
		if (ctx.performed)
		{
			Instantiate(Projectile, LaunchOffset.position, transform.rotation);
		}
	}

	public void SetGrounded(bool _grounded)
	{
		this._grounded = _grounded;
		if (_grounded)
		{
			_remainingJumps = maxJumps;
		}
	}




	public bool getFacingDirection()
	{
		return _isFacingRight;
	}




	private void OnThrowWaterInput(InputAction.CallbackContext context)
	{
		if (context.performed) animator.SetBool("isThrowing", true);
		if (context.canceled) animator.SetBool("isThrowing", false);
	}


	public bool damagePlayer(float damages)
	{
		// HP management
		hp -= damages;
		System.Debug.Log($"{damages} damages done, {hp} PV remaining");
		hp = Mathf.Clamp(hp, 0f, maxHp);
		float hpRatio = hp / maxHp;
		// HP bar 
		if (hpBar != null)
		{
			hpBar.fillAmount = hpRatio;
			hpBar.color = Color.Lerp(end_c, init_c, hpRatio);
		}
		return false;
	}

	// Start is called once before the first execution of Update after the MonoBehavior is created
	// "The Game starts"
	void Start()
	{
		_rb = GetComponent<Rigidbody2D>();    // Link the Rigidbody2D specified on the editor 
		_scale = transform.localScale;
		hp = maxHp;
		hpBarSize = hpBar.transform.localScale;
		init_c = hpBar.color;
		if (smokeParticleSystem != null)
		{
			var emission = smokeParticleSystem.emission;
			emission.rateOverTime = 0f;
		}
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

		if (smokeParticleSystem != null)
		{
			var emission = smokeParticleSystem.emission;
			emission.rateOverTime = _burnIntensity * 40f;
		}
		_burnIntensity = 0f;

	}


	private void Flip(float speed)
	{
		if (speed != 0)
		{
			// We invert the booleen if we change the side
			if ((speed > 0 && !_isFacingRight) || (speed < 0 && _isFacingRight))
			{
				_isFacingRight = !_isFacingRight;
			}
			transform.localScale = new Vector3(
					speed >= 0 ? _scale.x : -_scale.x,
					_scale.y,
			_scale.z);
		}
	}

}
