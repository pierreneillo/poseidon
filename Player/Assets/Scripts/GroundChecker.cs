using System;
using UnityEngine;
using UnityEngine.Events;

public class GroundChecker : MonoBehaviour
{
    public UnityEvent<bool> setCollisionStateAction;

    private int collidedGrounds = 0;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {
            collidedGrounds++;
            setCollisionStateAction?.Invoke(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {
            collidedGrounds--;
            if(collidedGrounds == 0)
            {
                setCollisionStateAction?.Invoke(false);
            } 
        }
            
    }
}
