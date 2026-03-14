using UnityEngine;

public class ShoveMovement : MonoBehaviour
{
    [Header("Liên kết bám đuôi (Train Logic)")]
    [Tooltip("Kéo cục Shove đi ngay phía trước vào đây. Nếu bỏ trống (null), cục này là Đầu Tàu (Head)")]
    public ShoveMovement previousShove;

    [Header("Trạng thái hiện tại")]
    public StateMovementShoveEnum currentState = StateMovementShoveEnum.Stop;

    
}