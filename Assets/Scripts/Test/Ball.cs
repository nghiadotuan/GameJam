using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Splines;

public class Ball : MonoBehaviour
{
    private bool _isExploded = false;


    public void ExplodeSimple(Vector3 force)
    {
        if (_isExploded) return;
        gameObject.layer = LayerMask.NameToLayer("BallFall");
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
        // THÊM DÒNG NÀY: Bắt đầu theo dõi độ cao ngầm
        MonitorHeightAndRemovePhysics().Forget();
    }

    // Tác vụ chạy ngầm kiểm tra độ cao mỗi frame

    private async UniTaskVoid MonitorHeightAndRemovePhysics()
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        while (this != null && gameObject != null)
        {
            if (GameController.Instance != null && GameController.Instance.disablePhysicsTransform != null)
            {
                Transform target = GameController.Instance.disablePhysicsTransform;

                // ==========================================
                // 1. TẠO LỰC HÚT VẬT LÝ NHẸ NHÀNG VỀ TÂM LỖ
                // ==========================================
                if (rb != null)
                {
                    // Chỉ tính toán hướng hút trên mặt phẳng ngang (X và Z), bỏ qua Y để bóng rơi tự nhiên
                    Vector3 targetPos = new Vector3(target.position.x, transform.position.y, target.position.z);

                    // Khoảng cách ngang từ bóng đến tâm lỗ
                    float horizontalDistance = Vector3.Distance(transform.position, targetPos);

                    // Chỉ bắt đầu hút khi bóng rơi vào gần khu vực phễu (ví dụ cách tâm lỗ < 2 đơn vị)
                    // và độ cao Y của bóng đang gần bằng miệng phễu (tránh hút bóng khi nó đang bay tít trên cao)
                    if (horizontalDistance > 0.05f && transform.position.y < target.position.y + 3f)
                    {
                        Vector3 pullDirection = (targetPos - transform.position).normalized;

                        // Chỉnh lực này nhỏ thôi (ví dụ 1f - 3f) để nó lăn từ từ tạo cảm giác tự nhiên
                        float pullForce = 2f;

                        // Dùng ForceMode.Force để duy trì gia tốc nhẹ nhàng
                        rb.AddForce(pullDirection * pullForce);
                    }
                }

                // ==========================================
                // 2. LOGIC KIỂM TRA ĐỂ CHUI VÀO ỐNG
                // ==========================================
                float deltaX = Mathf.Abs(transform.position.x - target.position.x);

                // Nếu lọt xuống dưới Y và X nằm ngay sát tâm lỗ (khe hẹp 0.1f)
                if (transform.position.y < target.position.y+.1 && deltaX < .68f)
                {
                    // Xóa vật lý
                    if (rb != null) Destroy(rb);

                    Collider col = GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    // Bắt đầu hành trình trượt trong ống
                    MoveAlongTransformListTask().Forget();

                    // Thu nhỏ bóng
                    LMotion.Create(Vector3.one, Vector3.one * 0.5f, 1f)
                        .WithEase(Ease.Linear)
                        .WithDelay(1)
                        .BindToLocalScale(transform).AddTo(GameController.Instance);

                    break; // Dừng vòng lặp check độ cao
                }
            }

            // ĐÃ SỬA: Chuyển sang FixedUpdate vì có dùng AddForce để vật lý chạy chuẩn xác
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
        }
    }

    private async UniTaskVoid MoveAlongTransformListTask()
    {
        // Kiểm tra danh sách Node có hợp lệ không
        if (GameController.Instance == null || GameController.Instance.pipeNodes == null || GameController.Instance.pipeNodes.Count == 0)
        {
            Debug.LogWarning("Chưa gán List Transform đường đi cho GameController!");
            return;
        }

        List<Transform> nodes = GameController.Instance.pipeNodes;
        float speed = GameConfig.Instance.pipeMoveSpeed;
        int currentNodeIndex = 0;

        // Lặp qua từng Transform trong danh sách
        while (currentNodeIndex < nodes.Count && this != null && gameObject != null)
        {
            Transform targetNode = nodes[currentNodeIndex];

            // Đề phòng trường hợp một Node vô tình bị xóa trên Scene
            if (targetNode == null)
            {
                currentNodeIndex++;
                continue;
            }

            // Lấy trực tiếp tọa độ World của Transform
            Vector3 targetWorldPos = targetNode.position;

            // Di chuyển quả bóng đến vị trí đó
            while (this != null && gameObject != null && Vector3.Distance(transform.position, targetWorldPos) > 0.001f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetWorldPos,
                    speed * Time.deltaTime
                );

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // Đã đến đích của Node hiện tại, tăng index để đi tới Node tiếp theo
            currentNodeIndex++;
        }
    }
}