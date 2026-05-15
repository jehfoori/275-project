using UnityEngine;

public sealed class SimulationBounds : MonoBehaviour
{
    [SerializeField] private Vector3 size = new Vector3(30f, 18f, 30f);
    [SerializeField] private Color gizmoColor = new Color(0.25f, 0.8f, 1f, 0.35f);

    public Vector3 Center => transform.position;
    public Vector3 Size => size;

    public Vector3 RandomPointInside()
    {
        Vector3 halfSize = size * 0.5f;

        return Center + new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y),
            Random.Range(-halfSize.z, halfSize.z));
    }

    public bool Contains(Vector3 position)
    {
        Vector3 localPosition = position - Center;
        Vector3 halfSize = size * 0.5f;

        return Mathf.Abs(localPosition.x) <= halfSize.x
            && Mathf.Abs(localPosition.y) <= halfSize.y
            && Mathf.Abs(localPosition.z) <= halfSize.z;
    }

    public Vector3 ClosestPointInside(Vector3 position)
    {
        Vector3 localPosition = position - Center;
        Vector3 halfSize = size * 0.5f;

        return Center + new Vector3(
            Mathf.Clamp(localPosition.x, -halfSize.x, halfSize.x),
            Mathf.Clamp(localPosition.y, -halfSize.y, halfSize.y),
            Mathf.Clamp(localPosition.z, -halfSize.z, halfSize.z));
    }

    public Vector3 GetCenteringDirection(Vector3 position)
    {
        Vector3 localPosition = position - Center;
        Vector3 halfSize = size * 0.5f;
        Vector3 normalizedOffset = new Vector3(
            halfSize.x > 0f ? localPosition.x / halfSize.x : 0f,
            halfSize.y > 0f ? localPosition.y / halfSize.y : 0f,
            halfSize.z > 0f ? localPosition.z / halfSize.z : 0f);

        float distanceFromCenter = normalizedOffset.magnitude;
        if (distanceFromCenter < 0.65f)
        {
            return Vector3.zero;
        }

        return (Center - position).normalized;
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
        Gizmos.DrawWireCube(Center, size);
    }
}
