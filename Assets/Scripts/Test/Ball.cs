using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public class Ball : MonoBehaviour
{
    private bool _isExploded = false;

    /// <summary>
    /// Animate ball ra ngoài theo hướng từ explosionCenter bằng LitMotion,
    /// sau khi animation hoàn tất mới add Rigidbody để ball rơi xuống tự nhiên.
    /// </summary>
    public async UniTask BurstThenFall(Vector3 explosionCenter)
    {
        if (_isExploded) return;
        _isExploded = true;

        GameConfig config = GameConfig.Instance;

        // Tách khỏi parent ngay để tránh kế thừa transform từ Pack đang xoay
        transform.SetParent(null, true);

        // Tính hướng burst ra ngoài từ tâm nổ
        Vector3 dir = transform.position - explosionCenter;
        if (dir.sqrMagnitude < Mathf.Epsilon) dir = Random.onUnitSphere;
        dir.Normalize();
        dir.y += config.burstUpward;
        dir.Normalize();

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + dir * config.burstDistance;

        // Animate position ra ngoài, await hoàn tất rồi mới add Rigidbody
        await LMotion.Create(startPos, targetPos, config.burstDuration)
            .WithEase(Ease.OutQuad)
            .BindToPosition(transform)
            .ToUniTask();

        if (this == null || gameObject == null) return;

        // Add Rigidbody sau animation để ball rơi tự nhiên theo gravity
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = config.ballMass;
        rb.linearDamping = config.ballLinearDamping;
        rb.angularDamping = config.ballAngularDamping;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxDepenetrationVelocity = config.ballMaxDepenetrationVelocity;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Destroy(gameObject, config.ballAutoDestroyDelay);
    }

    /// <summary>
    /// Kích hoạt vật lý ngay lập tức không có animation (dùng cho hold explosion).
    /// </summary>
    public void PreparePhysics()
    {
        if (_isExploded) return;
        _isExploded = true;

        GameConfig config = GameConfig.Instance;

        transform.SetParent(null, true);

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = config.ballMass;
        rb.linearDamping = config.ballLinearDamping;
        rb.angularDamping = config.ballAngularDamping;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxDepenetrationVelocity = config.ballMaxDepenetrationVelocity;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Destroy(gameObject, config.ballAutoDestroyDelay);
    }
}