using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public class Ball : MonoBehaviour
{
    private bool _isExploded = false;

        public void ExplodeSimple(Vector3 force)
    {
        if (_isExploded) return;
        _isExploded = true;

        transform.SetParent(null, true);

        // 1. Chút nhiễu tọa độ để dãn Collider
        transform.position += UnityEngine.Random.insideUnitSphere * 0.005f; // Chú ý: Vì bi có 0.02, offset chỉ nên rất nhỏ 0.005

        // Bật Rigidbody
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        
        // 2. Chỉnh Mass và Drag (QUAN TRỌNG)
        // Vì vật quá nhỏ, tăng nhẹ Mass để giảm sốc vật lý
        rb.mass = 5f; 
        
        // Cản không khí cực cao để tạo cảm giác rụng "đầm" (tựa con xúc xắc lăn dưới mặt bàn)
        rb.linearDamping = 15f; 
        rb.angularDamping = 10f; // Hãm lực xoay 
        
        // Hạn chế tuyệt đối lực nẩy Depenetration của Unity
        rb.maxDepenetrationVelocity = 0.05f; // Rất nhỏ để hợp với size 0.02
        
        // Tuỳ ý: Bổ sung 1 lực hút tĩnh xuống lòng đất (Bởi vì ta đã khoá bằng Damping 15f ở trên)
        // rb.AddForce(Vector3.down * 40f, ForceMode.Acceleration);
    }


}