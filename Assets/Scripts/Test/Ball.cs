using UnityEngine;

public class Ball : MonoBehaviour
{
    private bool isExploded = false;

    public void PreparePhysics()
    {
        if (isExploded) return;
        isExploded = true;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.5f; 
        rb.linearDamping = 0.1f; // Tăng drag một chút để bóng rơi đầm hơn, không bị bay xa
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        Destroy(gameObject, 3f);
    }
}