using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum ColorEnum
{
    None,
    Red,
    Blue,
    Green,
    Yellow,
    Purple,
    Orange,
    Pink
}

public class PackBalls : MonoBehaviour
{
    public ColorEnum colorIndex = ColorEnum.None;
    
    [Header("Data Management")]
    [Tooltip("Danh sách chứa toàn bộ các quả bóng con")]
    public List<GameObject> balls = new List<GameObject>();

    /// <summary>
    /// Tìm tất cả con có MeshRenderer, add SphereCollider và cập nhật vào list balls.
    /// Bạn có thể gọi hàm này từ Editor hoặc khi Runtime.
    /// </summary>
    [Button][ContextMenu("Auto Add Colliders & Sync List")]
    public void AddCollidersAndSyncList()
    {
        // Làm sạch list cũ trước khi đồng bộ lại
        balls.Clear();

        // Lấy tất cả MeshRenderer của các object con
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer mr in meshRenderers)
        {
            GameObject target = mr.gameObject;

            // 1. Thêm vào list quản lý
            if (!balls.Contains(target))
            {
                balls.Add(target);
            }

            // 2. Kiểm tra và add SphereCollider nếu chưa có
            SphereCollider sc = target.GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = target.AddComponent<SphereCollider>();
            }

            // 3. Tự động tính bán kính dựa trên Mesh để bọc khít
            // Extents là một nửa kích thước của Bounds
            float maxExtents = Mathf.Max(
                mr.localBounds.extents.x, 
                mr.localBounds.extents.y, 
                mr.localBounds.extents.z
            );
            
            sc.radius = maxExtents;

            // Đảm bảo object đã được active
            target.SetActive(true);
        }

        Debug.Log($"<color=cyan>[PackBalls]</color> Đã đồng bộ {balls.Count} quả bóng vào danh sách và gán Collider.");
    }

    /// <summary>
    /// Hàm tiện ích để xóa toàn bộ bóng con (dùng khi muốn generate lại)
    /// </summary>
    [ContextMenu("Clear All Balls")]
    public void ClearAllBalls()
    {
        // Xóa object thực tế
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        
        // Xóa list
        balls.Clear();
        Debug.Log("<color=red>[PackBalls]</color> Đã xóa sạch toàn bộ bóng.");
    }
}