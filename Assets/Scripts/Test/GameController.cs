using UnityEngine;
using UnityEngine.InputSystem;

public class GameController : MonoBehaviour
{
    public static GameController Instance;

    public enum InputState { Idle, PotentialInteraction, LongPressing, Swiping, PackDisabled }
    
    [Header("Current State")]
    public InputState currentState = InputState.Idle;

    [Header("Settings")]
    public Camera mainCamera;
    public LayerMask ballLayer;
    public float longPressDuration = 0.5f;
    public float moveThreshold = 15f; // Giảm xuống một chút để xoay nhạy hơn

    private PackBalls _currentDisabledPack;
    private Vector2 _startPosition;
    private float _timer;
    private bool _hitBallOnDown; // Lưu xem lúc nhấn xuống có trúng bóng không

    public bool IsHoldingBall => currentState == InputState.PackDisabled;

    private void Awake() => Instance = this;

    private void Update() => HandleInputState();

    private void HandleInputState()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 currentMousePos = mouse.position.ReadValue();

        switch (currentState)
        {
            case InputState.Idle:
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _startPosition = currentMousePos;
                    _timer = 0;
                    // Kiểm tra xem có trúng bóng không nhưng CHƯA hành động gì cả
                    _hitBallOnDown = CheckIfHittingBall(_startPosition);
                    
                    // Chuyển sang trạng thái chờ tương tác (Bất kể bấm vào đâu)
                    currentState = InputState.PotentialInteraction;
                }
                break;

            case InputState.PotentialInteraction:
                _timer += Time.deltaTime;
                float dist = Vector2.Distance(_startPosition, currentMousePos);

                // 1. DI CHUYỂN -> Chuyển sang xoay ngay lập tức (Bất kể có trúng bóng hay không)
                if (dist > moveThreshold)
                {
                    currentState = InputState.Swiping;
                }
                // 2. GIỮ IM + TRÚNG BÓNG -> Chuyển sang Disable Pack
                else if (_timer >= longPressDuration && _hitBallOnDown)
                {
                    ExecuteDisablePack(_startPosition);
                    currentState = InputState.PackDisabled;
                }
                // 3. THẢ TAY SỚM
                else if (mouse.leftButton.wasReleasedThisFrame)
                {
                    // Nếu bấm nhanh vào bóng thì mới Log
                    if (_hitBallOnDown) ExecuteClickLog(currentMousePos);
                    currentState = InputState.Idle;
                }
                break;

            case InputState.Swiping:
                // Trạng thái này cho phép SmoothModelRotator chạy
                if (mouse.leftButton.wasReleasedThisFrame) currentState = InputState.Idle;
                break;

            case InputState.PackDisabled:
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    EnableCurrentPack();
                    currentState = InputState.Idle;
                }
                break;
        }
    }

    private bool CheckIfHittingBall(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        return Physics.Raycast(ray, 100f, ballLayer);
    }

    private void ExecuteClickLog(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ballLayer))
        {
            PackBalls pack = hit.collider.GetComponentInParent<PackBalls>();
    
            if (pack != null)
            {
                Vector3 explosionPos = hit.point;
            
                // THÔNG SỐ NỔ NHẸ (Bạn có thể chỉnh ở đây)
                float explosionRadius = 2f; 
                float explosionForce = 20f; // Mức 50f là rất nhẹ, chỉ đủ rã khối
                float upwardsModifier = 0.3f; // Đẩy nhẹ lên trên để rụng tự nhiên

                // Chuyển List sang Array để duyệt an toàn
                GameObject[] ballArray = pack.balls.ToArray();

                // BƯỚC 1: Add Rigidbody cho TOÀN BỘ bóng trước
                foreach (GameObject ballObj in ballArray)
                {
                    if (ballObj != null)
                    {
                        Ball ballScript = ballObj.GetComponent<Ball>();
                        if (ballScript != null)
                        {
                            ballScript.PreparePhysics(); // Gọi hàm mới đã sửa ở Ball.cs
                        }
                    }
                }

                // BƯỚC 2: Tác động lực nổ sau khi tất cả đã có vật lý
                foreach (GameObject ballObj in ballArray)
                {
                    if (ballObj != null)
                    {
                        Rigidbody rb = ballObj.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            // Hàm chuẩn của Unity để nổ khối mượt mà
                            rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, upwardsModifier);
                        }
                    }
                }

                // Xóa danh sách và hủy cha
                pack.balls.Clear();
                Destroy(pack.gameObject, 3.5f);
            
                Debug.Log($"<color=orange>[Explosion]</color> Đã rã khối: {pack.name}");
            }
        }
    }

    private void ExecuteDisablePack(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ballLayer))
        {
            _currentDisabledPack = hit.collider.GetComponentInParent<PackBalls>();
            if (_currentDisabledPack != null)
            {
                Vector3 explosionPos = hit.point;
                // Bán kính nổ nên bao trùm toàn bộ Pack (ví dụ 5m)
                float explosionRadius = 5f; 
                // LỰC CỰC NHẸ: Thử mức 50f đến 100f (vì ExplosionForce dùng đơn vị khác AddForce)
                float explosionForce = 50f; 

                // Bước 1: Kích hoạt Rigidbody cho TOÀN BỘ bóng trong Pack trước
                GameObject[] ballArray = _currentDisabledPack.balls.ToArray();
                foreach (GameObject ballObj in ballArray)
                {
                    if (ballObj != null)
                    {
                        Ball b = ballObj.GetComponent<Ball>();
                        if (b != null) b.PreparePhysics();
                    }
                }

                // Bước 2: Tác động lực nổ đồng loạt sau khi tất cả đã có Rigidbody
                foreach (GameObject ballObj in ballArray)
                {
                    if (ballObj != null)
                    {
                        Rigidbody rb = ballObj.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            // AddExplosionForce(lực, tâm nổ, bán kính, lực nâng lên)
                            // 0.5f ở cuối giúp bóng hơi nảy lên rồi mới rơi xuống
                            rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, 0.5f);
                        }
                    }
                }
            
                _currentDisabledPack.balls.Clear();
                Destroy(_currentDisabledPack.gameObject, 3.5f);
            }
        }
    }

    private void EnableCurrentPack()
    {
        if (_currentDisabledPack != null)
        {
            _currentDisabledPack.gameObject.SetActive(true);
            _currentDisabledPack = null;
        }
    }
}