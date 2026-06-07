using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class ProjectileBehaviour : MonoBehaviour
{
    public float Speed = 4.5f;

    private void Update(){
        transform.position += -transform.right * Time.deltaTime * Speed;
    }

    private void OnCollisionEnter2(Collision2D collision){
        Destroy(gameObject);
    }
}