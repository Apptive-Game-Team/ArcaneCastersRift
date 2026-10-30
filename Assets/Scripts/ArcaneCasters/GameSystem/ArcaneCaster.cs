using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneCasters.DataSystem;
using GyeMong.GameSystem.Creature;
using UnityEngine;
using Util.ChangeListener;

namespace ArcaneCasters.GameSystem {
    public sealed class CasterStatus : ChangeListenerCaller<IChangeListener<ArcaneCaster>, ArcaneCaster> { }

    // Mira, the player. Position is the ground point under the feet.
    public sealed class ArcaneCaster : Creature {
        public readonly CasterStatus Status = new CasterStatus();
        public ArcaneSpellBook Book { get; private set; }
        public float Mana { get; private set; } = 100;
        public int Potions { get; private set; } = 3;
        public Vector2 Move, Aim = Vector2.down;
        public ArcaneActorView View { get; private set; }
        public ArcaneSession Session { get; private set; }
        public bool Dead { get; private set; }
        public float InvulnerableUntil;
        public float[] Ready = new float[7];
        public int CastCount { get; private set; }

        private Rigidbody2D body;
        private float shieldUntil, dashUntil, castUntil, notifyAt, aimFaceUntil;
        private Vector2 dashDirection;
        private SpriteRenderer wardBubble;

        public void Init(ArcaneSession session, ArcaneSpellBook book) {
            Session = session;
            Book = book;
            maxHp = currentHp = 120;
            speed = 3.6f;
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            gameObject.AddComponent<CircleCollider2D>().radius = ArcaneHitShapes.PlayerRadius;
            View = gameObject.AddComponent<ArcaneActorView>();
            View.Init(session.Assets, "mira");
            _spriteRenderer = View.Body;
            wardBubble = new GameObject("Ward").AddComponent<SpriteRenderer>();
            wardBubble.transform.SetParent(transform, false);
            wardBubble.transform.localPosition = new Vector3(0, .75f);
            wardBubble.sprite = session.Assets.Icon("ward");
            wardBubble.sharedMaterial = session.Assets.material;
            if (wardBubble.sprite) wardBubble.transform.localScale = Vector3.one * (1.7f / wardBubble.sprite.bounds.size.x);
            wardBubble.color = new Color(1, 1, 1, .32f);
            wardBubble.enabled = false;
        }

        private void Update() {
            if (!Session || Session.Paused || Dead) return;
            Mana = Mathf.Min(100, Mana + Time.deltaTime * 11);
            if (currentShield > 0 && Time.time >= shieldUntil) BreakWard();
            bool walking = Move.sqrMagnitude > .01f;
            View.facing = walking && Time.time > aimFaceUntil ? Move : Aim;
            View.moving = Move.sqrMagnitude > .01f || Time.time < dashUntil;
            View.casting = Time.time < castUntil;
            wardBubble.enabled = currentShield > 0;
            wardBubble.sortingOrder = View.Body.sortingOrder + 1;
            bool invulnerable = Time.time < InvulnerableUntil;
            var color = View.Body.color;
            if (invulnerable && Time.time < dashUntil + .2f) View.Body.color = new Color(.75f, .9f, 1f, .65f);
            else if (color.a < .99f && !invulnerable) View.Body.color = Color.white;
            if (Time.time > notifyAt) { notifyAt = Time.time + .1f; Status.Call(this); }
        }

        private void FixedUpdate() {
            if (!body) return;
            if (Dead || Session.Paused) { body.velocity = Vector2.zero; return; }
            body.velocity = Time.time < dashUntil ? dashDirection * 12.5f : Vector2.ClampMagnitude(Move, 1) * speed;
        }

        public bool CanCast(ArcaneSpell spell) => !Dead && !Session.Paused && Book.Rank(spell) > 0 && Time.time >= Ready[(int)spell] && Mana >= Book.Stats(spell).cost;

