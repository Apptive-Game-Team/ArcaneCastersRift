using System.Collections;
using ArcaneCasters.DataSystem;
using GyeMong.GameSystem.Creature;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // The stone golem Mira summons: walks to the nearest enemy, holds it and punches.
    public sealed class ArcaneGolem : Creature {
        private ArcaneSession session;
        private SpellStats stats;
        private ArcaneActorView view;
        private Rigidbody2D body;
        private float until, attackAt, swingUntil;
        private bool dead;

        public void Init(ArcaneSession owner, SpellStats spec) {
            session = owner;
            stats = spec;
            maxHp = currentHp = stats.health;
            until = Time.time + stats.duration;
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            gameObject.AddComponent<CircleCollider2D>().radius = .36f;
            view = gameObject.AddComponent<ArcaneActorView>();
            view.Init(owner.Assets, "ally-golem", 0, facesRight: true);
            _spriteRenderer = view.Body;
            ArcaneEffects.Sprite(owner.Assets, "stones", transform.position + Vector3.up * .15f, 1.8f, .55f, 0, ArcaneSorting.GroundDecal + 30);
            owner.Audio.Summon();
        }

        private void FixedUpdate() {
            if (dead || !session) return;
            if (session.Paused) { body.velocity = Vector2.zero; return; }
            if (Time.time > until) { OnDead(); return; }
            view.windup = Time.time < swingUntil;
            ArcaneEnemy target = session.Nearest(transform.position, 6);
            Vector2 destination = target ? (Vector2)target.transform.position : (Vector2)session.Player.transform.position + Vector2.right * 1.1f;
            float reach = target ? target.HitRadius + .75f : .6f;
            float distance = Vector2.Distance(transform.position, destination);
            view.facing = destination - (Vector2)transform.position;
            view.moving = distance > reach;
            body.velocity = view.moving ? session.Steer(transform.position, destination) * 2.4f : Vector2.zero;
            if (target && distance <= reach + .1f && Time.time > attackAt) {
                attackAt = Time.time + .9f;
                swingUntil = Time.time + .3f;
                target.OnAttacked(stats.damage);
                session.Blast(target.transform.position, stats.splash, stats.splashDamage, true);
            }
        }

        public override void OnAttacked(float amount) {
            if (dead) return;
            base.OnAttacked(amount);
            view.Hurt(new Color(1f, .7f, .6f));
            if (currentHp <= 0) OnDead();
        }

        protected override IEnumerator Blink() { yield return null; }

        protected override void OnDead() {
            if (dead) return;
            dead = true;
            ArcaneEffects.Sprite(session.Assets, "dust", transform.position + Vector3.up * .4f, 1.4f, .45f);
            Destroy(gameObject);
        }
    }
}
