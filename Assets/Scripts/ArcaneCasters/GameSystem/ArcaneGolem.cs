using System.Collections;
using ArcaneCasters.DataSystem;
using GyeMong.GameSystem.Creature;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    public sealed class ArcaneGolem : Creature {
        private ArcaneSession session;
        private SpellStats stats;
        private ArcaneActorView view;
        private Rigidbody2D body;
        private float until, attackAt;
        private bool dead;
        public void Init(ArcaneSession owner,SpellStats spec) {
            session=owner;stats=spec;maxHp=currentHp=stats.health;until=Time.time+stats.duration;
            body=gameObject.AddComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;
            gameObject.AddComponent<CircleCollider2D>().radius=.25f;
            view=gameObject.AddComponent<ArcaneActorView>();view.Init(owner.Assets,"golem",1);_spriteRenderer=view.Body;
        }
        private void FixedUpdate() {
            if(dead || !session) return;
            if(Time.time>until) {OnDead();return;}
            ArcaneEnemy target=session.Nearest(transform.position,6);
            Vector2 destination=target?target.transform.position:session.Player.transform.position;
            float distance=Vector2.Distance(transform.position,destination);
            view.moving=distance>.85f;body.velocity=view.moving?session.Steer(transform.position,destination)*2.4f:Vector2.zero;
            if(target && distance<1 && Time.time>attackAt) {
                attackAt=Time.time+.9f;target.OnAttacked(stats.damage);
                session.Blast(target.transform.position,stats.splash,stats.splashDamage,true);
            }
        }
        public override void OnAttacked(float amount) {if(dead)return;base.OnAttacked(amount);if(currentHp<=0)OnDead();}
        protected override IEnumerator Blink() {view.Flash();yield return null;}
        protected override void OnDead() {if(dead)return;dead=true;Destroy(gameObject);}
    }
}
