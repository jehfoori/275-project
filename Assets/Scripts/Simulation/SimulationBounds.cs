using UnityEngine;

public sealed class SimulationBounds : MonoBehaviour
{
    [SerializeField] private Vector3 size = new Vector3(30f, 18f, 30f);
    [SerializeField] private float groundY;
    [SerializeField] private Color gizmoColor = new Color(0.25f, 0.8f, 1f, 0.35f);

    public Vector3 Center => transform.position;
    public Vector3 Size => size;
    public float GroundY => groundY;

    public Vector3 RandomPointInside()
    {
        Vector3 halfSize = size * 0.5f;

        return Center + new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y),
            Random.Range(-halfSize.z, halfSize.z));
    }

    public Vector3 RandomGroundPointInside()
    {
        Vector3 halfSize = size * 0.5f;

        return new Vector3(
            Center.x + Random.Range(-halfSize.x, halfSize.x),
            groundY,
            Center.z + Random.Range(-halfSize.z, halfSize.z));
    }

    public Vector3 RandomGroundDirection()
    {
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.001f)
        {
            return Vector3.forward;
        }

        return new Vector3(direction.x, 0f, direction.y);
    }

    public Vector3 ProjectPointToGround(Vector3 position)
    {
        position.y = groundY;
        return position;
    }

    public Vector3 ProjectVectorToGround(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    public bool Contains(Vector3 position)
    {
        Vector3 localPosition = position - Center;
        Vector3 halfSize = size * 0.5f;

        return Mathf.Abs(localPosition.x) <= halfSize.x
            && Mathf.Abs(localPosition.z) <= halfSize.z;
    }

    public Vector3 ClosestPointInside(Vector3 position)
    {
        Vector3 localPosition = position - Center;
        Vector3 halfSize = size * 0.5f;

        return Center + new Vector3(
            Mathf.Clamp(localPosition.x, -halfSize.x, halfSize.x),
            groundY - Center.y,
            Mathf.Clamp(localPosition.z, -halfSize.z, halfSize.z));
    }

    public Vector3 GetCenteringDirection(Vector3 position)
    {
        Vector3 localPosition = position - Center;
        Vector3 halfSize = size * 0.5f;
        Vector3 normalizedOffset = new Vector3(
            halfSize.x > 0f ? localPosition.x / halfSize.x : 0f,
            0f,
            halfSize.z > 0f ? localPosition.z / halfSize.z : 0f);

        float distanceFromCenter = normalizedOffset.magnitude;
        if (distanceFromCenter < 0.65f)
        {
            return Vector3.zero;
        }

        return ProjectVectorToGround(Center - position).normalized;
    }

    private void OnValidate()
    {
        size = new Vector3(
            Mathf.Max(1f, size.x),
            Mathf.Max(1f, size.y),
            Mathf.Max(1f, size.z));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(ProjectPointToGround(Center), new Vector3(size.x, 0.05f, size.z));
    }
}
