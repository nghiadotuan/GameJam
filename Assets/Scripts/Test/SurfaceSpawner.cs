using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class PerfectEdgeFloodPacker : MonoBehaviour
{
    [Header("Prefab & Thông số")]
    public GameObject spherePrefab;
    
    [Tooltip("Bán kính của quả bóng (VD: bóng scale 0.2 thì bán kính là 0.1)")]
    public float sphereRadius = 0.05f;

    [Tooltip("Độ sít: 1.0 là chạm vỏ. 0.98 là lún nhẹ vào nhau 2% để lấp khe hở nhỏ ở góc cạnh phức tạp.")]
    [Range(0.8f, 1.0f)]
    public float tightness = 0.98f;

    [Tooltip("Góc gập để nhận diện là 'Cạnh Viền'. (Cube là 90 độ, để 30 là an toàn bắt được hết)")]
    public float sharpEdgeAngle = 30f;

    [SerializeField, HideInInspector]
    private List<GameObject> spawnedBalls = new List<GameObject>();
    private List<Vector3> validPositions = new List<Vector3>();

    struct PropagationNode
    {
        public Vector3 posMesh; // Tọa độ dính chặt trên bề mặt Mesh
        public Vector3 normal;  // Pháp tuyến tại điểm đó
    }

    [Button][ContextMenu("1. Bọc Kín Khung & Ruột (Sharp Edge + Flood)")]
    public void PackPerfectly()
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

        float stepDist = sphereRadius * 2f * tightness;
        float minValidSqrDist = (stepDist * 0.98f) * (stepDist * 0.98f);

        Queue<PropagationNode> activeNodes = new Queue<PropagationNode>();

        Debug.Log("Đang đóng khung các cạnh sắc nhọn và lan truyền... Vui lòng đợi!");

        // ==========================================================
        // BƯỚC 1: HÀN ĐỈNH VÀ TÌM CẠNH SẮC NHỌN (SHARP EDGES)
        // ==========================================================
        Dictionary<Vector3, int> uniqueVertsMap = new Dictionary<Vector3, int>();
        List<Vector3> uniqueVerts = new List<Vector3>();
        int[] remappedTris = new int[tris.Length];

        // Hàn đỉnh chống đứt gãy
        for (int i = 0; i < worldVerts.Length; i++)
        {
            Vector3 rounded = new Vector3(
                Mathf.Round(worldVerts[i].x * 1000f) / 1000f,
                Mathf.Round(worldVerts[i].y * 1000f) / 1000f,
                Mathf.Round(worldVerts[i].z * 1000f) / 1000f);

            if (!uniqueVertsMap.ContainsKey(rounded))
            {
                uniqueVertsMap[rounded] = uniqueVerts.Count;
                uniqueVerts.Add(worldVerts[i]);
            }
            remappedTris[i] = uniqueVertsMap[rounded];
        }

        // Gom các tam giác theo cạnh
        Dictionary<long, List<int>> edgeToTris = new Dictionary<long, List<int>>();
        for (int i = 0; i < remappedTris.Length; i += 3)
        {
            AddEdge(remappedTris[i], remappedTris[i + 1], i / 3, edgeToTris);
            AddEdge(remappedTris[i + 1], remappedTris[i + 2], i / 3, edgeToTris);
            AddEdge(remappedTris[i + 2], remappedTris[i], i / 3, edgeToTris);
        }

        // ==========================================================
        // BƯỚC 2: RẢI BÓNG BO KÍN TẤT CẢ CÁC CẠNH (DỰNG KHUNG)
        // ==========================================================
        foreach (var kvp in edgeToTris)
        {
            bool isSharpEdge = false;

            if (kvp.Value.Count == 1) // Cạnh hở viền ngoài
            {
                isSharpEdge = true;
            }
            else if (kvp.Value.Count >= 2) // Cạnh giao giữa 2 mặt
            {
                int tri1 = kvp.Value[0] * 3;
                int tri2 = kvp.Value[1] * 3;
                Vector3 n1 = Vector3.Cross(worldVerts[tris[tri1 + 1]] - worldVerts[tris[tri1]], worldVerts[tris[tri1 + 2]] - worldVerts[tris[tri1]]).normalized;
                Vector3 n2 = Vector3.Cross(worldVerts[tris[tri2 + 1]] - worldVerts[tris[tri2]], worldVerts[tris[tri2 + 2]] - worldVerts[tris[tri2]]).normalized;
                
                // Nếu góc gập giữa 2 mặt lớn hơn ngưỡng (VD: Cube là 90 độ > 30 độ)
                if (Vector3.Angle(n1, n2) >= sharpEdgeAngle) isSharpEdge = true;
            }

            if (isSharpEdge)
            {
                int v1Idx = (int)(kvp.Key >> 32);
                int v2Idx = (int)(kvp.Key & 0xFFFFFFFF);
                Vector3 start = uniqueVerts[v1Idx];
                Vector3 end = uniqueVerts[v2Idx];

                float len = Vector3.Distance(start, end);
                int steps = Mathf.Max(1, Mathf.RoundToInt(len / stepDist));

                // Rải bóng thẳng hàng trên cạnh này
                for (int i = 0; i <= steps; i++)
                {
                    Vector3 pt = Vector3.Lerp(start, end, (float)i / steps);
                    PropagationNode node = GetClosestPointOnMesh(pt, worldVerts, tris, worldNormals);

                    Vector3 spawnPos = node.posMesh + node.normal * sphereRadius;

                    bool isSpaceEmpty = true;
                    foreach (Vector3 p in validPositions)
                    {
                        if ((p - spawnPos).sqrMagnitude < minValidSqrDist)
                        {
                            isSpaceEmpty = false; break;
                        }
                    }

                    if (isSpaceEmpty)
                    {
                        validPositions.Add(spawnPos);
                        activeNodes.Enqueue(node); // Cho bóng ở cạnh làm hạt giống để bò vào ruột
                        SpawnBall(spawnPos, node.normal);
                    }
                }
            }
        }

        // Nếu model là mặt cầu tròn xoe (không có góc cạnh sắc), lấy bừa đỉnh đầu tiên làm hạt giống
        if (activeNodes.Count == 0)
        {
            PropagationNode fallbackNode = GetClosestPointOnMesh(worldVerts[0], worldVerts, tris, worldNormals);
            Vector3 spawnPos = fallbackNode.posMesh + fallbackNode.normal * sphereRadius;
            validPositions.Add(spawnPos);
            activeNodes.Enqueue(fallbackNode);
            SpawnBall(spawnPos, fallbackNode.normal);
        }

        // ==========================================================
        // BƯỚC 3: LAN TRUYỀN TỪ CẠNH VÀO LẤP KÍN RUỘT (FLOOD FILL)
        // ==========================================================
        int failsafe = 150000; 
        int iterations = 0;

        while (activeNodes.Count > 0 && iterations < failsafe)
        {
            iterations++;
            PropagationNode current = activeNodes.Dequeue();

            Vector3 tangent = Vector3.Cross(current.normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(current.normal, Vector3.forward);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(current.normal, tangent).normalized;

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector3 direction2D = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                
                Vector3 candidatePos = current.posMesh + direction2D * stepDist;
                PropagationNode child = GetClosestPointOnMesh(candidatePos, worldVerts, tris, worldNormals);

                // Nếu nhảy xa quá (qua vực sâu) thì bỏ qua
                if (Vector3.Distance(current.posMesh, child.posMesh) > stepDist * 1.5f) continue;

                Vector3 spawnPos = child.posMesh + child.normal * sphereRadius;

                bool isSpaceEmpty = true;
                foreach (Vector3 p in validPositions)
                {
                    if ((p - spawnPos).sqrMagnitude < minValidSqrDist)
                    {
                        isSpaceEmpty = false; break;
                    }
                }

                if (isSpaceEmpty)
                {
                    validPositions.Add(spawnPos);
                    activeNodes.Enqueue(child); 
                    SpawnBall(spawnPos, child.normal);
                }
            }
        }

        Debug.Log($"Hoàn mỹ! Đã chốt kín mọi góc cạnh và lấp đầy mặt phẳng với {spawnedBalls.Count} quả bóng.");
    }

    void AddEdge(int v1, int v2, int triIndex, Dictionary<long, List<int>> edgeDict)
    {
        int min = Mathf.Min(v1, v2); int max = Mathf.Max(v1, v2);
        long edgeKey = ((long)min << 32) | (long)max;
        if (!edgeDict.ContainsKey(edgeKey)) edgeDict[edgeKey] = new List<int>();
        edgeDict[edgeKey].Add(triIndex);
    }

    void SpawnBall(Vector3 pos, Vector3 normal)
    {
        GameObject ball = Instantiate(spherePrefab, pos, Quaternion.LookRotation(normal), transform);
        ball.SetActive(true); // Đảm bảo Active dù Prefab đang tắt
        spawnedBalls.Add(ball);
    }

    // --- CÁC HÀM TOÁN HỌC CHIẾU ĐIỂM LƯỚI (Không thay đổi) ---

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