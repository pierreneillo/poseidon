using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class ProjectileBehaviour : MonoBehaviour
{
    public float speed = 15.0f;
    public float lifetime = 5.0f;

    private bool direction = true;
    private float cooldown; 

    public PlayerScript player;

    void Start(){
        player = UnityEngine.Object.FindFirstObjectByType<PlayerScript>();
        direction = player.getFacingDirection();
        Destroy(gameObject,lifetime);
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
        Destroy(gameObject);
    }
}