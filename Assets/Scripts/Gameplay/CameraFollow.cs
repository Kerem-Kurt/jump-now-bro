using UnityEngine;

namespace JumpNowBro.Gameplay
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 deadzoneHalfExtents = new Vector2(2f, 1f);
        [SerializeField] float smoothTime = 0.15f;
        [SerializeField] float deathShakeDuration = 0.3f;
        [SerializeField] float deathShakeMagnitude = 0.4f;

        Vector3 velocity;
        Vector3 followPos;
        bool initialized;
        float shakeTimer;
        float shakeDuration;
        float shakeMagnitude;

        public void SetTarget(Transform t)
        {
            target = t;
            followPos = transform.position;
            initialized = true;
        }

        public void Shake() => Shake(deathShakeDuration, deathShakeMagnitude);

        public void Shake(float duration, float magnitude)
        {
            shakeDuration = duration;
            shakeTimer = duration;
            shakeMagnitude = magnitude;
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (!initialized)
            {
                followPos = transform.position;
                initialized = true;
            }

            // Smooth-damp a base position the deadzone tracks; shake is layered on top
            // so it never feeds back into the damp and pollute the follow.
            Vector3 delta = target.position - followPos;
            float dx = Mathf.Max(0, Mathf.Abs(delta.x) - deadzoneHalfExtents.x) * Mathf.Sign(delta.x);
            float dy = Mathf.Max(0, Mathf.Abs(delta.y) - deadzoneHalfExtents.y) * Mathf.Sign(delta.y);
            Vector3 desired = followPos + new Vector3(dx, dy, 0f);
            desired.z = followPos.z;
            followPos = Vector3.SmoothDamp(followPos, desired, ref velocity, smoothTime);

            Vector3 shakeOffset = Vector3.zero;
            if (shakeTimer > 0f)
            {
                shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);
                float falloff = shakeDuration > 0f ? shakeTimer / shakeDuration : 0f;
                Vector2 r = Random.insideUnitCircle * (shakeMagnitude * falloff);
                shakeOffset = new Vector3(r.x, r.y, 0f);
            }

            transform.position = followPos + shakeOffset;
        }
    }
}
