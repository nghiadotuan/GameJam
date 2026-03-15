using UnityEngine;

public class ShoveMovement : MonoBehaviour
{
    private bool _isMovingOut = false;
    private SmallShove[] _smallShoves;
    private int _inPipeBallCount;
    
    public ColorEnum TargetColor { get; set; } = ColorEnum.None;
    public int InPipeBallCount => _inPipeBallCount;

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

        if (GameController.Instance != null && GameController.Instance.stashShoves.Contains(this))
        {
            // Stash mỗi slot độc lập: chỉ cần slot vừa đầy thì thử transfer.
            if (shove != null && shove.NumBallFull > 0 && shove.CurrentBallCount >= shove.NumBallFull)
            {
                GameController.Instance.TryTransferStashToMainAsync().Forget();
            }
            return;
        }

        bool allFull = IsCompletelyFull();

        if (allFull)
        {
            if (ShoveContainer.Instance != null && ShoveContainer.Instance.shoveList.Count > 0 && ShoveContainer.Instance.shoveList[0] == this)
            {
                ShoveContainer.Instance.TriggerMoveOutFrontShove();
            }
        }
    }

    public bool IsCompletelyFull()
    {
        if (_smallShoves == null || _smallShoves.Length == 0)
        {
            _smallShoves = GetComponentsInChildren<SmallShove>();
        }

        foreach (var ss in _smallShoves)
        {
            // Chỉ xem là full khi bóng đã đáp thật sự, không tính pending đang bay.
            if (ss == null || ss.NumBallFull == 0 || ss.CurrentBallCount < ss.NumBallFull)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryReserveInPipeBall()
    {
        if (_isMovingOut) return false;

        if (_smallShoves == null || _smallShoves.Length == 0)
        {
            _smallShoves = GetComponentsInChildren<SmallShove>();
        }

        int totalCapacity = 0;
        int usedSlots = 0;
        foreach (var ss in _smallShoves)
        {
            if (ss == null) continue;
            totalCapacity += ss.NumBallFull;
            usedSlots += ss.CurrentBallCount + ss.PendingBallCount;
        }

        if (totalCapacity <= 0) return false;
        if (usedSlots + _inPipeBallCount >= totalCapacity) return false;

        _inPipeBallCount++;
        return true;
    }

    public void ReleaseInPipeBall()
    {
        if (_inPipeBallCount > 0) _inPipeBallCount--;
    }

    public void ResetToEmpty()
    {
        TargetColor = ColorEnum.None;
        _inPipeBallCount = 0;
        
        Shove shoveComp = GetComponent<Shove>();
        if (shoveComp != null) shoveComp.Color = ColorEnum.None;

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

        // Khôi phục bộ giáp xám bằng lệnh ApplyMaterial
        ApplyMaterial();
    }

    public void ApplyMaterial()
    {
        GameController.Instance = FindAnyObjectByType<GameController>();
        if (GameController.Instance == null || GameController.Instance.materialMapping == null || GameController.Instance.materialMapping.Count == 0) return;

        // Stash shove luôn giữ material hiện tại, không cho đổi trong mọi trường hợp.
        if (GameController.Instance.stashShoves != null && GameController.Instance.stashShoves.Contains(this)) return;

        int index = (int)TargetColor;
        if (index < 0 || index >= GameController.Instance.materialMapping.Count) index = 0; // Fallback None ( Gray )

        Material mat = GameController.Instance.materialMapping[index];
        if (mat == null) return;

        // Quét toàn bộ MeshRenderer trong Shove (Bao gồm vỏ xe và cỏ của nó, hoặc các họng nhỏ)
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        Debug.Log(index + "   "+ TargetColor + "   "+ renderers.Length);
        foreach (var r in renderers)
        {
            r.material = mat;
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

                if (GameController.Instance != null)
                {
                    GameController.Instance.RegisterShoveMoveOut();
                }
            }
        }
    }
}