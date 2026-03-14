using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public class Ball : MonoBehaviour
{
    private bool _isExploded = false;

    public async UniTask BurstAndFall(Vector3 explosionDir, float forcePercent)
    {
        if (_isExploded) return;
        _isExploded = true;

        GameConfig config = GameConfig.Instance;
        transform.SetParent(null, true);

        // --- GIAI ĐOẠN 1: BURST (Bung tạo hình sóng) ---
        Vector3 startPos = transform.position;

        // Hướng bung đã được tính từ Cha -> Bóng (truyền từ Controller)
        Vector3 dir = explosionDir;

        // ĐỘ CAO SÓNG: Càng gần điểm click, lực nâng Y càng mạnh
        float waveUpward = config.burstUpward * forcePercent;
        dir.y += waveUpward;
        dir.Normalize();

        // TẦM VỚI SÓNG: Càng gần điểm click, bung càng xa
        float currentBurstDist = config.burstDistance * forcePercent;
        Vector3 burstTarget = startPos + dir * currentBurstDist;

        await LMotion.Create(startPos, burstTarget, config.burstDuration)
            .WithEase(Ease.OutQuad)
            .BindToPosition(transform)
            .ToUniTask();

        if (this == null || gameObject == null) return;

        // --- GIAI ĐOẠN 2: FALL ---
        Vector3 pos = transform.position;
        float currentHorizontalSpeed = config.fallHorizontalSpeed * forcePercent;
        Vector3 horizontalVel = new Vector3(dir.x, 0f, dir.z) * currentHorizontalSpeed;

        // Vận tốc rơi ban đầu cũng được hưởng lợi từ lực nâng của sóng
        float velocityY = config.fallInitialUpVelocity * forcePercent;

        float elapsed = 0f;
        while (this != null && elapsed < config.fallMaxDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            velocityY += config.fakeGravity * dt;
            pos += horizontalVel * dt;
            pos.y += velocityY * dt;
            transform.position = pos;

            if (pos.y < -15f) break;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        if (this != null && pos.y < -.15f)
        {
            PreparePhysics(new Vector3(horizontalVel.x, velocityY, horizontalVel.z));
        }
    }

    public void PreparePhysics(Vector3 exitVelocity)
    {
        GameConfig config = GameConfig.Instance;
        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = config.ballMass;
        rb.linearDamping = config.ballLinearDamping;
        rb.angularDamping = config.ballAngularDamping;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Giữ nguyên đà rơi từ toán học sang vật lý
        rb.linearVelocity = exitVelocity;
    }
}