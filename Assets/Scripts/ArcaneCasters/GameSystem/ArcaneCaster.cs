using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneCasters.DataSystem;
using GyeMong.GameSystem.Creature;
using UnityEngine;
using Util.ChangeListener;

namespace ArcaneCasters.GameSystem {
    public sealed class CasterStatus : ChangeListenerCaller<IChangeListener<ArcaneCaster>, ArcaneCaster> { }

    public sealed class ArcaneCaster : Creature {
        public readonly CasterStatus Status = new CasterStatus();
        public ArcaneSpellBook Book { get; private set; }
        public float Mana { get; private set; } = 100;
        public int Potions { get; private set; } = 3;
        public Vector2 Move, Aim = Vector2.right;
        public ArcaneActorView View { get; private set; }
        public ArcaneSession Session { get; private set; }
        public bool Dead { get; private set; }
        public float InvulnerableUntil;
        public float[] Ready = new float[7];
        private Rigidbody2D body;
        private float shieldUntil, dashUntil, castUntil, notifyAt;
        private Vector2 dashDirection;
        public void Init(ArcaneSession session, ArcaneSpellBook book) {
            Session = session; Book = book; maxHp = currentHp = 120; speed = 3.4f;
            body = gameObject.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            gameObject.AddComponent<CircleCollider2D>().radius = .19f;
            View = gameObject.AddComponent<ArcaneActorView>(); View.Init(session.Assets, "mage", .85f); _spriteRenderer = View.Body;
        }
        private void Update() {
            if (!Session || Session.Paused || Dead) return;
            Mana = Mathf.Min(100, Mana + Time.deltaTime * 11);
            if (currentShield > 0 && Time.time >= shieldUntil) BreakWard();
            View.facing = Aim; View.moving = Move.sqrMagnitude > .01f; View.casting = Time.time < castUntil;
            if (Time.time > notifyAt) { notifyAt = Time.time + .1f; Status.Call(this); }
        }
        private void FixedUpdate() {
            if (!body) return;
            body.velocity = Dead || Session.Paused ? Vector2.zero : Time.time < dashUntil ? dashDirection * 13 : Vector2.ClampMagnitude(Move, 1) * speed;
        }
        public bool Cast(ArcaneSpell spell, Vector2 point) {
            if (Dead || Session.Paused || Book.Rank(spell) == 0 || Time.time < Ready[(int)spell]) return false;
            var stats = Book.Stats(spell);
            if (Mana < stats.cost) { Session.Toast("마력이 부족해. 잠깐 숨을 고르자."); return false; }
            Mana -= stats.cost; Ready[(int)spell] = Time.time + stats.cooldown; castUntil = Time.time + .18f;
            Session.Audio.Cast();
            Vector2 origin = transform.position, direction = (point - origin).normalized;
            if (direction.sqrMagnitude < .1f) direction = Aim;
            Aim = direction;
            switch (spell) {
                case ArcaneSpell.Bolt: case ArcaneSpell.Frost:
                    ArcaneProjectile.Fire(Session, origin, direction, stats.damage, stats.speed, true, stats.pierce, spell == ArcaneSpell.Frost ? stats.duration : 0, stats.slow, stats.freeze); break;
                case ArcaneSpell.Summon:
                    if (Session.Golem) Destroy(Session.Golem.gameObject);
                    Vector2 destination = origin + Vector2.ClampMagnitude(point - origin, 2.5f);
                    if (Physics2D.OverlapCircle(destination, .3f, Session.Walls)) destination = origin + direction * .5f;
                    Session.Golem = new GameObject("Summoned guardian").AddComponent<ArcaneGolem>();
                    Session.Golem.transform.position = destination; Session.Golem.Init(Session, stats); break;
                case ArcaneSpell.Lightning:
                    Vector2 center=origin+Vector2.ClampMagnitude(point-origin,5);
                    RiftVisual.Ring(center, stats.radius, new Color(1,.8f,.3f), Session.Assets.material);
                    var struck=new HashSet<ArcaneEnemy>();
                    foreach (var enemy in Session.Enemies.ToArray()) if (enemy && !enemy.Dead && Vector2.Distance(center, enemy.transform.position) <= stats.radius) {
                        struck.Add(enemy);
                        enemy.OnAttacked(stats.damage * (enemy.Slowed ? 1.25f : 1)); enemy.ApplyControl(stats.stun, 0, 0);
                        RiftVisual.Line(center+Vector2.up*3, enemy.View.BodyPosition, Color.yellow, Session.Assets.material);
                    }
                    Vector2 chainFrom=center;
                    for(int chain=0;chain<stats.chains&&struck.Count>0;chain++){
                        var next=Session.Enemies.Where(e=>e&&!e.Dead&&!struck.Contains(e)&&Vector2.Distance(e.transform.position,chainFrom)<3.1f).OrderBy(e=>Vector2.Distance(e.transform.position,chainFrom)).FirstOrDefault();
                        if(!next)break;struck.Add(next);next.OnAttacked(stats.damage*.6f);next.ApplyControl(stats.stun,0,0);RiftVisual.Line(chainFrom,next.View.BodyPosition,Color.yellow,Session.Assets.material);chainFrom=next.transform.position;
                    }
                    break;
                case ArcaneSpell.Roots: StartCoroutine(RootField(origin + Vector2.ClampMagnitude(point-origin, 3.5f), stats)); break;
                case ArcaneSpell.Ward: currentShield = stats.absorb; shieldUntil = Time.time + stats.duration; RiftVisual.Ring(origin, .65f, Color.cyan, Session.Assets.material, .7f); break;
                case ArcaneSpell.Dash:
                    dashDirection = Move.sqrMagnitude > .1f ? Move.normalized : direction;
                    dashUntil = Time.time + .14f; InvulnerableUntil = Time.time + stats.invincible;
                    if (stats.burst > 0) StartCoroutine(DashBurst(stats)); break;
            }
            Status.Call(this); return true;
        }
        private IEnumerator DashBurst(SpellStats stats) { yield return new WaitForSeconds(.15f); Session.Blast(transform.position, stats.radius, stats.burst, true); }
        private IEnumerator RootField(Vector2 point, SpellStats stats) {
            float until = Time.time + stats.duration;
            while (Time.time < until && !Dead) {
                RiftVisual.Ring(point, stats.radius, new Color(.4f,1,.5f), Session.Assets.material, .5f);
                foreach (var enemy in Session.Enemies.ToArray()) if (enemy && !enemy.Dead && Vector2.Distance(point, enemy.transform.position) < stats.radius) {
                    enemy.OnAttacked(stats.damage); if (!enemy.Flying) enemy.ApplyControl(.5f, 0, 0);
                }
                yield return new WaitForSeconds(.5f);
            }
        }
        private void BreakWard() {
            currentShield = 0; var stats = Book.Stats(ArcaneSpell.Ward);
            if (stats.burst > 0) Session.Blast(transform.position, stats.radius, stats.burst, true);
        }
        public override void OnAttacked(float amount) {
            if (Dead || amount <= 0 || Time.time < InvulnerableUntil) return;
            bool shielded = currentShield > 0; base.OnAttacked(amount);
            InvulnerableUntil = Time.time + .25f; Session.Shake(.12f); Session.FloatText(transform.position, "−" + Mathf.RoundToInt(amount), new Color(1,.4f,.3f));
            if (shielded && currentShield <= 0) BreakWard();
            currentHp = Mathf.Max(0, currentHp); Status.Call(this);
            if (currentHp <= 0) OnDead();
        }
        protected override IEnumerator Blink() { View.Flash(); yield return null; }
        protected override void OnDead() {
            if (Dead) return; Dead = true; body.velocity = Vector2.zero; StopAllCoroutines(); StartCoroutine(Respawn());
        }
        private IEnumerator Respawn() {
            Session.Toast("연구 기록은 무사해. 야영지에서 다시 시작하자."); yield return new WaitForSeconds(1.2f);
            transform.position = ArcaneSession.Camp; currentHp = maxHp; Mana = 100; currentShield = 0; Potions = 3; Dead = false;
            InvulnerableUntil = Time.time + 2; Session.Save(); Status.Call(this);
        }
        public bool Heal() {
            if (Dead || Session.Paused || Potions <= 0 || currentHp >= maxHp) return false;
            Potions--; currentHp = Mathf.Min(maxHp, currentHp+65); Status.Call(this); return true;
        }
        public void Rest() { currentHp = maxHp; Mana = 100; Potions = 3; Status.Call(this); }
        public void Reward(int xp) {
            Book.Save.experience += xp;
            while (Book.Save.experience >= Book.Save.level * 70) {
                Book.Save.experience -= Book.Save.level * 70; Book.Save.level++; Book.Save.research += 2;
                currentHp = maxHp; Session.Toast("연구 진전! 강화에 쓸 연구점 2를 얻었다. [K]");
            }
            Session.Save(); Status.Call(this);
        }
    }
}
