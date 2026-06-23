using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class ProjectileBehaviour : MonoBehaviour
{
    public float speed = 15.0f;
    public float lifetime = 5.0f;

    private bool direction = true;
    private float cooldown; 
    private Vector3 deltaPositionProjectile = new Vector3(1.0f,0,0);

    public PlayerScript player;

    void Start(){
        player = UnityEngine.Object.FindFirstObjectByType<PlayerScript>();
        direction = player.getFacingDirection();
        Destroy(gameObject,lifetime);
        if (direction){
            transform.position += deltaPositionProjectile;
        }
        else{
            transform.position -= deltaPositionProjectile;
        }
    }

    private void Update(){
        if (direction){
            transform.position += transform.right * Time.deltaTime * speed;
        }
        else{
            transform.position -= transform.right * Time.deltaTime * speed;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision){
        // Enemy detected? Kill him!
        if (collision.gameObject.layer == LayerMask.NameToLayer("Enemies"))
        {
            Destroy(collision.gameObject);
        }
        Destroy(gameObject);
    }
}