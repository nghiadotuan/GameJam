using Sirenix.Utilities;
using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
public class GameConfig : GlobalConfig<GameConfig>
{
    [Header("Input")]
    public float longPressDuration = 0.5f;
    public float moveThreshold = 15f;

    [Header("Click Explosion")]
    public float clickExplosionRadius = 8f;
    public float clickMaxSpeed = 0f;
    public float clickUpwardsModifier = 0f;
    public int clickRippleDelayPerBallMs = 15;

    [Header("Burst Animation")]
    public float burstDistance = 0.4f;
    public float burstUpward = 0.1f;
    public float burstDuration = 0.18f;

    [Header("Fake Fall")]
    public float fakeGravity = -12f;
    public float fallInitialUpVelocity = 0.8f;
    public float fallHorizontalSpeed = 0.4f;
    public float fallDestroyBelowOffset = 15f;
    public float fallMaxDuration = 4f;

    [Header("Hold Explosion")]
    public float holdExplosionRadius = 8f;
    public float holdMaxSpeed = 0f;
    public float holdUpwardsModifier = 0f;

    [Header("Ball Physics")]
    public float ballMass = 1f;
    public float ballLinearDamping = 0.3f;
    public float ballAngularDamping = 0.3f;
    public float ballMaxDepenetrationVelocity = 0.02f;
    public float ballAutoDestroyDelay = 3f;

    [Header("Camera Zoom")]
    public float zoomSpeed = 5f;
    public float zoomMinDistance = 1f;
    public float zoomMaxDistance = 50f;
    public float zoomSmoothSpeed = 10f;

    [Header("Pipe Settings")]
    [Tooltip("Tốc độ bóng trượt trong ống nước")]
    public float pipeMoveSpeed = 5f;

    [Header("Shove Settings")]
    public float distanceShove = 1.5f; // Khoảng cách dây kéo
    public float shoveSpeed = 5f;      // Tốc độ di chuyển
}
