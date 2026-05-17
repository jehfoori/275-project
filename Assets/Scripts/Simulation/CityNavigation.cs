using System.Collections.Generic;
using UnityEngine;

public sealed class CityNavigation
{
    private const string WaypointNamePrefix = "CrowdWaypoint";
    private const string ObstacleVolumeName = "ObstacleVolume";

    private readonly List<Node> nodes = new List<Node>();
    private readonly List<BoxCollider> obstacles = new List<BoxCollider>();
    private readonly List<Link> links = new List<Link>();
    private readonly Queue<int> frontier = new Queue<int>();
    private readonly List<int> routeScratch = new List<int>();
    private int[] cameFrom = new int[0];

    public IReadOnlyList<Node> Nodes => nodes;
    public IReadOnlyList<BoxCollider> Obstacles => obstacles;
    public IReadOnlyList<Link> Links => links;
    public bool HasWaypoints => nodes.Count > 0;

    public void Build(
        Transform waypointRoot,
        Transform obstacleRoot,
        float agentClearance,
        float maxLinkDistance,
        float segmentSampleSpacing)
    {
        nodes.Clear();
        obstacles.Clear();
        links.Clear();

        CollectObstacles(obstacleRoot);
        CollectWaypoints(waypointRoot, Mathf.Max(0.05f, agentClearance));
        BuildLinks(Mathf.Max(0.05f, agentClearance), Mathf.Max(1f, maxLinkDistance), Mathf.Max(0.5f, segmentSampleSpacing));
    }

