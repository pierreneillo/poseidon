using UnityEngine;

public class GroundChecker : MonoBehaviour
{
    public PlayerScript player;

    private int collidedGrounds = 0;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {
            collidedGrounds++;
            player.SetGrounded(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {
            collidedGrounds--;
            if(collidedGrounds == 0)
            {
                player.SetGrounded(false);
            } 
        }
            
    }
}
