using UnityEngine;

namespace KnightsGauntlet.View
{
    // A tiny, self-contained screen shake. Lives on the main camera and nudges it
    // with a decaying random offset for a fraction of a second, then snaps back to
    // rest. Used for the little "impact" kick when the player captures an enemy.
    //
    // The camera's rest position is owned by BoardView.FitCamera, which only rewrites
    // it on a screen change (rotation/resize) and otherwise leaves it alone. So while
    // a shake is running (a couple hundred ms) the offset is entirely ours to apply
    // and undo. We snapshot the rest position only when idle, so a shake that fires
    // while another is still decaying can't bake its own offset into the rest.
    public class CameraShake : MonoBehaviour
    {
        private Vector3 _rest;
        private float _timer;
        private float _duration;
        private float _magnitude;
        private bool _active;
        private Vector2 _kickDirection;

        // magnitude is the peak offset in world units; duration is the total time to
        // decay back to zero. A later Shake overrides an in-flight one so rapid
        // captures don't stack into a violent wobble.
        public void Shake(float magnitude, float duration)
            => Shake(Vector2.zero, magnitude, duration);

        public void Shake(Vector2 kickDirection, float magnitude, float duration)
        {
            if (magnitude <= 0f || duration <= 0f) return;
            if (!_active) _rest = transform.localPosition; // true rest, captured only when settled
            _active = true;
            _kickDirection = kickDirection.sqrMagnitude > 0.001f ? kickDirection.normalized : Vector2.zero;
            _magnitude = magnitude;
            _duration = duration;
            _timer = duration;
        }

        // A new player move takes visual priority over an unfinished impact shake.
        public void Cancel()
        {
            if (!_active) return;
            _timer = 0f;
            _active = false;
            transform.localPosition = _rest;
        }

        private void LateUpdate()
        {
            if (!_active) return;

            // Unscaled so the kick still reads if a pause/ad has frozen timeScale.
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                transform.localPosition = _rest;
                _active = false;
                return;
            }

            float k = _timer / _duration;          // 1 -> 0 over the shake
            float amp = _magnitude * k * k;         // ease out so it settles softly
            Vector2 off = _kickDirection * amp + Random.insideUnitCircle * (amp * 0.12f);
            transform.localPosition = _rest + new Vector3(off.x, off.y, 0f);
        }
    }
}
