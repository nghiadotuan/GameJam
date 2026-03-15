using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;

public class Ball : MonoBehaviour
{
    private bool _isExploded = false;
    private static float _nextShootTime = 0f;
    private static float _nextEnterPipeTime = 0f;
    private SmallShove _assignedShove;
    private ShoveMovement _targetShoveAfterPipe;
    private bool _hasPipeReservation;

    public void ExplodeSimple(Vector3 force, ShoveMovement targetShove = null, bool hasPipeReservation = false)
    {
        if (_isExploded) return;
        _isExploded = true;
        _assignedShove = null;
        _targetShoveAfterPipe = targetShove;
        _hasPipeReservation = hasPipeReservation;

        transform.SetParent(null, true);

        // 1. Chút nhiễu tọa độ để dãn Collider
        transform.position += UnityEngine.Random.insideUnitSphere * 0.005f;

        // Bật Rigidbody
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = 1f;
        rb.linearDamping = 3f; // Tăng nhẹ cản không khí để bóng bay lơ lửng hơn
        rb.angularDamping = 5f;
        rb.maxDepenetrationVelocity = 0.1f;

        // CHÌA KHÓA: Tắt trọng lực mặc định của Trái Đất đi!
        rb.useGravity = false;

        // Ép lực văng nổ
        rb.AddForce(force, ForceMode.Impulse);

        MonitorHeightAndRemovePhysics().Forget();
        gameObject.layer = LayerMask.NameToLayer("BallFall");
    }

