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

        LMotion.Create(Vector3.one, Vector3.one * .8f, 1f)
            .WithEase(Ease.Linear)
            .WithDelay(1)
            .BindToLocalScale(transform).AddTo(this);
        // THÊM DÒNG NÀY: Bắt đầu theo dõi độ cao ngầm
        MonitorHeightAndRemovePhysics().Forget();
    }

    // Tác vụ chạy ngầm kiểm tra độ cao mỗi frame

    private async UniTaskVoid MonitorHeightAndRemovePhysics()
    {
        while (this != null && gameObject != null)
        {
            if (GameController.Instance != null && GameController.Instance.disablePhysicsTransform != null)
            {
                if (transform.position.y < GameController.Instance.disablePhysicsTransform.position.y)
                {
                    // 1. Xóa vật lý
                    Rigidbody rb = GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);

                    Collider col = GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    // 2. Bắt đầu hành trình trượt trong ống
                    MoveAlongTransformListTask().Forget();

                    break; // Dừng vòng lặp check độ cao
                }
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
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