        public bool Cast(ArcaneSpell spell, Vector2 point) {
            if (Dead || Session.Paused || Book.Rank(spell) == 0 || Time.time < Ready[(int)spell]) return false;
            var stats = Book.Stats(spell);
            if (Mana < stats.cost) { Session.Toast("마력이 부족해. 잠깐 숨을 고르자."); return false; }
            Mana -= stats.cost;
            Ready[(int)spell] = Time.time + stats.cooldown;
            CastCount++;
            Vector2 origin = transform.position, direction = (point - origin).normalized;
            if (direction.sqrMagnitude < .1f) direction = Aim;
            Aim = direction;
            if (spell != ArcaneSpell.Dash) { castUntil = Time.time + .2f; aimFaceUntil = Time.time + .6f; }
            Session.Audio.Cast(spell);
            switch (spell) {
                case ArcaneSpell.Bolt:
                case ArcaneSpell.Frost:
                    ArcaneProjectile.Fire(Session, origin + direction * .3f, direction, stats.damage, Mathf.Max(stats.speed, 9f), true, stats.pierce,
                        spell == ArcaneSpell.Frost ? stats.duration : 0, stats.slow, stats.freeze, range: 9f);
                    break;
                case ArcaneSpell.Summon: Summon(origin, point, direction, stats); break;
                case ArcaneSpell.Lightning: Lightning(origin, point, stats); break;
                case ArcaneSpell.Roots: StartCoroutine(RootField(origin + Vector2.ClampMagnitude(point - origin, 3.5f), stats)); break;
                case ArcaneSpell.Ward:
                    currentShield = stats.absorb;
                    shieldUntil = Time.time + stats.duration;
                    break;
                case ArcaneSpell.Dash:
                    dashDirection = Move.sqrMagnitude > .1f ? Move.normalized : direction;
                    dashUntil = Time.time + .15f;
                    InvulnerableUntil = Time.time + stats.invincible;
                    ArcaneEffects.Sprite(Session.Assets, "dust", origin + Vector2.up * .15f, .9f, .35f, 0, ArcaneSorting.At(origin.y) + 1);
                    if (stats.burst > 0) StartCoroutine(DashBurst(stats));
                    break;
            }
            Status.Call(this);
            return true;
        }

        private void Summon(Vector2 origin, Vector2 point, Vector2 direction, SpellStats stats) {
            if (Session.Golem) Destroy(Session.Golem.gameObject);
            Vector2 destination = origin + Vector2.ClampMagnitude(point - origin, 2.5f);
            if (Physics2D.OverlapCircle(destination, .4f, Session.Walls)) destination = origin + direction * .6f;
            Session.Golem = new GameObject("Summoned stone golem").AddComponent<ArcaneGolem>();
            Session.Golem.transform.position = destination;
            Session.Golem.Init(Session, stats);
        }

        private void Lightning(Vector2 origin, Vector2 point, SpellStats stats) {
            Vector2 center = origin + Vector2.ClampMagnitude(point - origin, 5);
            ArcaneEffects.Ring(center, stats.radius, new Color(1, .85f, .35f, .9f), Session.Assets.material, .35f, .06f, ArcaneSorting.Telegraph + 5);
            var struck = new HashSet<ArcaneEnemy>();
            foreach (var enemy in Session.Enemies.ToArray()) {
                if (!enemy || enemy.Dead || !ArcaneHitShapes.InCircle(center, stats.radius, enemy.transform.position, enemy.HitRadius)) continue;
                struck.Add(enemy);
                Strike(enemy.transform.position);
                enemy.OnAttacked(stats.damage * (enemy.Slowed ? 1.25f : 1));
                enemy.ApplyControl(stats.stun, 0, 0);
            }
            if (struck.Count == 0) Strike(center);
            Vector2 chainFrom = center;
            for (int chain = 0; chain < stats.chains && struck.Count > 0; chain++) {
                var next = Session.Enemies.Where(e => e && !e.Dead && !struck.Contains(e) && Vector2.Distance(e.transform.position, chainFrom) < 3.1f)
                                          .OrderBy(e => Vector2.Distance(e.transform.position, chainFrom)).FirstOrDefault();
                if (!next) break;
                struck.Add(next);
                ArcaneEffects.Line(chainFrom + Vector2.up * .5f, next.View.BodyPosition, new Color(1, .95f, .5f), Session.Assets.material, .07f, .25f);
                next.OnAttacked(stats.damage * .6f);
                next.ApplyControl(stats.stun, 0, 0);
                chainFrom = next.transform.position;
            }
            Session.Shake(.12f);
        }

        private void Strike(Vector2 ground) {
            var bolt = ArcaneEffects.Sprite(Session.Assets, "strike", ground + Vector2.up * 1.4f, 3f, .3f, 0, ArcaneSorting.At(ground.y) + 3);
            if (bolt) bolt.transform.localScale = Vector3.one * (3f / Mathf.Max(bolt.sprite.bounds.size.y, .01f));
        }

