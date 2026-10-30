using System.Collections.Generic;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    public sealed class ArcaneProjectile : MonoBehaviour {
        private ArcaneSession session;
        private Vector2 direction;
        private float damage, speed, until, slowTime, slow, freeze;
        private bool friendly;
        private int pierce;
        private readonly HashSet<ArcaneEnemy> hit = new HashSet<ArcaneEnemy>();
        public static void Fire(ArcaneSession session, Vector2 position, Vector2 direction, float damage, float speed, bool friendly, int pierce = 0, float slowTime = 0, float slow = 0, float freeze = 0) {
            var go = new GameObject(friendly ? "Card bolt" : "Ember"); go.transform.position = position;
            var shot = go.AddComponent<ArcaneProjectile>(); shot.session=session; shot.direction=direction.normalized; shot.damage=damage; shot.speed=speed;
            shot.friendly=friendly; shot.pierce=pierce; shot.slowTime=slowTime; shot.slow=slow; shot.freeze=freeze; shot.until=Time.time+3;
            RiftVisual.Disk(go.transform, new Vector2(0,.35f), .09f, .12f, friendly ? slowTime > 0 ? Color.cyan : new Color(1,.8f,.3f) : new Color(1,.25f,.2f), session.Assets.material, 10000);
            var trail=go.AddComponent<TrailRenderer>(); trail.sharedMaterial=session.Assets.material; trail.startWidth=.09f; trail.endWidth=0; trail.time=.12f;
            trail.startColor= friendly ? Color.cyan : Color.red; trail.endColor=Color.clear; trail.sortingOrder=9999;
        }
        private void Update() {
            if (!session || Time.time > until) { Destroy(gameObject); return; }
            Vector2 start=transform.position, end=start+direction*speed*Time.deltaTime;
            var wall=Physics2D.CircleCast(start,.06f,direction,Vector2.Distance(start,end),session.Walls);
            if(wall.collider) { Destroy(gameObject); return; }
            if (friendly) {
                foreach(var enemy in session.Enemies.ToArray()) {
                    if(!enemy || enemy.Dead || hit.Contains(enemy) || Distance(enemy.transform.position,start,end) > (enemy.Boss ? .5f : .3f)) continue;
                    hit.Add(enemy); enemy.OnAttacked(damage); enemy.ApplyControl(freeze,slowTime,slow);
                    if(pierce-- <= 0) { Destroy(gameObject); return; }
                }
            } else {
                if(session.Golem && Distance(session.Golem.transform.position,start,end)<.35f) { session.Golem.OnAttacked(damage); Destroy(gameObject); return; }
                if(Distance(session.Player.transform.position,start,end)<.25f) { session.Player.OnAttacked(damage); Destroy(gameObject); return; }
            }
            transform.position=end;
        }
        private static float Distance(Vector2 point,Vector2 a,Vector2 b) {
            Vector2 d=b-a; float t=d.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude);
            return Vector2.Distance(point,a+d*t);
        }
    }
}
