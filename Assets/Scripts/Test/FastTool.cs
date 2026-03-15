using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class FastTool : MonoBehaviour
{
    [Button]
    public void AddSphereCollidersToVisibleChildren()
    {
        // Lấy object cha đang chọn trong Hierarchy
        var parent = transform;

        if (parent == null)
        {
            Debug.LogWarning("[FastTool] Bạn phải chọn một Object cha trước!");
            return;
        }

        // Tìm tất cả MeshRenderer của các con
        MeshRenderer[] meshRenderers = parent.GetComponentsInChildren<MeshRenderer>(true);
        int count = 0;

        foreach (MeshRenderer mr in meshRenderers)
        {
            GameObject target = mr.gameObject;

            // Kiểm tra xem đã có SphereCollider chưa
            if (target.GetComponent<SphereCollider>() == null)
            {
                SphereCollider sc = Undo.AddComponent<SphereCollider>(target);
                
                // Tự động tính Radius khít theo Mesh
                // Dùng localBounds để không bị ảnh hưởng bởi phép xoay của object
                float maxBound = Mathf.Max(
                    mr.localBounds.extents.x, 
                    mr.localBounds.extents.y, 
                    mr.localBounds.extents.z
                );
                
                sc.radius = maxBound;
                count++;
            }
        }

        Debug.Log($"[FastTool] Hoàn tất! Đã thêm {count} Sphere Colliders vào các object có MeshRenderer.");
    }

    [Button]
    public  void PrepareBalls()
    {
        
        MeshRenderer[] renderers = transform.GetComponentsInChildren<MeshRenderer>(true);
    
        foreach (var mr in renderers)
        {
            mr.gameObject.layer = 3;
            if (mr.gameObject.GetComponent<Ball>() == null)
                mr.gameObject.AddComponent<Ball>();
            
            if (mr.gameObject.GetComponent<SphereCollider>() == null)
                mr.gameObject.AddComponent<SphereCollider>();
        }
        Debug.Log("Đã gán Script Ball và Collider thành công!");
    }

    [Button]
    public void PrepareGrandChildrenBallsOnly()
    {
        var grandChildren = GetGrandChildrenOnly();
        int processed = 0;

        foreach (var go in grandChildren)
        {
            if (go == null) continue;

            go.layer = 3;

            if (go.GetComponent<Ball>() == null)
                go.AddComponent<Ball>();

            if (go.GetComponent<SphereCollider>() == null)
                go.AddComponent<SphereCollider>();

            processed++;
        }

        PackBalls pack = GetComponent<PackBalls>();
        if (pack != null)
        {
            pack.balls.Clear();
            pack.balls.AddRange(grandChildren);
        }

        Debug.Log($"[FastTool] GrandChildren only: đã xử lý {processed} object (con của con)." +
                  (pack != null ? $" PackBalls.balls = {pack.balls.Count}." : ""));
    }

    private List<GameObject> GetGrandChildrenOnly()
    {
        List<GameObject> result = new List<GameObject>();

        foreach (Transform child in transform)
        {
            foreach (Transform grandChild in child)
            {
                result.Add(grandChild.gameObject);
            }
        }

        return result;
    }


}