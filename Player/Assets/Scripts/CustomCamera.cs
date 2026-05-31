using UnityEngine;

public class CustomCamera : MonoBehaviour
{
    // Public attributes
    [Header("Target settings")]
    public Transform target;
    public float xOffset = 0.0f;
    public float yOffset = 0.0f;

    [Space(5)]
    [Header("Movement settings")]
    [Range(0f, 5f)]
    public float xThresholdRadius = 1f;
    [Range(0f, 5f)]
    public float yThresholdRadius = 0.5f;
    [Range(0f, 1f)]
    public float followStrength = 0.5f;

    // Private attributes
    private float _xThresholdRadiusSquared;
    private float _yThresholdRadiusSquared;
    private Vector3 _targetOffset;

    // Start
    void Start()
    {
        _targetOffset = new Vector3(xOffset, yOffset, 0f);
        _xThresholdRadiusSquared = xThresholdRadius * xThresholdRadius;
        _yThresholdRadiusSquared = yThresholdRadius * yThresholdRadius;
        
    }

    // Update (after computing the Player Position)
    void LateUpdate()
    {
        if (target == null) return;

        Vector2 targetPosition2D = target.position - _targetOffset;
        Vector2 cameraPosition2D = transform.position;
        Vector2 diff = targetPosition2D - cameraPosition2D;
        float ellipseDistance = (diff.x * diff.x / _xThresholdRadiusSquared) + (diff.y * diff.y / _yThresholdRadiusSquared) ;

        if (ellipseDistance > 1f)
        {
            float angle = Mathf.Atan2(diff.y, diff.x);

            Vector2 targetTargetPosition = targetPosition2D - new Vector2(
                Mathf.Cos(angle) * xThresholdRadius,
                Mathf.Sin(angle) * yThresholdRadius
            );

            Vector2 newCamPos = Vector2.Lerp(cameraPosition2D, targetTargetPosition, 20f * followStrength * Time.deltaTime);

            transform.position = new Vector3(
                newCamPos.x,
                newCamPos.y,
                transform.position.z
            );
        }
    }
}