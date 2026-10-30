using System.Collections;
using GyeMong.GameSystem.Creature;
using GyeMong.GameSystem.Creature.Mob.StateMachineMob;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    public enum EnemyRole { Imp, Spitter, Charger, Wisp, Brute, Warden }
    public sealed class ArcaneEnemy : StateMachineMob {
        public ArcaneSession Session { get; private set; }
        public ArcaneActorView View { get; private set; }
        public EnemyRole Role { get; private set; }
        public string SpawnId { get; private set; }
        public bool Dead { get; private set; }
        public bool Flying => Role == EnemyRole.Wisp;
        public bool Boss => Role == EnemyRole.Warden;
        public bool Slowed => Time.time < slowUntil;
        public bool Alerted { get; private set; }
        private float stunUntil, slowUntil, slowAmount, nextAttack;
        private Rigidbody2D body;
        private Vector2 home;
        private LineRenderer healthBar;
        public void Init(ArcaneSession owner, EnemyRole role, string id) {
            Session=owner;Role=role;SpawnId=id;home=transform.position;
            maxHp=currentHp=Boss?650:role==EnemyRole.Brute?190:role==EnemyRole.Charger?130:role==EnemyRole.Wisp?85:100;
            damage=Boss?28:role==EnemyRole.Brute?25:16; speed=role==EnemyRole.Charger?2.6f:Boss?1.9f:2.05f;
            body=gameObject.AddComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;body.mass=Boss?8:2;
            var collider=gameObject.AddComponent<CircleCollider2D>();collider.radius=Boss?.45f:.25f;
            View=gameObject.AddComponent<ArcaneActorView>();View.Init(owner.Assets,Boss?"warden":role==EnemyRole.Brute||role==EnemyRole.Charger?"golem":Flying?"wisp":"imp",Boss?1.7f:role==EnemyRole.Brute?1.1f:.8f,Flying?.4f:0);
            _spriteRenderer=View.Body;Session.Enemies.Add(this);StartMob();
            var bar=new GameObject("Health");bar.transform.SetParent(transform,false);healthBar=bar.AddComponent<LineRenderer>();healthBar.sharedMaterial=owner.Assets.material;healthBar.useWorldSpace=false;healthBar.positionCount=2;healthBar.startWidth=healthBar.endWidth=.055f;healthBar.startColor=healthBar.endColor=new Color(.9f,.32f,.22f);healthBar.sortingOrder=19000;
        }
        private Creature Target {
            get {
                if(Session.Golem && Vector2.Distance(transform.position,Session.Golem.transform.position)<2.6f) return Session.Golem;
                return Session.Player;
            }
        }
        private float Range => Role==EnemyRole.Spitter||Flying?4.6f:Role==EnemyRole.Charger?3.4f:Boss?2.7f:1.05f;
        protected override void Update() {
            if(!Dead && Session && !Session.Paused) base.Update();
            if(healthBar){healthBar.enabled=!Dead&&!Boss&&Alerted;healthBar.SetPosition(0,new Vector3(-.3f,.95f+(Flying?.4f:0)));healthBar.SetPosition(1,new Vector3(-.3f+.6f*Mathf.Max(0,currentHp/maxHp),.95f+(Flying?.4f:0)));}
        }
        private void FixedUpdate() { if(Dead || Time.time<stunUntil || Session.Paused)body.velocity=Vector2.zero; }
        public void ApplyControl(float stun,float slowDuration,float amount) {
            stunUntil=Mathf.Max(stunUntil,Time.time+stun);slowUntil=Mathf.Max(slowUntil,Time.time+slowDuration);slowAmount=Mathf.Max(amount,Time.time<slowUntil?slowAmount:0);
        }
        public override IEnumerator Stun(float duration) {ApplyControl(duration,0,0);yield return new WaitForSeconds(duration);}
        public override void OnAttacked(float amount) {
            if(Dead||amount<=0)return;Alerted=true;base.OnAttacked(amount);
            Session.Audio.Hit();
            Session.FloatText(View.BodyPosition,Mathf.RoundToInt(amount).ToString(),new Color(1,.86f,.4f));
            if(currentHp<=0)OnDead();
        }
        protected override IEnumerator Blink() {View.Flash();yield return null;}
        protected override void OnDead() {
            if(Dead)return;Dead=true;body.velocity=Vector2.zero;StopAllCoroutines();GetComponent<Collider2D>().enabled=false;
            Session.EnemyDefeated(this);Destroy(gameObject,.08f);
        }
        private void Think() {
            if(Time.time<stunUntil){body.velocity=Vector2.zero;return;}
            float playerDistance=Vector2.Distance(transform.position,Session.Player.transform.position);
            if(playerDistance<6.1f)Alerted=true;
            if(Vector2.Distance(transform.position,home)>8 || playerDistance>10 || Session.Player.Dead)Alerted=false;
            if(!Alerted) {body.velocity=Vector2.Distance(transform.position,home)>.25f?Session.Steer(transform.position,home)*speed:Vector2.zero;return;}
            Vector2 point=Target.transform.position;float distance=Vector2.Distance(transform.position,point);
            if(distance<Range && Time.time>nextAttack && !Physics2D.Linecast(transform.position,point,Session.Walls)) {
                ChangeState(new AttackState {mob=this});return;
            }
            Vector2 direction=Session.Steer(transform.position,point);
            if((Role==EnemyRole.Spitter||Flying)&&distance<2.1f)direction=-direction;
            body.velocity=direction*speed*(Slowed?1-slowAmount:1);View.moving=body.velocity.sqrMagnitude>.1f;
        }
        private IEnumerator Attack() {
            body.velocity=Vector2.zero;View.moving=false;
            Vector2 origin=transform.position,locked=Target.transform.position;
            bool ranged=Role==EnemyRole.Spitter||Flying;
            float radius=Boss?2.15f:Role==EnemyRole.Brute?1.25f:.9f;
            bool charge=Role==EnemyRole.Charger;
            Vector2 center=ranged||charge?locked:origin;
            if(charge)RiftVisual.Line(origin,locked,new Color(1,.35f,.15f),Session.Assets.material,.12f,.8f);
            else RiftVisual.Ring(center,ranged?.45f:radius,new Color(1,.3f,.16f),Session.Assets.material,.85f);
            yield return new WaitForSeconds(Boss?.9f:.7f);
            if(Dead||Time.time<stunUntil)yield break;
            if(ranged) {
                Vector2 aim=(locked-origin).normalized;
                ArcaneProjectile.Fire(Session,origin,aim,damage,4.8f,false);
                if(Flying)foreach(float angle in new[]{-18f,18f})ArcaneProjectile.Fire(Session,origin,Quaternion.Euler(0,0,angle)*aim,damage,4.1f,false);
            } else if(charge) {
                float end=Time.time+.38f;Vector2 dir=(locked-origin).normalized;bool hit=false;
                while(Time.time<end&&Time.time>=stunUntil) {
                    body.velocity=dir*8;
                    if(!hit&&Vector2.Distance(transform.position,Target.transform.position)<.8f){Target.OnAttacked(damage+6);hit=true;}
                    yield return null;
                }
                body.velocity=Vector2.zero;
            } else {
                Session.Blast(center,radius,damage,false);
                if(Boss)for(int i=0;i<8;i++)ArcaneProjectile.Fire(Session,origin,new Vector2(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4)),18,3.4f,false);
            }
            nextAttack=Time.time+(Boss?1.3f:.9f);
        }
        public sealed class ThinkState : BaseState {
            public override int GetWeight()=>1;
            public override IEnumerator StateCoroutine(){yield return null;}
            public override void OnStateUpdate(){((ArcaneEnemy)mob).Think();}
        }
        public sealed class AttackState : BaseState {
            public override int GetWeight()=>0;
            public override IEnumerator StateCoroutine(){yield return null;yield return ((ArcaneEnemy)mob).Attack();if(!((ArcaneEnemy)mob).Dead)mob.ChangeState(new RecoveryState{mob=mob});}
        }
        public sealed class RecoveryState : BaseState {
            public override int GetWeight()=>0;
            public override IEnumerator StateCoroutine(){yield return null;yield return new WaitForSeconds(.6f);if(!((ArcaneEnemy)mob).Dead)mob.ChangeState();}
        }
    }
}
