using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // Follows Mira and stops at the field edge so the void outside the map never shows.
    public sealed class ArcaneCamera : MonoBehaviour {
        public const float ViewHalfHeight = 4.6f;
        public ArcaneSession Session;
        private Camera view;

        private void Awake() {
            view = GetComponent<Camera>();
            view.orthographicSize = ViewHalfHeight;
        }

        private void LateUpdate() {
            if (!Session || !Session.Player || Session.Field == null) return;
            Vector3 focus = Session.Player.transform.position + Vector3.up * .6f;
            var boss = Session.Enemies.Find(e => e && e.Boss && e.Alerted && !e.Dead);
            if (boss) focus = Vector3.Lerp(focus, boss.transform.position + Vector3.up * 1.4f, .4f);
            Vector3 target = Clamp(focus);
            bool snap = (transform.position - target).sqrMagnitude > 64;
            transform.position = snap ? target : Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
            if (Time.unscaledTime < Session.ShakeUntil) transform.position += (Vector3)Random.insideUnitCircle * .05f;
        }

        private Vector3 Clamp(Vector3 p) {
            var bounds = Session.Field.Bounds;
            float halfW = view.orthographicSize * view.aspect, halfH = view.orthographicSize;
            p.x = bounds.width > halfW * 2 ? Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW) : bounds.center.x;
            p.y = bounds.height > halfH * 2 ? Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH) : bounds.center.y;
            p.z = -10;
            return p;
        }
    }
}
