using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneCasters.DataSystem;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    public sealed class ArcaneSession : MonoBehaviour {
        public ArcaneAssets Assets;
        public string SaveKey="ArcaneCastersRift.Unity.v1";
        public static readonly Vector2 Camp=new Vector2(-13,-7);
        public ArcaneCaster Player {get;private set;}
        public ArcaneGolem Golem;
        public readonly List<ArcaneEnemy> Enemies=new List<ArcaneEnemy>();
        public bool Paused {get;private set;}
        public bool InCombat=>Enemies.Any(e=>e&&!e.Dead&&e.Alerted&&Vector2.Distance(e.transform.position,Player.transform.position)<7);
        public LayerMask Walls=>LayerMask.GetMask("Wall");
        public Action<string> OnToast;
        public Action<string,string,string> OnDialogue;
        public Action<Vector2,string,Color> OnFloatText;
        public float ShakeUntil;
        public ArcaneAudio Audio {get;private set;}
        private LineRenderer compass;
        public int RewardsIssued {get;private set;}
        private HashSet<string> defeated;
        public string Objective {
            get {
                switch(Player.Book.Save.phase){
                    case 0:return "세계수의 뿌리 조사하기";
                    case 1:return "뿌리 근처의 불씨 정리하기";
                    case 2:return "개울과 돌무리의 흔적 조사하기";
                    case 3:return "유적의 문지기 제압하기";
                    case 4:return "야영지로 돌아가 오린에게 보고하기";
                    default:return "첫 현장 연구 완료 · 남은 숲 탐험하기";
                }
            }
        }
        public Vector2 Goal {
            get {var save=Player.Book.Save;return save.phase==0||save.phase==1?new Vector2(-4,-1):save.phase==2?!save.brook?new Vector2(-13,6):new Vector2(5,1):save.phase==3?new Vector2(14,8):Camp;}
        }
        private void Start() {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--arcane-capture-dir")>=0)SaveKey="ArcaneCasters.Capture."+Guid.NewGuid().ToString("N");
#endif
            Audio=gameObject.AddComponent<ArcaneAudio>();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            gameObject.AddComponent<ArcaneRenderCapture>();
#endif
            ArcaneSave save=null;
            try { if(PlayerPrefs.HasKey(SaveKey))save=JsonUtility.FromJson<ArcaneSave>(PlayerPrefs.GetString(SaveKey)); }catch(Exception){Toast("연구 기록을 읽지 못했어. 새 기록으로 시작할게.");}
            var book=new ArcaneSpellBook(Assets.spells,save);defeated=new HashSet<string>(book.Save.defeated);
            Player=new GameObject("Mira • field scholar").AddComponent<ArcaneCaster>();Player.transform.position=Camp;Player.Init(this,book);
            Spawn(EnemyRole.Imp,"root-1",new Vector2(-4,0));Spawn(EnemyRole.Imp,"root-2",new Vector2(-2,-2));Spawn(EnemyRole.Spitter,"root-3",new Vector2(-1,1));
            Spawn(EnemyRole.Wisp,"brook-1",new Vector2(-12,5));Spawn(EnemyRole.Spitter,"brook-2",new Vector2(-11,6));
            Spawn(EnemyRole.Charger,"bridge-1",new Vector2(8,2));Spawn(EnemyRole.Brute,"bridge-2",new Vector2(10,4));
            Spawn(EnemyRole.Wisp,"ruin-1",new Vector2(13,6));Spawn(EnemyRole.Spitter,"ruin-2",new Vector2(15,7));Spawn(EnemyRole.Warden,"warden",new Vector2(14,8));
            Spawn(EnemyRole.Brute,"grove-1",new Vector2(-7,2));Spawn(EnemyRole.Charger,"grove-2",new Vector2(-9,3));
            var guide=new GameObject("Research direction");compass=guide.AddComponent<LineRenderer>();compass.sharedMaterial=Assets.material;compass.startWidth=.06f;compass.endWidth=.02f;compass.startColor=compass.endColor=new Color(1,.76f,.24f,.7f);compass.positionCount=3;compass.sortingOrder=8000;
            var mentor=new GameObject("Orin").AddComponent<SpriteRenderer>();mentor.sprite=Assets.Get("orin");mentor.sharedMaterial=Assets.material;mentor.transform.position=Camp+new Vector2(-1.1f,.45f);if(mentor.sprite)mentor.transform.localScale=Vector3.one*(1.1f/mentor.sprite.bounds.size.y);mentor.sortingOrder=650;
            foreach(var point in new[]{new Vector2(-4,-1),new Vector2(-13,6),new Vector2(5,1),new Vector2(-8,2)})RiftVisual.Ring(point,.38f,new Color(1,.82f,.3f,.7f),Assets.material,100000);
            Toast("오린의 현장 실습 · 빛나는 뿌리로 가 보자. [F] 조사");
        }
        private void Update(){
            if(!Player||!compass)return;
            Vector2 direction=(Goal-(Vector2)Player.transform.position).normalized;Vector2 side=new Vector2(-direction.y,direction.x)*.13f;
            Vector2 tip=(Vector2)Player.transform.position+direction*.95f;
            compass.enabled=!Paused&&!InCombat&&Vector2.Distance(Goal,Player.transform.position)>2;
            compass.SetPosition(0,tip-direction*.24f+side);compass.SetPosition(1,tip);compass.SetPosition(2,tip-direction*.24f-side);
        }
        public Vector2 ResolveAim(Vector2 cursor){
            var body=Enemies.Where(e=>e&&!e.Dead&&Vector2.Distance(e.View.BodyPosition,cursor)<(e.Boss?.8f:.5f)).OrderBy(e=>Vector2.Distance(e.View.BodyPosition,cursor)).FirstOrDefault();
            return body?(Vector2)body.transform.position:cursor;
        }
        public ArcaneEnemy Spawn(EnemyRole role,string id,Vector2 position) {
            if(defeated.Contains(id))return null;
            var enemy=new GameObject(id).AddComponent<ArcaneEnemy>();enemy.transform.position=position;enemy.Init(this,role,id);return enemy;
        }
        public ArcaneEnemy Nearest(Vector2 point,float range) {return Enemies.Where(e=>e&&!e.Dead&&Vector2.Distance(point,e.transform.position)<range).OrderBy(e=>Vector2.Distance(point,e.transform.position)).FirstOrDefault();}
        public Vector2 Steer(Vector2 from,Vector2 to) {
            Vector2 direction=(to-from).normalized;
            foreach(float angle in new[]{0,35f,-35f,70f,-70f,110f,-110f}){
                Vector2 turn=Quaternion.Euler(0,0,angle)*direction;
                if(!Physics2D.CircleCast(from,.24f,turn,.65f,Walls))return turn;
            }
            return Vector2.zero;
        }
        public void EnemyDefeated(ArcaneEnemy enemy) {
            if(!defeated.Add(enemy.SpawnId))return;RewardsIssued++;Enemies.Remove(enemy);
            Player.Reward(enemy.Boss?150:35);Player.Book.Save.research+=enemy.Boss?3:1;
            if(enemy.Boss){Player.Book.Save.phase=4;Toast("목소리가 끊겼다. 오린에게 돌아가자.");}
            CheckProgress();Save();
        }
        private void CheckProgress() {
            var save=Player.Book.Save;
            if(save.phase==1&&new[]{"root-1","root-2","root-3"}.All(defeated.Contains)){save.phase=2;Unlock(ArcaneSpell.Lightning);}
            if(save.phase==2&&save.brook&&save.rocks)save.phase=defeated.Contains("warden")?4:3;
        }
        public string Interaction {
            get {
                Vector2 p=Player.transform.position;
                if(Vector2.Distance(p,Camp)<2)return "F  오린과 대화 · 휴식";
                if(Vector2.Distance(p,new Vector2(-4,-1))<1.8f)return "F  세계수 뿌리 조사";
                if(Vector2.Distance(p,new Vector2(-13,6))<1.8f)return "F  개울의 결정 조사";
                if(Vector2.Distance(p,new Vector2(5,1))<1.8f)return "F  돌무리 조사";
                if(Vector2.Distance(p,new Vector2(-8,2))<1.8f)return "F  덩굴 표본 채집";
                return "";
            }
        }
        public void Interact() {
            var save=Player.Book.Save;Vector2 p=Player.transform.position;
            if(Vector2.Distance(p,Camp)<2){
                if(InCombat){Toast("주변이 안전해지면 쉬자.");return;}Player.Rest();
                if(save.phase==4){save.phase=5;OnDialogue?.Invoke("오린","목소리가 악마들의 것이라고? 결론부터 적지 말거라. 다음에는, 누가 듣고 있었는지부터 살펴보자.","warden");}
                else OnDialogue?.Invoke("오린","미라, 오늘은 뿌리의 마력부터 재 보거라. 핀은 돌부족 쪽으로 보냈다. 카드 던지기는 좋지만 보고서도 잊지 말고.","mage");
            }else if(Vector2.Distance(p,new Vector2(-4,-1))<1.8f){if(save.phase==0)save.phase=1;OnDialogue?.Invoke("미라","뿌리에 열이 남아 있어. 핀이 가져간 측정기보다 내 카드가 먼저 알아챘네… 잠깐, 저 불씨들은 뭐지?","mage");}
            else if(Vector2.Distance(p,new Vector2(-13,6))<1.8f){if(InCombat){Toast("먼저 주변의 불씨를 정리하자.");return;}save.brook=true;Unlock(ArcaneSpell.Frost);OnDialogue?.Invoke("미라","차가운 결정이 불씨를 늦추고 있어. 카드에 이 흐름을 옮겨 보면… 빙결 뒤에 낙뢰를 쏘면 어떨까?","mage");}
            else if(Vector2.Distance(p,new Vector2(5,1))<1.8f){if(InCombat){Toast("먼저 주변을 안전하게 만들자.");return;}save.rocks=true;Unlock(ArcaneSpell.Ward);OnDialogue?.Invoke("미라","돌부족의 보호 무늬야. 그런데 왜 유적 쪽으로 이어져 있지? 핀, 넌 대체 어디까지 간 거야.","golem");}
            else if(Vector2.Distance(p,new Vector2(-8,2))<1.8f){if(InCombat)return;Unlock(ArcaneSpell.Roots);}
            CheckProgress();Save();
        }
        private void Unlock(ArcaneSpell spell){if(Player.Book.Unlock(spell)){Player.Book.Save.research+=2;Toast(Player.Book.Definition(spell).name+" 획득 · [K]에서 장착과 강화");}}
        public void Blast(Vector2 center,float radius,float damage,bool friendly){
            RiftVisual.Ring(center,radius,friendly?new Color(.4f,1,.8f):new Color(1,.35f,.15f),Assets.material,.3f);
            if(friendly){foreach(var enemy in Enemies.ToArray())if(enemy&&!enemy.Dead&&Vector2.Distance(center,enemy.transform.position)<=radius)enemy.OnAttacked(damage);}
            else{if(Vector2.Distance(center,Player.transform.position)<=radius)Player.OnAttacked(damage);if(Golem&&Vector2.Distance(center,Golem.transform.position)<=radius)Golem.OnAttacked(damage);}
        }
        public void Toast(string message)=>OnToast?.Invoke(message);
        public void FloatText(Vector2 p,string text,Color color)=>OnFloatText?.Invoke(p,text,color);
        public void Shake(float duration)=>ShakeUntil=Time.unscaledTime+duration;
        public void Pause(bool value){Paused=value;Time.timeScale=value?0:1;}
        public void Save(){if(!Player)return;Player.Book.Save.defeated=defeated.ToArray();PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(Player.Book.Save));PlayerPrefs.Save();}
        private void OnDestroy(){Time.timeScale=1;}
    }
}
