using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Splines;

public class NarrowMeshToSpline : MonoBehaviour
{
    [Tooltip("Kéo object chứa Mesh Filter của cái curve hẹp vào đây")]
    public MeshFilter sourceMesh;

    [Tooltip("Khoảng cách gộp điểm. Nếu mesh rất hẹp, các đỉnh sát nhau (< 0.05m) sẽ bị gộp làm 1 tâm duy nhất.")]
    public float mergeDistance = 0.05f;

    [Button]
    [ContextMenu("Trích xuất điểm -> Tạo Spline")]
    public void ExtractAndCreateSpline()
    {
        if (sourceMesh == null || sourceMesh.sharedMesh == null)
        {
            Debug.LogError("Chưa gán Mesh hoặc Mesh bị trống!");
            return;
        }

        Vector3[] rawVertices = sourceMesh.sharedMesh.vertices;
        if (rawVertices.Length == 0) return;

        // BƯỚC 1: LỌC ĐIỂM TRÙNG (Gộp các điểm của mesh hẹp thành 1 đường đơn)
        List<Vector3> uniqueVertices = new List<Vector3>();
        foreach (Vector3 v in rawVertices)
        {
            // Đưa tọa độ đỉnh về không gian thế giới (World Space)
            Vector3 worldPos = sourceMesh.transform.TransformPoint(v);

            bool isDuplicate = false;
            foreach (Vector3 uv in uniqueVertices)
            {
                if (Vector3.Distance(worldPos, uv) < mergeDistance)
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate) uniqueVertices.Add(worldPos);
        }

        if (uniqueVertices.Count < 2)
        {
            Debug.LogError("Sau khi gộp, không đủ điểm để tạo đường (hãy giảm Merge Distance xuống)!");
            return;
        }

        // BƯỚC 2: SẮP XẾP THỨ TỰ TỪ ĐẦU ĐẾN CUỐI (Nearest Neighbor)
        // Tìm 1 điểm ở ngoài cùng để làm điểm xuất phát
        Vector3 startPoint = uniqueVertices[0];
        float maxDistFromZero = 0;
        foreach (Vector3 v in uniqueVertices)
        {
            float d = Vector3.Distance(uniqueVertices[0], v);
            if (d > maxDistFromZero)
            {
                maxDistFromZero = d;
                startPoint = v; // Điểm xa nhất chắc chắn là 1 trong 2 đầu mút của ống
            }
        }

        List<Vector3> sortedVertices = new List<Vector3>();
        sortedVertices.Add(startPoint);
        uniqueVertices.Remove(startPoint);

        Vector3 currentPoint = startPoint;

        // Cứ đứng ở điểm hiện tại, tìm điểm nào gần nhất thì nối vào
        while (uniqueVertices.Count > 0)
        {
            float closestDist = float.MaxValue;
            int closestIndex = -1;

            for (int i = 0; i < uniqueVertices.Count; i++)
            {
                float d = Vector3.Distance(currentPoint, uniqueVertices[i]);
                if (d < closestDist)
                {
                    closestDist = d;
                    closestIndex = i;
                }
            }

            currentPoint = uniqueVertices[closestIndex];
            sortedVertices.Add(currentPoint);
            uniqueVertices.RemoveAt(closestIndex);
        }

        // BƯỚC 3: VẼ RA SPLINE
        SplineContainer container = GetComponent<SplineContainer>();
        if (container == null) container = gameObject.AddComponent<SplineContainer>();

        Spline newSpline = new Spline();
        foreach (Vector3 pos in sortedVertices)
        {
            // Chuyển lại về tọa độ Local của object chứa Spline
            Vector3 localPos = transform.InverseTransformPoint(pos);
            newSpline.Add(new BezierKnot(localPos));
        }

        container.Spline = newSpline;
        Debug.Log($"Thành công! Đã chắt lọc {rawVertices.Length} đỉnh thô thành {sortedVertices.Count} điểm chuẩn trên Spline.");
    }

    [Header("Debug Nodes")] [Tooltip("Thư mục cha chứa các cục Node sinh ra. Nếu để trống code sẽ tự tạo.")]
    public Transform debugContainer;

    [Tooltip("Prefab để hiển thị Node. Nếu để trống, code sẽ tự tạo ra các khối Cầu (Sphere) nhỏ màu trắng.")]
    public GameObject nodePrefab;

    [Button][ContextMenu("Test: Sinh GameObject tại các Node")]
    public void SpawnGameObjectsAtNodes()
    {
        SplineContainer splineContainer = GetComponent<SplineContainer>();
        if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count == 0)
        {
            Debug.LogWarning("Chưa có đường Spline nào hoặc Spline đang trống rỗng! Hãy chạy hàm Tạo Spline trước.");
            return;
        }

        // 1. Tạo Container nếu chưa có
        if (debugContainer == null)
        {
            GameObject containerObj = new GameObject("Debug_SplineNodes");
            containerObj.transform.SetParent(this.transform); // Đặt làm con của object hiện tại
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;
            debugContainer = containerObj.transform;
        }

        // 2. XÓA CÁC ĐIỂM CŨ (Phải lặp ngược từ dưới lên trên khi xóa child trong Editor)
        for (int i = debugContainer.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(debugContainer.GetChild(i).gameObject);
        }

        // 3. SINH RA CÁC ĐIỂM MỚI
        Spline spline = splineContainer.Spline;
        for (int i = 0; i < spline.Count; i++)
        {
            // Lấy dữ liệu của từng điểm (Knot)
            BezierKnot knot = spline[i];

            GameObject nodeObj;
            if (nodePrefab != null)
            {
                nodeObj = Instantiate(nodePrefab);
            }
            else
            {
                // Tự động tạo khối cầu nhỏ nếu bạn lười kéo Prefab
                nodeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nodeObj.transform.localScale = Vector3.one * 0.05f; // Thu nhỏ xíu lại cho dễ nhìn

                // Xóa Collider đi để lúc bạn test game nó không cản đường quả bóng lăn
                DestroyImmediate(nodeObj.GetComponent<Collider>());
            }

            nodeObj.name = $"Node_{i:000}";
            nodeObj.transform.SetParent(debugContainer);

            // Knot.Position là tọa độ Local (tương đối so với cục SplineContainer)
            nodeObj.transform.localPosition = knot.Position;
        }

        Debug.Log($"Đã sinh ra {spline.Count} GameObjects tại các điểm Node để bạn kiểm tra!");
    }
}