        private IEnumerator DashBurst(SpellStats stats) {
            yield return new WaitForSeconds(.15f);
            Session.Blast(transform.position, stats.radius, stats.burst, true);
        }

        private IEnumerator RootField(Vector2 point, SpellStats stats) {
            var patch = ArcaneEffects.Sprite(Session.Assets, "thorns", point, stats.radius * 2.1f, stats.duration, 0, ArcaneSorting.GroundDecal + 40);
            float until = Time.time + stats.duration;
            while (Time.time < until && !Dead) {
                foreach (var enemy in Session.Enemies.ToArray()) {
                    if (!enemy || enemy.Dead || !ArcaneHitShapes.InCircle(point, stats.radius, enemy.transform.position, enemy.HitRadius)) continue;
                    enemy.OnAttacked(stats.damage);
                    if (!enemy.Flying) enemy.ApplyControl(.5f, 0, 0);
                }
                yield return new WaitForSeconds(.5f);
            }
            if (patch) Destroy(patch.gameObject);
        }

        private void BreakWard() {
            currentShield = 0;
            var stats = Book.Stats(ArcaneSpell.Ward);
            if (stats.burst > 0) Session.Blast(transform.position, stats.radius, stats.burst, true);
        }

        public override void OnAttacked(float amount) {
            if (Dead || amount <= 0) return;
            if (Time.time < InvulnerableUntil) {
                if (Time.time < dashUntil + .25f) Session.Dodged(transform.position);
                return;
            }
            bool shielded = currentShield > 0;
            float before = currentHp;
            base.OnAttacked(amount);
            InvulnerableUntil = Time.time + .35f;
            if (currentHp < before) {
                View.Hurt(new Color(1f, .45f, .42f), .18f);
                Session.Shake(.14f);
                Session.Audio.PlayerHurt();
                Session.PlayerHurt(before - currentHp);
                Session.FloatText((Vector2)transform.position + Vector2.up * 1.6f, "−" + Mathf.RoundToInt(before - currentHp), new Color(1, .42f, .32f));
            } else {
                Session.Audio.WardHit();
                Session.FloatText((Vector2)transform.position + Vector2.up * 1.6f, "막음", new Color(.6f, .95f, 1f));
            }
            if (shielded && currentShield <= 0) BreakWard();
            currentHp = Mathf.Max(0, currentHp);
            Status.Call(this);
            if (currentHp <= 0) OnDead();
        }

        protected override IEnumerator Blink() { yield return null; }

        protected override void OnDead() {
            if (Dead) return;
            Dead = true;
            body.velocity = Vector2.zero;
            Move = Vector2.zero;
            StopAllCoroutines();
            Session.Audio.PlayerDown();
            View.FadeOut(.8f);
            StartCoroutine(Respawn());
        }

        private IEnumerator Respawn() {
            Session.PlayerDown();
            yield return new WaitForSeconds(2.2f);
            transform.position = Session.StartPoint;
            body.position = Session.StartPoint;
            currentHp = maxHp;
            Mana = 100;
            currentShield = 0;
            Potions = 3;
            Dead = false;
            View.Body.color = Color.white;
            InvulnerableUntil = Time.time + 2;
            Session.PlayerRespawned();
            Status.Call(this);
        }

        public bool Heal() {
            if (Dead || Session.Paused || Potions <= 0 || currentHp >= maxHp) return false;
            Potions--;
            currentHp = Mathf.Min(maxHp, currentHp + 65);
            Session.Audio.Heal();
            ArcaneEffects.Sprite(Session.Assets, "potion", (Vector2)transform.position + Vector2.up * 1.7f, .5f, .6f, 0, 22000);
            Status.Call(this);
            return true;
        }

        public void Rest() { currentHp = maxHp; Mana = 100; Potions = 3; Status.Call(this); }

        public void Reward(int xp) {
            Book.Save.experience += xp;
            while (Book.Save.experience >= Book.Save.level * 70) {
                Book.Save.experience -= Book.Save.level * 70;
                Book.Save.level++;
                Book.Save.research += 2;
                currentHp = maxHp;
                Session.Toast("연구 진전! 연구점 2를 얻었어. [K]에서 마법을 강화하자.");
                Session.Audio.Chime();
            }
            Session.Save();
            Status.Call(this);
        }
    }
}
