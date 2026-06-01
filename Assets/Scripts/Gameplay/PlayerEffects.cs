using System.Collections;
using UnityEngine;

namespace JumpNowBro.Gameplay
{
    // Render-side juice driven by PlayerController events. Lives off the controller
    // so the simulation stays presentation-free: in Phase 2 the client renders from
    // STATE without running PlayerController, but can drive the same effect hooks.
    [RequireComponent(typeof(TrailRenderer))]
    public class PlayerEffects : MonoBehaviour
    {
        [SerializeField] float trailHoldTime = 0.25f;
        [SerializeField] float trailWidth = 0.45f;
        [SerializeField] Color dashColor = new Color(0.5f, 0.9f, 1f, 1f);
        [SerializeField] int dashBurstCount = 14;

        PlayerController controller;
        TrailRenderer trail;
        ParticleSystem burst;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            ConfigureTrail();
            ConfigureBurst();
        }

        void OnEnable()
        {
            if (controller != null) controller.OnDash += HandleDash;
        }

        void OnDisable()
        {
            if (controller != null) controller.OnDash -= HandleDash;
        }

        void ConfigureTrail()
        {
            trail = GetComponent<TrailRenderer>();
            trail.time = trailHoldTime;
            trail.startWidth = trailWidth;
            trail.endWidth = 0f;
            trail.numCapVertices = 4;
            trail.emitting = false;
            trail.sortingOrder = -1;
            if (trail.sharedMaterial == null)
                trail.material = new Material(Shader.Find("Sprites/Default"));

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(dashColor, 0f), new GradientColorKey(dashColor, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = grad;
        }

        void ConfigureBurst()
        {
            var go = new GameObject("DashBurst");
            go.transform.SetParent(transform, false);
            burst = go.AddComponent<ParticleSystem>();
            burst.Stop();

            var main = burst.main;
            main.playOnAwake = false;
            main.startLifetime = 0.3f;
            main.startSpeed = 3.5f;
            main.startSize = 0.15f;
            main.startColor = dashColor;
            main.gravityModifier = 0.2f;
            main.maxParticles = 64;

            var emission = burst.emission;
            emission.enabled = false; // burst manually on dash, no continuous emission

            var shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            var renderer = burst.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = -1;
        }

        /// Client entry point: the ClientPredictor drives this from predicted EdgeFlags, since the client
        /// destroys PlayerController and the OnDash subscription above never fires there. Host stays on OnDash.
        public void PlayDash() => HandleDash();

        void HandleDash()
        {
            trail.Clear();
            trail.emitting = true;
            if (burst != null) burst.Emit(dashBurstCount);
            StopAllCoroutines();
            StartCoroutine(StopTrailAfter(trailHoldTime));
        }

        IEnumerator StopTrailAfter(float t)
        {
            yield return new WaitForSeconds(t);
            trail.emitting = false;
        }
    }
}
