using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class MeshSurfaceWrapper : MonoBehaviour
{
    [Header("Setup")]
    public GameObject spherePrefab;
    public float sphereRadius = 0.05f;

    [Header("Relaxation Settings")]
    public int initialPoints = 3000;
    public int relaxationSteps = 60;
    public float pushForce = 0.5f;

    [SerializeField, HideInInspector]
    private List<GameObject> spawnedBalls = new List<GameObject>();

    struct SurfacePoint
    {
        public Vector3 pos;
        public Vector3 normal;
    }

    Mesh mesh;
    Vector3[] worldVerts;
    Vector3[] worldNormals;
    int[] tris;

    float[] triAreas;
    float totalArea;

    [Button]
    public void WrapMeshRelaxation()
    {
        ClearBalls();

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        mesh = mf.sharedMesh;
        tris = mesh.triangles;

        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;

        worldVerts = new Vector3[verts.Length];
        worldNormals = new Vector3[normals.Length];

        for (int i = 0; i < verts.Length; i++)
        {
            worldVerts[i] = transform.TransformPoint(verts[i]);
            worldNormals[i] = transform.TransformDirection(normals[i]);
        }

        PrecomputeTriangleAreas();

        List<Vector3> points = GenerateInitialPoints();

        RelaxPoints(points);

        SpawnSpheres(points);

        Debug.Log($"Done! Spawned {spawnedBalls.Count} spheres.");
    }

    // ==============================
    // INITIAL POINT SAMPLING
    // ==============================

    List<Vector3> GenerateInitialPoints()
    {
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < initialPoints; i++)
        {
            int triIndex = GetRandomTriangle();

            Vector3 a = worldVerts[tris[triIndex]];
            Vector3 b = worldVerts[tris[triIndex + 1]];
            Vector3 c = worldVerts[tris[triIndex + 2]];

            float r1 = Random.value;
            float r2 = Random.value;

            if (r1 + r2 > 1f)
            {
                r1 = 1f - r1;
                r2 = 1f - r2;
            }

            Vector3 p = a + r1 * (b - a) + r2 * (c - a);

            points.Add(p);
        }

        return points;
    }

    void PrecomputeTriangleAreas()
    {
        int triCount = tris.Length / 3;
        triAreas = new float[triCount];

        totalArea = 0f;

        for (int i = 0; i < triCount; i++)
        {
            Vector3 a = worldVerts[tris[i * 3]];
            Vector3 b = worldVerts[tris[i * 3 + 1]];
            Vector3 c = worldVerts[tris[i * 3 + 2]];

            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;

            triAreas[i] = area;
            totalArea += area;
        }
    }

    int GetRandomTriangle()
    {
        float r = Random.value * totalArea;
        float accum = 0f;

        for (int i = 0; i < triAreas.Length; i++)
        {
            accum += triAreas[i];

            if (r <= accum)
                return i * 3;
        }

        return 0;
    }

    // ==============================
    // RELAXATION
    // ==============================

    void RelaxPoints(List<Vector3> points)
    {
        float targetDist = sphereRadius * 2f;
        float targetDistSqr = targetDist * targetDist;

        for (int step = 0; step < relaxationSteps; step++)
        {
            Vector3[] forces = new Vector3[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    Vector3 diff = points[i] - points[j];
                    float sqrDist = diff.sqrMagnitude;

                    if (sqrDist < targetDistSqr && sqrDist > 0.00001f)
                    {
                        float dist = Mathf.Sqrt(sqrDist);

                        float strength = (targetDist - dist) / targetDist;

                        Vector3 push = diff.normalized * strength * pushForce;

                        forces[i] += push;
                        forces[j] -= push;
                    }
                }
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 newPos = points[i] + forces[i];

                SurfacePoint surf = ProjectToClosestMeshPoint(newPos);

                points[i] = surf.pos;
            }
        }
    }

    // ==============================
    // SPAWN
    // ==============================

    void SpawnSpheres(List<Vector3> points)
    {
        float targetDist = sphereRadius * 2f;
        float targetDistSqr = targetDist * targetDist;

        List<SurfacePoint> finalPoints = new List<SurfacePoint>();

        foreach (var p in points)
        {
            SurfacePoint surf = ProjectToClosestMeshPoint(p);

            bool tooClose = false;

            foreach (var fp in finalPoints)
            {
                if ((surf.pos - fp.pos).sqrMagnitude < targetDistSqr * 0.9f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            finalPoints.Add(surf);

            Vector3 pos = surf.pos + surf.normal * (sphereRadius * 0.98f);

            Quaternion rot = Quaternion.LookRotation(surf.normal);

            GameObject ball = Instantiate(spherePrefab, pos, rot, transform);

            spawnedBalls.Add(ball);
        }
    }

    // ==============================
    // PROJECT TO MESH
    // ==============================

    SurfacePoint ProjectToClosestMeshPoint(Vector3 pt)
    {
        Vector3 closestPos = Vector3.zero;
        Vector3 closestNormal = Vector3.up;

        float minDist = float.MaxValue;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = worldVerts[tris[i]];
            Vector3 b = worldVerts[tris[i + 1]];
            Vector3 c = worldVerts[tris[i + 2]];

            Vector3 p = ClosestPointOnTriangle(pt, a, b, c);

            float dist = (pt - p).sqrMagnitude;

            if (dist < minDist)
            {
                minDist = dist;

                closestPos = p;

                Vector3 na = worldNormals[tris[i]];
                Vector3 nb = worldNormals[tris[i + 1]];
                Vector3 nc = worldNormals[tris[i + 2]];

                closestNormal = (na + nb + nc).normalized;
            }
        }

        return new SurfacePoint { pos = closestPos, normal = closestNormal };
    }

    // ==============================
    // CLOSEST POINT TRIANGLE
    // ==============================

    Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);

        if (d1 <= 0f && d2 <= 0f) return a;

        Vector3 bp = p - b;

        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);

        if (d3 >= 0f && d4 <= d3) return b;

        float vc = d1 * d4 - d3 * d2;

        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            return a + ab * (d1 / (d1 - d3));

        Vector3 cp = p - c;

        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);

        if (d6 >= 0f && d5 <= d6) return c;

        float vb = d5 * d2 - d1 * d6;

        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            return a + ac * (d2 / (d2 - d6));

        float va = d3 * d6 - d5 * d4;

        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));

        float denom = 1f / (va + vb + vc);

        return a + ab * (vb * denom) + ac * (vc * denom);
    }

    // ==============================
    // CLEAR
    // ==============================

    [Button]
    public void ClearBalls()
    {
        foreach (var ball in spawnedBalls)
            if (ball != null)
                DestroyImmediate(ball);

        spawnedBalls.Clear();
    }
}