    private async UniTaskVoid MonitorHeightAndRemovePhysics()
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        while (this != null && gameObject != null)
        {
            if (GameController.Instance != null && GameController.Instance.disablePhysicsTransform != null)
            {
                Transform target = GameController.Instance.disablePhysicsTransform;

                // ==========================================
                // 1. TRỌNG LỰC NHÂN TẠO & LỰC HÚT BÙ TRỪ
                // ==========================================
                if (rb != null)
                {
                    // A. Trọng lực nhân tạo (rơi chậm bồng bềnh)
                    rb.AddForce(Vector3.down * 3f, ForceMode.Acceleration);

                    // B. LOGIC MỚI: Check trực tiếp trị tuyệt đối X của quả bóng
                    if (Mathf.Abs(transform.position.x) > 0.3f)
                    {
                        // Hướng kéo vẫn nhắm về cái lỗ để bóng rơi chuẩn vào tâm
                        Vector3 directionToHole = (target.position - transform.position).normalized;

                        // Lực đẩy nhẹ (bạn có thể tinh chỉnh số này)
                        float gentlePullForce = 3f;

                        rb.AddForce(directionToHole * gentlePullForce, ForceMode.Acceleration);
                    }
                }

                // ==========================================
                // 2. LOGIC KIỂM TRA ĐỂ CHUI VÀO ỐNG
                // ==========================================
                // Khoảng cách thực tế từ bóng đến lỗ để quyết định việc tước vật lý
                float deltaX = Mathf.Abs(transform.position.x - target.position.x);

                // Nếu lọt xuống dưới Y và X nằm ngay sát tâm lỗ
                if (transform.position.y < target.position.y + 0.1f && deltaX < 0.5f)
                {
                    // Xóa vật lý
                    if (rb != null) Destroy(rb);

                    Collider col = GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    // Bắt đầu hành trình trượt trong ống
                    MoveAlongTransformListTask().Forget();

                    // Thu nhỏ bóng
                    LMotion.Create(Vector3.one, Vector3.one * 0.86f, .5f)
                        .WithEase(Ease.Linear)
                        .WithDelay(0.1f)
                        .BindToLocalScale(transform).AddTo(GameController.Instance);

                    break; // Dừng vòng lặp check độ cao
                }
            }

            await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
        }
    }

    private async UniTaskVoid MoveAlongTransformListTask()
    {
        // ==========================================
        // GIAI ĐOẠN 1.0: XẾP HÀNG CHỜ RƠI XUỐNG ỐNG (GIÃN CÁCH)
        // ==========================================
        float myEnterTime;
        if (Time.time > _nextEnterPipeTime) myEnterTime = Time.time;
        else myEnterTime = _nextEnterPipeTime;

        float enterInterval = 0.025f; // Thời gian giãn cách giữa các bóng trong ống
        _nextEnterPipeTime = (myEnterTime + enterInterval) / 3;

        float waitToEnter = myEnterTime - Time.time;
        if (waitToEnter > 0)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(waitToEnter), ignoreTimeScale: false, cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        if (this == null || gameObject == null)
        {
            ReleasePipeReservation();
            return;
        }

        if (GameController.Instance == null || GameController.Instance.pipeNodes == null || GameController.Instance.pipeNodes.Count == 0)
        {
            ReleasePipeReservation();
            return;
        }

        List<Transform> nodes = GameController.Instance.pipeNodes;
        float speed = GameConfig.Instance.pipeMoveSpeed;
        int currentNodeIndex = 0;

        // ==========================================
        // GIAI ĐOẠN 1: TRƯỢT TRONG ỐNG
        // ==========================================
        while (currentNodeIndex < nodes.Count && this != null && gameObject != null)
        {
            Transform targetNode = nodes[currentNodeIndex];
            if (targetNode == null)
            {
                currentNodeIndex++;
                continue;
            }

            Vector3 targetWorldPos = targetNode.position;

            while (this != null && gameObject != null && Vector3.Distance(transform.position, targetWorldPos) > 0.001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, speed * Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            currentNodeIndex++;
        }

        // ==========================================
        // GIAI ĐOẠN 2: XẾP HÀNG CHỜ BẮN (TẠO NHỊP BỤP BỤP)
        // ==========================================
        if (this == null || gameObject == null)
        {
            ReleasePipeReservation();
            return;
        }

        // 1. Chốt thời gian bắn cho quả bóng NÀY
        float myShootTime;
        if (Time.time > _nextShootTime)
        {
            // Nếu họng súng đang rảnh -> Bắn luôn
            myShootTime = Time.time;
        }
        else
        {
            // Nếu đang kẹt xe -> Xếp hàng sau thời gian của quả trước đó
            myShootTime = _nextShootTime;
        }

        // Tốc độ nhả đạn (Ví dụ 0.15s sẽ ra nhịp bụp bụp rất đã tai. Bạn có thể cho vào GameConfig)
        float shootInterval = 0.068f;

        // Cập nhật lại lịch bắn cho quả bóng tới sau
        _nextShootTime = myShootTime + shootInterval;

        // 2. Ép quả bóng này đứng chờ ở cửa ống cho tới lượt
        float waitDuration = myShootTime - Time.time;
        if (waitDuration > 0)
        {
            // Dùng TimeSpan để Delay an toàn với UniTask
            await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), ignoreTimeScale: false, cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        // Kiểm tra an toàn trước khi bắn (phòng trường hợp đang đợi thì bóng bị xóa)
        if (this == null || gameObject == null)
        {
            ReleasePipeReservation();
            return;
        }

        // ==========================================
        // GIAI ĐOẠN 3: TÌM SLOT PHÙ HỢP SAU KHI ĐI HẾT ỐNG, RỒI BẮN VÀO SHOVE
        // ==========================================
        ShoveMovement targetShove = _targetShoveAfterPipe;
        if (targetShove == null && ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0)
        {
            targetShove = ShoveContainer.Instance.shoveList[0];
        }

        if (targetShove != null)
        {
            float waitSlotTimeout = 0.5f;
            float deadline = Time.time + waitSlotTimeout;

            while (_assignedShove == null && Time.time <= deadline && this != null && gameObject != null)
            {
                foreach (var s in targetShove.GetComponentsInChildren<SmallShove>())
                {
                    if (s.PendingBallCount + s.CurrentBallCount < s.NumBallFull)
                    {
                        _assignedShove = s;
                        break;
                    }
                }

                if (_assignedShove == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            if (_assignedShove != null)
            {
                if (_hasPipeReservation)
                {
                    targetShove.ReleaseInPipeBall();
                    _hasPipeReservation = false;
                }

                _assignedShove.PendingBallCount++;

                Vector3 startPos = transform.position;
                Transform endTarget = _assignedShove.GetPosTransform();

                float flyDuration = 0.4f; // Bay nhanh dứt khoát
                float arcHeight = .068f; // Độ cao vòng cung

                // Thực thi bắn bám theo mục tiêu (đích di động)
                await transform.ShootArcAsync(startPos, endTarget, flyDuration, arcHeight, this.GetCancellationTokenOnDestroy());

                if (this != null && gameObject != null && targetShove != null)
                {
                    // Xét Parent và reset Local Position tĩnh để bay theo thùng Shove an toàn
                    transform.SetParent(endTarget); // Làm con trực tiếp của cái transform cục bộ luôn để cứng ngắt
                    transform.localPosition = Vector3.zero;

                    _assignedShove.ReceiveBall();
                }
                else if (_assignedShove.PendingBallCount > 0)
                {
                    _assignedShove.PendingBallCount--;
                }
            }
            else
            {
                ReleasePipeReservation();
                Debug.LogWarning("Ball đi hết ống nhưng chưa có slot SmallShove trống sau thời gian chờ an toàn.");
            }
        }
        else
        {
            ReleasePipeReservation();
            Debug.LogWarning("Ball đi hết ống nhưng không có Shove mục tiêu hợp lệ.");
        }
    }

    private void ReleasePipeReservation()
    {
        if (_hasPipeReservation && _targetShoveAfterPipe != null)
        {
            _targetShoveAfterPipe.ReleaseInPipeBall();
        }

        _hasPipeReservation = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        // Kiểm tra xem vật chạm vào có đúng là viền phễu không
        if (collision.gameObject.CompareTag("FunnelWall"))
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            // Đảm bảo bóng vẫn còn vật lý và đích đến vẫn tồn tại
            if (rb != null && GameController.Instance != null && GameController.Instance.disablePhysicsTransform != null)
            {
                Transform target = GameController.Instance.disablePhysicsTransform;

                // 1. Lấy hướng vector từ vị trí hiện tại của bóng chĩa thẳng về tâm lỗ
                Vector3 directionToHole = (target.position - transform.position).normalized;

                // Tùy chọn: Nếu bạn không muốn nó bị ép xuống/tâng lên quá mạnh, có thể triệt tiêu trục Y
                // directionToHole.y = 0; 
                // directionToHole = directionToHole.normalized;

                // 2. Lực đẩy lùa bóng về tâm. 
                // Dùng ForceMode.Acceleration để tạo lực đẩy liên tục mượt mà chừng nào bóng còn cọ xát vào viền
                float edgePushForce = 8f;
                rb.AddForce(directionToHole * edgePushForce, ForceMode.Acceleration);
            }
        }
    }

    public async UniTask TransferToShoveAsync(SmallShove targetShove)
    {
        if (targetShove == null || this == null || gameObject == null) return;

        _assignedShove = targetShove;

        Vector3 startPos = transform.position;
        Transform endTarget = _assignedShove.GetPosTransform();
        
        float flyDuration = 0.4f;
        float arcHeight = 0.068f;

        // Bắn vòng cung thẳng vào mục tiêu
        await transform.ShootArcAsync(startPos, endTarget, flyDuration, arcHeight, this.GetCancellationTokenOnDestroy());

        if (this != null && gameObject != null && _assignedShove != null)
        {
            transform.SetParent(endTarget);
            transform.localPosition = Vector3.zero;
            _assignedShove.ReceiveBall();
        }
    }
}