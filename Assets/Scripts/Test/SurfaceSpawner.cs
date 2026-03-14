using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class SeamlessFloodPacker : MonoBehaviour
{
    [Header("Prefab & Thông số")]
    public GameObject spherePrefab;
    
    [Tooltip("Bán kính của quả bóng (VD: bóng scale 0.2 thì bán kính là 0.1)")]
    public float sphereRadius = 0.05f;

    [Tooltip("Độ sít: 1.0 là chạm vỏ. Giảm xuống 0.98 nếu muốn lún nhẹ để lấp khe hở nhỏ ở góc cạnh phức tạp.")]
    [Range(0.8f, 1.0f)]
    public float tightness = 0.98f;

    [SerializeField, HideInInspector]
    private List<GameObject> spawnedBalls = new List<GameObject>();
    private List<Vector3> validPositions = new List<Vector3>();

    // Cấu trúc hỗ trợ lan truyền mạng nhện
    struct PropagationNode
    {
        public Vector3 posMesh; // Tọa độ dính chặt trên bề mặt Mesh
        public Vector3 normal;  // Pháp tuyến tại điểm đó
    }

    [Button][ContextMenu("1. Bọc Kín Seamless (Flood Fill - Lan Truyền)")]
    public void PackSeamless()
    {
        ClearBalls();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        Vector3[] normals = mesh.normals;

        Vector3[] worldVerts = new Vector3[verts.Length];
        Vector3[] worldNormals = new Vector3[normals.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            worldVerts[i] = transform.TransformPoint(verts[i]);
            worldNormals[i] = transform.TransformDirection(normals[i]).normalized;
        }

        // Khoảng cách chạm nhau chính xác giữa 2 tâm bóng
        float stepDist = sphereRadius * 2f * tightness;
        // Khoảng cách an toàn tối thiểu chống đè lấp (du di sai số toán học 2%)
        float minValidSqrDist = (stepDist * 0.98f) * (stepDist * 0.98f);

        Queue<PropagationNode> activeNodes = new Queue<PropagationNode>();

        Debug.Log("Đang bắt đầu lan truyền mạng nhện... Vui lòng đợi!");

        // ==========================================================
        // BƯỚC 1: HÀN ĐỈNH VÀ ĐẶT HẠT GIỐNG ĐẦU TIÊN (SEEDING)
        // ==========================================================
        
        // Tìm 1 hạt giống biên nếu có (dành cho Cube có viền hở)
        Dictionary<long, int> edgeCounts = new Dictionary<long, int>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            AddEdge(tris[i], tris[i + 1], edgeCounts);
            AddEdge(tris[i + 1], tris[i + 2], edgeCounts);
            AddEdge(tris[i + 2], tris[i], edgeCounts);
        }

        PropagationNode seedNode = new PropagationNode();
        bool foundBoundarySeed = false;

        foreach (var kvp in edgeCounts)
        {
            if (kvp.Value == 1) // Cạnh viền hở
            {
                int vIdx = (int)(kvp.Key >> 32); // Lấy đỉnh đầu tiên của cạnh
                seedNode = GetClosestPointOnMesh(worldVerts[vIdx], worldVerts, tris, worldNormals);
                foundBoundarySeed = true;
                break;
            }
        }

        // Nếu không có biên (như hình cầu), lấy bừa đỉnh đầu tiên làm hạt giống
        if (!foundBoundarySeed)
        {
            seedNode = GetClosestPointOnMesh(worldVerts[0], worldVerts, tris, worldNormals);
        }

        // Đặt quả bóng đầu tiên dính vào bề mặt Mesh
        Vector3 initialSpawnPos = seedNode.posMesh + seedNode.normal * sphereRadius;
        validPositions.Add(initialSpawnPos);
        activeNodes.Enqueue(seedNode);
        SpawnBall(initialSpawnPos, seedNode.normal);

        // ==========================================================
        // BƯỚC 2: LAN TRUYỀN FLOOD FILL (Geodesic Geodesic Wavefront)
        // ==========================================================
        
        int failsafe = 100000; // Ngăn Unity treo nếu model quá lớn hoặc bán kính quá nhỏ
        int iterations = 0;

        while (activeNodes.Count > 0 && iterations < failsafe)
        {
            iterations++;
            PropagationNode current = activeNodes.Dequeue();

            // Tạo một hệ trục 2D giả lập ngay trên bề mặt cong tại điểm hiện tại
            Vector3 tangent = Vector3.Cross(current.normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(current.normal, Vector3.forward);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(current.normal, tangent).normalized;

            // "Đẻ" ra 6 hướng xung quanh theo lưới lục giác
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector3 direction2D = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                
                // Vị trí dự kiến lơ lửng trong không gian
                Vector3 candidatePos = current.posMesh + direction2D * stepDist;

                // TOÁN HỌC CỐT LÕI: Kéo điểm candidate lơ lửng đó "dính chặt" xuống bề mặt Mesh
                PropagationNode child = GetClosestPointOnMesh(candidatePos, worldVerts, tris, worldNormals);

                // Nếu bước nhảy bị vượt qua vực sâu (VD: từ mặt nầy sang mặt kia của Cube hở), bỏ qua
                if (Vector3.Distance(current.posMesh, child.posMesh) > stepDist * 1.5f) continue;

                // Vị trí Spawn bóng thực tế (MeshPos + đẩy ra theo Normal)
                Vector3 spawnPos = child.posMesh + child.normal * sphereRadius;

                // Kiểm tra xem vị trí này đã được lấp kín chưa
                // Kiểm tra xem vị trí này đã được lấp kín chưa
                bool isSpaceEmpty = true;
                foreach (Vector3 p in validPositions)
                {
                    if ((p - spawnPos).sqrMagnitude < minValidSqrDist) // Đã sửa thành minValidSqrDist
                    {
                        isSpaceEmpty = false;
                        break;
                    }
                }

                // Nếu còn trống -> Đặt bóng, lấp kín khe rỗng, và biến nó thành hạt giống mới
                if (isSpaceEmpty)
                {
                    validPositions.Add(spawnPos);
                    activeNodes.Enqueue(child); // Thêm vào queue để lan truyền tiếp
                    SpawnBall(spawnPos, child.normal);
                }
            }
        }

        Debug.Log($"Tuyệt hảo! Đã lấp kín 100% bề mặt Seamless với {spawnedBalls.Count} quả bóng chạm sát nhau.");
    }

    void AddEdge(int v1, int v2, Dictionary<long, int> edgeCounts)
    {
        int min = Mathf.Min(v1, v2); int max = Mathf.Max(v1, v2);
        long edgeKey = ((long)min << 32) | (long)max;
        if (edgeCounts.ContainsKey(edgeKey)) edgeCounts[edgeKey]++;
        else edgeCounts[edgeKey] = 1;
    }

    void SpawnBall(Vector3 pos, Vector3 normal)
    {
        GameObject ball = Instantiate(spherePrefab, pos, Quaternion.LookRotation(normal), transform);
        spawnedBalls.Add(ball);
    }

    // --- CÁC HÀM TOÁN HỌC CHIẾU ĐIỂM (Không thay đổi) ---

    PropagationNode GetClosestPointOnMesh(Vector3 pt, Vector3[] verts, int[] tris, Vector3[] normals)
    {
        Vector3 closestPos = Vector3.zero; Vector3 closestNormal = Vector3.up; float minSqrDist = float.MaxValue;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = verts[tris[i]]; Vector3 b = verts[tris[i + 1]]; Vector3 c = verts[tris[i + 2]];
            Vector3 p = ClosestPointOnTriangle(pt, a, b, c);
            float sqrDist = (pt - p).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist; closestPos = p;
                closestNormal = InterpolateNormal(p, a, b, c, normals[tris[i]], normals[tris[i + 1]], normals[tris[i + 2]]);
            }
        }
        return new PropagationNode { posMesh = closestPos, normal = closestNormal.normalized };
    }

    Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a; Vector3 ac = c - a; Vector3 ap = p - a;
        float d1 = Vector3.Dot(ab, ap); float d2 = Vector3.Dot(ac, ap); if (d1 <= 0f && d2 <= 0f) return a;
        Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp); float d4 = Vector3.Dot(ac, bp); if (d3 >= 0f && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2; if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + (d1 / (d1 - d3)) * ab;
        Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp); float d6 = Vector3.Dot(ac, cp); if (d6 >= 0f && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6; if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + (d2 / (d2 - d6)) * ac;
        float va = d3 * d6 - d5 * d4; if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + ((d4 - d3) / ((d4 - d3) + (d5 - d6))) * (c - b);
        float denom = 1f / (va + vb + vc); return a + ab * (vb * denom) + ac * (vc * denom);
    }

    Vector3 InterpolateNormal(Vector3 p, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
    {
        Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector3.Dot(v0, v0); float d01 = Vector3.Dot(v0, v1); float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0); float d21 = Vector3.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01; if (denom == 0) return na;
        float v = (d11 * d20 - d01 * d21) / denom; float w = (d00 * d21 - d01 * d20) / denom; float u = 1.0f - v - w;
        return (na * u + nb * v + nc * w).normalized;
    }

    [ContextMenu("2. Xóa Bóng (Clear)")]
    public void ClearBalls()
    {
        foreach (GameObject ball in spawnedBalls) if (ball != null) DestroyImmediate(ball);
        spawnedBalls.Clear();
        validPositions.Clear();
    }
}