    public bool TryGetRandomWalkablePoint(float clearance, out Vector3 point)
    {
        clearance = Mathf.Max(0.05f, clearance);

        if (links.Count > 0)
        {
            for (int i = 0; i < 24; i++)
            {
                Link link = links[Random.Range(0, links.Count)];
                if (!link.IsClear)
                {
                    continue;
                }

                Vector3 start = nodes[link.From].Position;
                Vector3 end = nodes[link.To].Position;
                Vector3 direction = Flatten(end - start);
                Vector3 candidate = Vector3.Lerp(start, end, Random.value);

                if (direction.sqrMagnitude > 0.001f)
                {
                    Vector3 lateral = Vector3.Cross(Vector3.up, direction.normalized);
                    candidate += lateral * Random.Range(-clearance * 2.5f, clearance * 2.5f);
                }

                if (!IsPointBlocked(candidate, clearance))
                {
                    point = candidate;
                    return true;
                }
            }
        }

        if (nodes.Count > 0)
        {
            for (int i = 0; i < 24; i++)
            {
                Vector3 candidate = nodes[Random.Range(0, nodes.Count)].Position + Random.insideUnitSphere * clearance * 3f;
                candidate.y = nodes[0].Position.y;

                if (!IsPointBlocked(candidate, clearance))
                {
                    point = candidate;
                    return true;
                }
            }

            point = nodes[Random.Range(0, nodes.Count)].Position;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    public int GetNearestNodeIndex(Vector3 position)
    {
        int nearestIndex = -1;
        float nearestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < nodes.Count; i++)
        {
            float distanceSqr = Flatten(nodes[i].Position - position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    public int GetRandomDifferentNodeIndex(int currentIndex)
    {
        if (nodes.Count == 0)
        {
            return -1;
        }

        if (nodes.Count == 1)
        {
            return 0;
        }

        int targetIndex = currentIndex;
        for (int i = 0; i < 12 && targetIndex == currentIndex; i++)
        {
            targetIndex = Random.Range(0, nodes.Count);
        }

        return targetIndex == currentIndex ? (currentIndex + 1) % nodes.Count : targetIndex;
    }

    public bool TryBuildRoute(int startIndex, int targetIndex, List<int> route)
    {
        route.Clear();

        if (startIndex < 0 || startIndex >= nodes.Count || targetIndex < 0 || targetIndex >= nodes.Count)
        {
            return false;
        }

        if (startIndex == targetIndex)
        {
            route.Add(startIndex);
            return true;
        }

        EnsureSearchCapacity();
        for (int i = 0; i < cameFrom.Length; i++)
        {
            cameFrom[i] = -1;
        }

        frontier.Clear();
        frontier.Enqueue(startIndex);
        cameFrom[startIndex] = startIndex;

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            if (current == targetIndex)
            {
                break;
            }

            List<int> neighbors = nodes[current].Neighbors;
            for (int i = 0; i < neighbors.Count; i++)
            {
                int next = neighbors[i];
                if (cameFrom[next] != -1)
                {
                    continue;
                }

                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (cameFrom[targetIndex] == -1)
        {
            route.Add(targetIndex);
            return false;
        }

        routeScratch.Clear();
        for (int current = targetIndex; current != startIndex; current = cameFrom[current])
        {
            routeScratch.Add(current);
        }

        routeScratch.Add(startIndex);

        for (int i = routeScratch.Count - 1; i >= 0; i--)
        {
            route.Add(routeScratch[i]);
        }

        return true;
    }

    public bool IsPointBlocked(Vector3 point, float clearance)
    {
        clearance = Mathf.Max(0f, clearance);
        float clearanceSqr = clearance * clearance;

        for (int i = 0; i < obstacles.Count; i++)
        {
            BoxCollider obstacle = obstacles[i];
            if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 closest = obstacle.ClosestPoint(point);
            if ((Flatten(closest - point)).sqrMagnitude <= clearanceSqr)
            {
                return true;
            }
        }

        return false;
    }

    public Vector3 GetObstacleAvoidance(Vector3 position, Vector3 velocity, float lookAheadDistance, float clearance)
    {
        Vector3 forward = Flatten(velocity);
        if (forward.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        forward.Normalize();
        Vector3 lookAheadPoint = position + forward * Mathf.Max(0.1f, lookAheadDistance);
        Vector3 avoidance = Vector3.zero;
        float influenceDistance = Mathf.Max(0.1f, clearance);

        for (int i = 0; i < obstacles.Count; i++)
        {
            BoxCollider obstacle = obstacles[i];
            if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy)
            {
                continue;
            }

            AddAvoidanceFromSample(position, obstacle, influenceDistance, ref avoidance);
            AddAvoidanceFromSample(lookAheadPoint, obstacle, influenceDistance, ref avoidance);
        }

        return avoidance.sqrMagnitude > 0.001f ? avoidance.normalized : Vector3.zero;
    }

    public Vector3 ProjectOutsideObstacles(Vector3 position, float clearance)
    {
        clearance = Mathf.Max(0.05f, clearance);
        Vector3 projected = position;

        for (int pass = 0; pass < 3; pass++)
        {
            bool moved = false;

            for (int i = 0; i < obstacles.Count; i++)
            {
                BoxCollider obstacle = obstacles[i];
                if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 closest = obstacle.ClosestPoint(projected);
                Vector3 away = Flatten(projected - closest);
                float distance = away.magnitude;

                if (distance >= clearance)
                {
                    continue;
                }

                if (distance < 0.001f)
                {
                    Vector3 centerOffset = Flatten(projected - obstacle.bounds.center);
                    away = centerOffset.sqrMagnitude > 0.001f ? centerOffset.normalized : Vector3.forward;
                }
                else
                {
                    away /= distance;
                }

                projected += away * (clearance - distance + 0.02f);
                moved = true;
            }

            if (!moved)
            {
                break;
            }
        }

        return projected;
    }

    public bool HasLineOfSight(Vector3 start, Vector3 end, float clearance, float sampleSpacing)
    {
        Vector3 delta = Flatten(end - start);
        float distance = delta.magnitude;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.5f, sampleSpacing)));

        for (int i = 1; i < sampleCount; i++)
        {
            Vector3 sample = Vector3.Lerp(start, end, i / (float)sampleCount);
            if (IsPointBlocked(sample, clearance))
            {
                return false;
            }
        }

        return true;
    }

    private void CollectWaypoints(Transform waypointRoot, float agentClearance)
    {
        if (waypointRoot == null)
        {
            return;
        }

        for (int i = 0; i < waypointRoot.childCount; i++)
        {
            Transform child = waypointRoot.GetChild(i);
            if (!child.gameObject.activeInHierarchy || !child.name.StartsWith(WaypointNamePrefix))
            {
                continue;
            }

            nodes.Add(new Node(child, GetWalkableWaypointPosition(child.position, agentClearance)));
        }
    }

    private Vector3 GetWalkableWaypointPosition(Vector3 position, float agentClearance)
    {
        if (!IsPointBlocked(position, agentClearance))
        {
            return position;
        }

        return ProjectOutsideObstacles(position, agentClearance);
    }

    private void CollectObstacles(Transform obstacleRoot)
    {
        if (obstacleRoot == null)
        {
            return;
        }

        BoxCollider[] colliders = obstacleRoot.GetComponentsInChildren<BoxCollider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            BoxCollider collider = colliders[i];
            if (collider.enabled && collider.gameObject.activeInHierarchy && collider.name == ObstacleVolumeName)
            {
                obstacles.Add(collider);
            }
        }
    }

    private void BuildLinks(float agentClearance, float maxLinkDistance, float segmentSampleSpacing)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                float distance = Flatten(nodes[j].Position - nodes[i].Position).magnitude;
                if (distance > maxLinkDistance)
                {
                    continue;
                }

                bool clear = HasLineOfSight(nodes[i].Position, nodes[j].Position, agentClearance, segmentSampleSpacing);
                links.Add(new Link(i, j, clear));

                if (!clear)
                {
                    continue;
                }

                nodes[i].Neighbors.Add(j);
                nodes[j].Neighbors.Add(i);
            }
        }
    }

    private void EnsureSearchCapacity()
    {
        if (cameFrom.Length != nodes.Count)
        {
            cameFrom = new int[nodes.Count];
        }
    }

    private static void AddAvoidanceFromSample(Vector3 sample, BoxCollider obstacle, float influenceDistance, ref Vector3 avoidance)
    {
        Vector3 closest = obstacle.ClosestPoint(sample);
        Vector3 away = Flatten(sample - closest);
        float distance = away.magnitude;

        if (distance >= influenceDistance)
        {
            return;
        }

        if (distance < 0.001f)
        {
            Vector3 centerOffset = Flatten(sample - obstacle.bounds.center);
            away = centerOffset.sqrMagnitude > 0.001f ? centerOffset.normalized : Vector3.forward;
            distance = 0f;
        }
        else
        {
            away /= distance;
        }

        float strength = 1f - Mathf.Clamp01(distance / influenceDistance);
        avoidance += away * strength;
    }

    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    public sealed class Node
    {
        public readonly Transform Transform;
        public readonly List<int> Neighbors = new List<int>();
        private readonly Vector3 position;

        public Node(Transform transform, Vector3 position)
        {
            Transform = transform;
            this.position = position;
        }

        public Vector3 Position => position;
    }

    public struct Link
    {
        public int From;
        public int To;
        public bool IsClear;

        public Link(int from, int to, bool isClear)
        {
            From = from;
            To = to;
            IsClear = isClear;
        }
    }
}
