using UnityEngine;

public class GroundChecker : MonoBehaviour
{
    public PlayerScript player;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
            player.SetGrounded(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
            player.SetGrounded(false);
    }
}
