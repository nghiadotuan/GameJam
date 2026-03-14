using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public static class TransformExtensions
{
    /// <summary>
    /// Bắn object theo hình vòng cung Parabola từ Start đến End.
    /// </summary>
    /// <param name="transform">Transform của quả bóng cần bắn</param>
    /// <param name="startPos">Điểm xuất phát</param>
    /// <param name="endPos">Điểm rơi xuống</param>
    /// <param name="duration">Thời gian bay (giây)</param>
    /// <param name="arcHeight">Độ cao tối đa của vòng cung</param>
    /// <param name="cancellationToken">Token để hủy Task nếu object bị xóa</param>
    public static async UniTask ShootArcAsync(this Transform transform, Vector3 startPos, Vector3 endPos, float duration, float arcHeight, CancellationToken cancellationToken = default)
    {
        float elapsedTime = 0f;

        // Đặt object về vị trí xuất phát
        transform.position = startPos;

        while (elapsedTime < duration)
        {
            // Kiểm tra an toàn: Nếu object bị xóa hoặc bị cancel thì dừng ngay
            if (cancellationToken.IsCancellationRequested || transform == null) return;

            elapsedTime += Time.deltaTime;
            
            // Tính phần trăm thời gian đã trôi qua (t chạy từ 0 đến 1)
            float t = Mathf.Clamp01(elapsedTime / duration);

            // 1. Tính tọa độ tịnh tiến theo đường thẳng từ Start -> End
            Vector3 currentLinearPos = Vector3.Lerp(startPos, endPos, t);

            // 2. Tính độ cao của vòng cung Parabola
            // Công thức: 4 * h * t * (1 - t). Đỉnh cao nhất tại t = 0.5
            float currentHeight = arcHeight * 4f * t * (1f - t);

            // 3. Cộng độ cao vào trục Y để tạo đường cong
            currentLinearPos.y += currentHeight;

            // Cập nhật vị trí
            transform.position = currentLinearPos;

            // (Tùy chọn) Nếu bạn muốn quả bóng tự lăn/xoay khi bay, có thể thêm code xoay ở đây:
            // transform.Rotate(Vector3.right, 360f * Time.deltaTime);

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        // Đảm bảo khi kết thúc, object nằm chính xác tại điểm End
        if (transform != null)
        {
            transform.position = endPos;
        }
    }
}