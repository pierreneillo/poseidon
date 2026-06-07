using System;
using UnityEngine;
using UnityEngine.Events;

public class GroundChecker : MonoBehaviour
{
    public PlayerScript player;

    // Allows to know if we are to the ground or not : 
    // if we touch 2 objects, but that we left one, otherwise the algorithm will believe that
    // we have left the ground. By counting the collisions, we avoid this.
    private int collidedGrounds = 0;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {

            Collider2D playerCollider = GetComponent<Collider2D>();

            // If the player is above the triggered zone, it's that it is a collision with the floor
            if (playerCollider.bounds.min.y >= other.bounds.max.y - 0.1f)   // -0.1f margin in case of
            {
                // Debug.Log("Touché par le dessus");
                collidedGrounds++;
                player.SetGrounded(true);
            }

        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {
            collidedGrounds--;

            // collidedGrounds == 0 means that we are on the air
            if(collidedGrounds == 0)
            {
                player.SetGrounded(false);
            } 
        }
            
    }
}
