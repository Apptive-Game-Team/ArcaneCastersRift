using System.Collections;
using GyeMong.GameSystem.Creature;
using GyeMong.GameSystem.Creature.Mob.StateMachineMob;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    public enum EnemyRole { Imp, Spitter, Charger, Wisp, Brute, Warden }

    // One fixed encounter. Every attack is: telegraph (exact damage shape, filling over the
    // wind-up) -> resolve with the same shape -> recovery window the player can punish.
    public sealed class ArcaneEnemy : StateMachineMob {
        private sealed class Profile {
            public string name, sprite;
            public float hp, damage, speed, hitRadius, range, windup, recovery, altitude;
            public bool keepsDistance;
        }

        private static Profile For(EnemyRole role) {
            switch (role) {
                case EnemyRole.Imp: return new Profile { name = "잿불 발톱", sprite = "enemy-imp", hp = 90, damage = 14, speed = 2.3f, hitRadius = .32f, range = 2.0f, windup = .55f, recovery = .75f };
                case EnemyRole.Spitter: return new Profile { name = "갈래불 악마", sprite = "enemy-spitter", hp = 120, damage = 13, speed = 1.9f, hitRadius = .42f, range = 5.4f, windup = .75f, recovery = .9f, keepsDistance = true };
                case EnemyRole.Charger: return new Profile { name = "폭주하는 돌", sprite = "enemy-charger", hp = 150, damage = 24, speed = 1.7f, hitRadius = .45f, range = 4.8f, windup = .95f, recovery = 1.0f };
                case EnemyRole.Wisp: return new Profile { name = "불고리 감시자", sprite = "enemy-wisp", hp = 95, damage = 12, speed = 2.2f, hitRadius = .38f, range = 4.2f, windup = .85f, recovery = 1.1f, altitude = .35f, keepsDistance = true };
                case EnemyRole.Brute: return new Profile { name = "용암 분쇄자", sprite = "enemy-brute", hp = 240, damage = 28, speed = 1.5f, hitRadius = .6f, range = 1.9f, windup = 1.0f, recovery = 1.1f };
                default: return new Profile { name = "유적의 문지기", sprite = "enemy-warden", hp = 700, damage = 30, speed = 1.4f, hitRadius = .95f, range = 3.0f, windup = 1.1f, recovery = 1.2f };
            }
        }

        public ArcaneSession Session { get; private set; }
        public ArcaneActorView View { get; private set; }
        public EnemyRole Role { get; private set; }
        public string SpawnId { get; private set; }
        public string DisplayName => profile.name;
        public bool Dead { get; private set; }
        public bool Flying => Role == EnemyRole.Wisp;
        public bool Boss => Role == EnemyRole.Warden;
        public bool Slowed => Time.time < slowUntil;
        public bool Alerted { get; private set; }
        public bool Telegraphing { get; private set; }
        public float HitRadius => profile.hitRadius;

        private Profile profile;
        private float stunUntil, slowUntil, slowAmount, nextAttack;
        private Rigidbody2D body;
        private Vector2 home;
        private LineRenderer healthBar, healthBack;
        private readonly System.Collections.Generic.List<ArcaneTelegraph> active = new System.Collections.Generic.List<ArcaneTelegraph>();
        private int attackCount;

        public void Init(ArcaneSession owner, EnemyRole role, string id) {
            Session = owner; Role = role; SpawnId = id; home = transform.position;
            profile = For(role);
            maxHp = currentHp = profile.hp;
            damage = profile.damage;
            speed = profile.speed;
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.mass = Boss ? 8 : 2;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            gameObject.AddComponent<CircleCollider2D>().radius = profile.hitRadius * .8f;
            View = gameObject.AddComponent<ArcaneActorView>();
            View.Init(owner.Assets, profile.sprite, profile.altitude);
            _spriteRenderer = View.Body;
            Session.Enemies.Add(this);
            StartMob();
            float barY = View.BodyBounds.max.y - transform.position.y + .18f;
            healthBack = Bar(new Color(.05f, .08f, .12f, .85f), barY, 19000, .085f);
            healthBar = Bar(new Color(.95f, .38f, .2f), barY, 19001, .055f);
        }

        private LineRenderer Bar(Color color, float y, int order, float width) {
            var go = new GameObject("Health");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Session.Assets.material;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.SetPosition(0, new Vector3(-.36f, y));
            line.SetPosition(1, new Vector3(.36f, y));
            return line;
        }

        private Creature Target {
            get {
                var golem = Session.Golem;
                if (golem && Vector2.Distance(transform.position, golem.transform.position) < 2.6f) return golem;
                return Session.Player;
            }
        }

        protected override void Update() {
            if (!Dead && Session && !Session.Paused) base.Update();
            bool show = !Dead && !Boss && (Alerted || currentHp < maxHp);
            if (healthBar) {
                healthBar.enabled = healthBack.enabled = show;
                float y = healthBar.GetPosition(0).y;
                healthBar.SetPosition(1, new Vector3(-.36f + .72f * Mathf.Max(0, currentHp / maxHp), y));
            }
        }

        private void FixedUpdate() {
            if (Dead || Time.time < stunUntil || Session.Paused) body.velocity = Vector2.zero;
        }

        public void ApplyControl(float stun, float slowDuration, float amount) {
            if (stun > 0 && Telegraphing) CancelAttack();
            stunUntil = Mathf.Max(stunUntil, Time.time + stun);
            slowUntil = Mathf.Max(slowUntil, Time.time + slowDuration);
            slowAmount = Mathf.Max(amount, Time.time < slowUntil ? slowAmount : 0);
        }

        public override IEnumerator Stun(float duration) { ApplyControl(duration, 0, 0); yield return new WaitForSeconds(duration); }

        public override void OnAttacked(float amount) {
            if (Dead || amount <= 0) return;
            Alerted = true;
            base.OnAttacked(amount);
            View.Hurt(new Color(1f, .62f, .5f));
            Session.Audio.EnemyHit();
            ArcaneEffects.Sprite(Session.Assets, "spark", View.BodyPosition, .5f, .16f, Random.Range(0, 360f), 22000);
            Session.FloatText(View.BodyPosition + Vector2.up * .3f, Mathf.RoundToInt(amount).ToString(), new Color(1, .86f, .4f));
            if (currentHp <= 0) OnDead();
        }

        protected override IEnumerator Blink() { yield return null; }

        protected override void OnDead() {
            if (Dead) return;
            Dead = true;
            CancelAttack();
            body.velocity = Vector2.zero;
            StopAllCoroutines();
            GetComponent<Collider2D>().enabled = false;
            Session.Audio.EnemyDown(Boss);
            ArcaneEffects.Sprite(Session.Assets, "dust", transform.position + Vector3.up * .3f, Boss ? 2.2f : 1.1f, .5f);
            View.FadeOut(.35f);
            Session.EnemyDefeated(this);
            Destroy(gameObject, .4f);
        }

        // Player died: everyone returns home healed, so the fight restarts fairly.
        public void ResetToHome() {
            CancelAttack();
            StopAllCoroutines();
            transform.position = home;
            body.velocity = Vector2.zero;
            currentHp = maxHp;
            Alerted = false;
            stunUntil = slowUntil = 0;
            nextAttack = Time.time + 1;
            ChangeState(new ThinkState { mob = this });
        }

        private void CancelAttack() {
            foreach (var telegraph in active) if (telegraph) telegraph.Finish(false);
            active.Clear();
            Telegraphing = false;
            if (View) View.windup = false;
        }

        private void Think() {
            if (Time.time < stunUntil) { body.velocity = Vector2.zero; return; }
            var player = Session.Player;
            float playerDistance = Vector2.Distance(transform.position, player.transform.position);
            if (playerDistance < (Boss ? 7.5f : 6.1f) && !player.Dead) Alerted = true;
            if (Vector2.Distance(transform.position, home) > 9 || playerDistance > 11 || player.Dead) Alerted = false;
            if (!Alerted) {
                bool away = Vector2.Distance(transform.position, home) > .25f;
                body.velocity = away ? Session.Steer(transform.position, home) * speed : Vector2.zero;
                View.moving = away;
                return;
            }
            Vector2 point = Target.transform.position;
            float distance = Vector2.Distance(transform.position, point);
            View.facing = point - (Vector2)transform.position;
            bool clear = !Physics2D.Linecast(transform.position, point, Session.Walls);
            if (distance < profile.range && Time.time > nextAttack && clear) {
                ChangeState(new AttackState { mob = this });
                return;
            }
            Vector2 direction = Session.Steer(transform.position, point);
            if (profile.keepsDistance && distance < profile.range * .55f) direction = Session.Steer(transform.position, (Vector2)transform.position * 2 - point);
            if (Flying && distance < profile.range) direction = Quaternion.Euler(0, 0, 80) * (point - (Vector2)transform.position).normalized;
            body.velocity = direction * speed * (Slowed ? 1 - slowAmount : 1);
            View.moving = body.velocity.sqrMagnitude > .05f;
        }

        private IEnumerator Attack() {
            body.velocity = Vector2.zero;
            View.moving = false;
            Vector2 origin = transform.position;
            Vector2 locked = Target.transform.position;
            Vector2 aim = (locked - origin).sqrMagnitude > .001f ? (locked - origin).normalized : Vector2.down;
            View.facing = aim;
            attackCount++;
            switch (Role) {
                case EnemyRole.Imp: yield return Lunge(origin, aim, 2.0f, .42f, 9.5f); break;
                case EnemyRole.Charger: yield return Lunge(origin, aim, 5.0f, .5f, 12f); break;
                case EnemyRole.Spitter: yield return Fan(origin, aim, new[] { -16f, 0f, 16f }, 6f, 6.5f); break;
                case EnemyRole.Wisp: yield return Fan(origin, aim, new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f }, 4.6f, 4.2f); break;
                case EnemyRole.Brute: yield return Slam(origin + aim * .7f, 1.5f, profile.windup, damage); break;
                default: yield return WardenPattern(origin, aim); break;
            }
            nextAttack = Time.time + profile.recovery;
        }

        private bool StillAble => !Dead && Time.time >= stunUntil;

        private void Resolve() {
            foreach (var telegraph in active) if (telegraph) telegraph.Finish(true);
            active.Clear();
        }

        private IEnumerator Telegraph(ArcaneTelegraph telegraph, float windup) {
            active.Add(telegraph);
            Telegraphing = true;
            View.windup = true;
            if (Boss || Role == EnemyRole.Brute || Role == EnemyRole.Charger) Session.Audio.Warning();
            yield return new WaitForSeconds(windup);
            Telegraphing = false;
            View.windup = false;
        }

        // The dasher's body sweeps along the lane, so the warned rectangle runs from its centre
        // to dash length plus its own radius; the hit test uses exactly that rectangle.
        private IEnumerator Lunge(Vector2 origin, Vector2 aim, float length, float halfWidth, float dashSpeed) {
            float reach = length + profile.hitRadius;
            yield return Telegraph(ArcaneTelegraph.Lane(Session.Assets.material, origin, aim, reach, halfWidth, profile.windup), profile.windup);
            if (!StillAble) { CancelAttack(); yield break; }
            Resolve();
            bool hitPlayer = false, hitGolem = false;
            Vector2 previous = transform.position;
            float travelled = 0;
            while (travelled < length && StillAble) {
                body.velocity = aim * dashSpeed;
                yield return new WaitForFixedUpdate();
                Vector2 now = transform.position;
                travelled = Vector2.Dot(now - origin, aim);
                float swept = Mathf.Min(travelled + profile.hitRadius, reach);
                var player = Session.Player;
                if (!hitPlayer && ArcaneHitShapes.InLane(origin, aim, swept, halfWidth, player.transform.position, ArcaneHitShapes.PlayerRadius)) {
                    hitPlayer = true;
                    player.OnAttacked(damage);
                }
                var golem = Session.Golem;
                if (!hitGolem && golem && ArcaneHitShapes.InLane(origin, aim, swept, halfWidth, golem.transform.position, .45f)) {
                    hitGolem = true;
                    golem.OnAttacked(damage);
                }
                bool stalled = Vector2.Distance(previous, now) < dashSpeed * Time.fixedDeltaTime * .3f;
                previous = now;
                if (!stalled) continue;
                // Stopped by a wall or prop (not by the target it just hit): the charger stumbles.
                if (Role == EnemyRole.Charger && !hitPlayer && !hitGolem) { stunUntil = Time.time + 1.4f; Session.Shake(.15f); Session.Audio.Thud(); }
                break;
            }
            body.velocity = Vector2.zero;
        }

        private IEnumerator Fan(Vector2 origin, Vector2 aim, float[] angles, float length, float projectileSpeed) {
            for (int i = 1; i < angles.Length; i++) {
                Vector2 direction = Quaternion.Euler(0, 0, angles[i]) * aim;
                active.Add(ArcaneTelegraph.Lane(Session.Assets.material, origin, direction, length, ArcaneProjectile.Radius, profile.windup));
            }
            Vector2 first = Quaternion.Euler(0, 0, angles[0]) * aim;
            yield return Telegraph(ArcaneTelegraph.Lane(Session.Assets.material, origin, first, length, ArcaneProjectile.Radius, profile.windup), profile.windup);
            if (!StillAble) { CancelAttack(); yield break; }
            Resolve();
            Session.Audio.EnemyShot();
            // Projectiles live exactly as far as the warning lanes reach.
            foreach (float angle in angles)
                ArcaneProjectile.Fire(Session, origin, Quaternion.Euler(0, 0, angle) * aim, damage, projectileSpeed, false, range: length);
        }

        private IEnumerator Slam(Vector2 center, float radius, float windup, float amount) {
            yield return Telegraph(ArcaneTelegraph.Circle(Session.Assets.material, center, radius, windup), windup);
            if (!StillAble) { CancelAttack(); yield break; }
            Resolve();
            Session.Audio.Slam(Boss);
            Session.Shake(Boss ? .25f : .14f);
            ArcaneEffects.Sprite(Session.Assets, "dust", center + Vector2.up * .2f, radius * 1.6f, .45f, 0, ArcaneSorting.GroundDecal + 20, new Color(1, .8f, .65f, .85f));
            Session.EnemyBlast(center, radius, amount);
        }

        private IEnumerator WardenPattern(Vector2 origin, Vector2 aim) {
            bool enraged = currentHp < maxHp * .5f;
            if (attackCount % 3 == 0) {
                // Stone shards in every direction: stand between the lanes.
                var angles = new float[10];
                for (int i = 0; i < angles.Length; i++) angles[i] = i * 36f + (attackCount % 2) * 18f;
                yield return Fan(origin, aim, angles, 5.5f, 4.6f);
                yield break;
            }
            yield return Slam(origin + aim * 1.0f, 2.3f, profile.windup, damage);
            if (!enraged || !StillAble) yield break;
            yield return new WaitForSeconds(.25f);
            Vector2 follow = Session.Player.transform.position;
            Vector2 toward = (follow - (Vector2)transform.position).normalized;
            float reach = Mathf.Min(Vector2.Distance(transform.position, follow), 2.6f);
            yield return Slam((Vector2)transform.position + toward * reach, 1.6f, .8f, damage * .8f);
        }

        public sealed class ThinkState : BaseState {
            public override int GetWeight() => 1;
            public override IEnumerator StateCoroutine() { yield return null; }
            public override void OnStateUpdate() { ((ArcaneEnemy)mob).Think(); }
        }

        public sealed class AttackState : BaseState {
            public override int GetWeight() => 0;
            public override IEnumerator StateCoroutine() {
                var enemy = (ArcaneEnemy)mob;
                yield return null;
                yield return enemy.Attack();
                if (!enemy.Dead) mob.ChangeState(new RecoveryState { mob = mob });
            }
            public override void OnStateExit() { ((ArcaneEnemy)mob).CancelAttack(); }
        }

        public sealed class RecoveryState : BaseState {
            public override int GetWeight() => 0;
            public override IEnumerator StateCoroutine() {
                var enemy = (ArcaneEnemy)mob;
                yield return null;
                yield return new WaitForSeconds(enemy.profile.recovery * .6f);
                if (!enemy.Dead) mob.ChangeState(new ThinkState { mob = mob });
            }
        }
    }
}
