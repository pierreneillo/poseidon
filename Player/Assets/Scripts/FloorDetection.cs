using UnityEngine;

public class FloorDetection : MonoBehaviour
{

    Vector2 point1;
    Vector2 point2;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get informations of corners 
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        float length = box.bounds.size.x;
        float high = box.bounds.size.y;
        Vector2 topLeft = new Vector2(-length/2,high/2);
        Vector2 topRight = new Vector2(length/2,high/2);

        // Draw the line of the top
        point1 = (Vector2)transform.position + topLeft;
        point2 = (Vector2)transform.position + topRight;
        Debug.Log(point1);
        Debug.Log(point2);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Generic function automaticly called by Unity for visualisation
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(point1, point2);
    }
}
