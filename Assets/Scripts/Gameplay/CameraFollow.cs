using UnityEngine;

namespace JumpNowBro.Gameplay
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 deadzoneHalfExtents = new Vector2(2f, 1f);
        [SerializeField] float smoothTime = 0.15f;

        Vector3 velocity;

        public void SetTarget(Transform t) => target = t;

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 delta = target.position - transform.position;
            float dx = Mathf.Max(0, Mathf.Abs(delta.x) - deadzoneHalfExtents.x) * Mathf.Sign(delta.x);
            float dy = Mathf.Max(0, Mathf.Abs(delta.y) - deadzoneHalfExtents.y) * Mathf.Sign(delta.y);
            Vector3 desired = transform.position + new Vector3(dx, dy, 0f);
            desired.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
