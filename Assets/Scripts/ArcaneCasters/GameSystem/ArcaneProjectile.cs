using System.Collections.Generic;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // Projectiles move on the ground plane; the sprite is drawn at chest height above its shadow.
    public sealed class ArcaneProjectile : MonoBehaviour {
        public const float Radius = .12f;
        public const float VisualHeight = .55f;

        private ArcaneSession session;
        private Vector2 direction;
        private float damage, speed, until, slowTime, slow, freeze;
        private bool friendly;
        private int pierce;
        private readonly HashSet<ArcaneEnemy> hit = new HashSet<ArcaneEnemy>();

        public static void Fire(ArcaneSession session, Vector2 position, Vector2 direction, float damage, float speed, bool friendly,
                                int pierce = 0, float slowTime = 0, float slow = 0, float freeze = 0, float range = 9f, string look = null) {
            direction = direction.normalized;
            var go = new GameObject(friendly ? "Card bolt" : "Ember bolt");
            go.transform.position = position;
            var shot = go.AddComponent<ArcaneProjectile>();
            shot.session = session;
            shot.direction = direction;
            shot.damage = damage;
            shot.speed = speed;
            shot.friendly = friendly;
            shot.pierce = pierce;
            shot.slowTime = slowTime;
            shot.slow = slow;
            shot.freeze = freeze;
            shot.until = Time.time + range / Mathf.Max(.1f, speed);
            string effect = look ?? (friendly ? (slowTime > 0 ? "ice" : "card") : "ember");
            var sprite = session.Assets.Effect(effect);
            if (sprite) {
                var render = new GameObject("Sprite").AddComponent<SpriteRenderer>();
                render.transform.SetParent(go.transform, false);
                render.transform.localPosition = new Vector3(0, VisualHeight);
                render.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                render.transform.localScale = Vector3.one * ((friendly ? .62f : .55f) / Mathf.Max(sprite.bounds.size.x, .01f));
                render.sprite = sprite;
                render.sharedMaterial = session.Assets.material;
                render.sortingOrder = 21000;
            }
            ArcaneEffects.Disk(go.transform, .1f, .045f, new Color(0, 0, 0, .3f), session.Assets.material, ArcaneSorting.GroundDecal + 10);
        }

        private void Update() {
            if (!session || Time.time > until) { Destroy(gameObject); return; }
            if (session.Paused) return;
            Vector2 start = transform.position, end = start + direction * speed * Time.deltaTime;
            var wall = Physics2D.CircleCast(start, .06f, direction, Vector2.Distance(start, end), session.Walls);
            if (wall.collider) { Burst(wall.point); Destroy(gameObject); return; }
            if (friendly) {
                foreach (var enemy in session.Enemies.ToArray()) {
                    if (!enemy || enemy.Dead || hit.Contains(enemy)) continue;
                    if (ArcaneHitShapes.SegmentDistance(enemy.transform.position, start, end) > enemy.HitRadius + Radius) continue;
                    hit.Add(enemy);
                    enemy.OnAttacked(damage);
                    enemy.ApplyControl(freeze, slowTime, slow);
                    if (pierce-- <= 0) { Destroy(gameObject); return; }
                }
            } else {
                var golem = session.Golem;
                if (golem && ArcaneHitShapes.SegmentDistance(golem.transform.position, start, end) < .45f + Radius) { golem.OnAttacked(damage); Burst(end); Destroy(gameObject); return; }
                if (ArcaneHitShapes.SegmentDistance(session.Player.transform.position, start, end) <= ArcaneHitShapes.PlayerRadius + Radius) {
                    session.Player.OnAttacked(damage);
                    Destroy(gameObject);
                    return;
                }
            }
            transform.position = end;
        }

        private void Burst(Vector2 at) => ArcaneEffects.Sprite(session.Assets, "spark", at + Vector2.up * VisualHeight * .6f, .35f, .18f, 0, 21000, friendly ? Color.white : new Color(1, .6f, .4f));
    }
}
