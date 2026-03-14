using UnityEngine;

public class ShoveMovement : MonoBehaviour
{
    private bool _isMovingOut = false;

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
            float speed = GameConfig.Instance.shoveSpeed;
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