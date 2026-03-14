using UnityEngine;

public class ShoveMovement : MonoBehaviour
{
    private bool _isMovingOut = false;
    private SmallShove[] _smallShoves;
    
    public ColorEnum TargetColor { get; set; } = ColorEnum.None;

    private void Start()
    {
        _smallShoves = GetComponentsInChildren<SmallShove>();
        foreach (var ss in _smallShoves)
        {
            ss.OnShoveFull += CheckAllShovesFull;
        }
    }

    private void OnDestroy()
    {
        if (_smallShoves != null)
        {
            foreach (var ss in _smallShoves)
            {
                if (ss != null) ss.OnShoveFull -= CheckAllShovesFull;
            }
        }
    }

    private void CheckAllShovesFull(SmallShove shove)
    {
        if (_isMovingOut) return;

        bool allFull = true;
        foreach (var ss in _smallShoves)
        {
            // Phải check số lượng bóng THỰC TẾ đã rơi vào (CurrentBallCount)
            // Thay vì IsFull (bị dính cả PendingBallCount của những quả đang bay)
            if (ss.CurrentBallCount < ss.NumBallFull || ss.NumBallFull == 0)
            {
                allFull = false;
                break;
            }
        }

        if (allFull)
        {
            if (ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0 && ShoveContainer.Instance.shoveList[0] == this)
            {
                ShoveContainer.Instance.TriggerMoveOutFrontShove();
            }
            else if (GameController.Instance != null && GameController.Instance.stashShoves.Contains(this))
            {
                // Gọi GameController thử dọn dẹp chuyển qua xe chính
                GameController.Instance.TryTransferStashToMainAsync().Forget();
            }
        }
    }

    public void ResetToEmpty()
    {
        TargetColor = ColorEnum.None;

        if (_smallShoves != null)
        {
            foreach (var ss in _smallShoves)
            {
                if (ss != null) ss.ResetShove();
            }
        }

        // Bỏ Parent toàn bộ bóng hiện có (để bọn chúng bay đi chỗ khác mà không bám vào chiếc xe này nữa)
        // Lưu ý: Các bóng này sẽ tự động SetParent vào SmallShove/ShoveMovement MỚI trong hàm TransferToShoveAsync của chúng
        foreach (Transform child in transform)
        {
            Ball b = child.GetComponent<Ball>();
            if (b != null)
            {
                child.SetParent(null, true);
            }
        }

        // Khôi phục bộ giáp xám
        if (GameController.Instance != null && GameController.Instance.materialMapping != null && GameController.Instance.materialMapping.Count > 0)
        {
            var renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = GameController.Instance.materialMapping[0]; // 0 là None (Xám)
            }
        }
    }

    /// <summary>
    /// Kích hoạt khi bị ShoveContainer đá ra khỏi List
    /// </summary>
    public void StartMoveOut()
    {
        _isMovingOut = true;
    }

    private void Update()
    {
        // Nó chỉ tự thân vận động khi đang ở trạng thái MoveOut
        if (_isMovingOut && ConveyorBelt.Instance != null && ConveyorBelt.Instance.EndShove != null)
        {
            // Đi tốc độ chậm mượt mà khi bị đầy, ví dụ đi bằng một nửa tốc độ băng chuyền bình thường
            float speed = GameConfig.Instance.shoveSpeed * 0.5f; 
            Vector3 targetPos = ConveyorBelt.Instance.EndShove.position;

            // Di chuyển về End Shove
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            // Khi chạm đích End Shove
            if (Vector3.Distance(transform.position, targetPos) <= 0.001f)
            {
                _isMovingOut = false;
                
                Debug.Log($"{gameObject.name} đã đi đến điểm End Shove!");
                // Xử lý logic tiếp theo của bạn ở đây (Ví dụ: Destroy, gỡ bóng, ghi điểm...)
                // Destroy(gameObject); 
            }
        }
    }
}