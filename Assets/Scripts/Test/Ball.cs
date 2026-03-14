using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

public class Ball : MonoBehaviour
{
    private bool _isExploded = false;

    public async UniTask BurstAndFall(Vector3 explosionCenter, float forcePercent)
    {
        if (_isExploded) return;
        _isExploded = true;

        GameConfig config = GameConfig.Instance;
        transform.SetParent(null, true);

        Vector3 dir = (transform.position - explosionCenter).normalized;
        dir.y += config.burstUpward;
        dir.Normalize();

        Vector3 startPos = transform.position;

        // LỰC BIẾN THIÊN: Quả gần bung xa, quả xa bung gần
        float currentBurstDist = config.burstDistance * forcePercent;
        Vector3 burstTarget = startPos + dir * currentBurstDist;

        // --- GIAI ĐOẠN 1: BURST ---
        await LMotion.Create(startPos, burstTarget, config.burstDuration)
            .WithEase(Ease.OutQuad)
            .BindToPosition(transform)
            .ToUniTask();

        if (this == null || gameObject == null) return;

        // --- GIAI ĐOẠN 2: FALL ---
        Vector3 pos = transform.position;

        // Vận tốc cũng biến thiên theo lực nổ
        float currentHorizontalSpeed = config.fallHorizontalSpeed * forcePercent;
        Vector3 horizontalVel = new Vector3(dir.x, 0f, dir.z) * currentHorizontalSpeed;
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

        if (this != null) Destroy(gameObject);
    }
}