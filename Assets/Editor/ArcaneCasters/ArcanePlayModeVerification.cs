using System;
using System.Collections;
using ArcaneCasters.DataSystem;
using ArcaneCasters.GameSystem;
using ArcaneCasters.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneCasters.Editor {
    [InitializeOnLoad]
    public static class ArcanePlayModeVerification {
        private const string Active="Arcane.Verify.Active",Key="Arcane.Verify.Key";
        private static IEnumerator routine;
        private static double deadline;
        private static double next;
        static ArcanePlayModeVerification(){EditorApplication.playModeStateChanged+=StateChanged;if(SessionState.GetBool(Active,false))EditorApplication.update+=Tick;}
        public static void Run(){
            SessionState.SetBool(Active,true);SessionState.SetString(Key,"ArcaneCastersRift.Verify."+Guid.NewGuid().ToString("N"));
            EditorSceneManager.OpenScene(ArcaneProjectBuilder.ScenePath);
            UnityEngine.Object.FindObjectOfType<ArcaneSession>().SaveKey=SessionState.GetString(Key,"");
            EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state){
            if(!SessionState.GetBool(Active,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){SceneManager.sceneLoaded+=OnSceneLoaded;routine=Verify();deadline=EditorApplication.timeSinceStartup+55;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        }
        private static void OnSceneLoaded(Scene scene,LoadSceneMode mode){var session=UnityEngine.Object.FindObjectOfType<ArcaneSession>();if(session)session.SaveKey=SessionState.GetString(Key,"");}
        private static void Tick(){
            if(!EditorApplication.isPlaying||routine==null)return;
            try{
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Verification timeout");
                if(EditorApplication.timeSinceStartup<next)return;
                if(!routine.MoveNext()){Finish(0);return;}
                next=EditorApplication.timeSinceStartup+(routine.Current is float seconds?seconds:0);
            }catch(Exception error){Debug.LogError("ARCANE_PLAYMODE_FAIL: "+error);Finish(1);}
        }
        private static void Check(bool condition,string name){if(!condition)throw new Exception(name);Debug.Log("ARCANE_CHECK_OK: "+name);}
        private static IEnumerator Verify(){
            yield return .5f;
            var s=UnityEngine.Object.FindObjectOfType<ArcaneSession>();s.GetComponent<ArcaneInput>().enabled=false;var p=s.Player;
            Check(p&&p.Book!=null,"scene creates mage and spell book");Check(s.Enemies.Count==12,"fixed encounter roster");
            Vector2 origin=p.transform.position;p.Move=Vector2.right;yield return .45f;p.Move=Vector2.zero;
            Check(Vector2.Distance(origin,p.transform.position)>.7f,"physics movement");
            float hp=p.CurrentHp;p.OnAttacked(30);Check(p.CurrentHp==hp-30,"Creature damage");Check(p.Heal()&&p.CurrentHp==p.MaxHp,"limited potion recovery");
            Check(p.Cast(ArcaneSpell.Summon,p.transform.position+Vector3.right),"summon spends mana");Check(p.Mana<=70.1f&&s.Golem,"summon Creature instantiated");Check(!p.Cast(ArcaneSpell.Summon,p.transform.position),"cooldown rejects repeated summon");
            p.transform.position=new Vector2(-13,-7);var target=s.Spawn(EnemyRole.Imp,"verify-bolt",new Vector2(-11.6f,-7));float enemyHp=target.CurrentHp;
            Check(p.Cast(ArcaneSpell.Bolt,target.transform.position),"bolt command");yield return .35f;
            Check(target.CurrentHp<enemyHp,"swept bolt damages enemy");int rewards=s.RewardsIssued;target.OnAttacked(9999);target.OnAttacked(9999);
            Check(s.RewardsIssued==rewards+1,"enemy death grants reward once");
            if(s.Golem)UnityEngine.Object.Destroy(s.Golem.gameObject);yield return .1f;
            p.transform.position=ArcaneSession.Camp;p.InvulnerableUntil=0;float beforeEnemy=p.CurrentHp;
            var attacker=s.Spawn(EnemyRole.Imp,"verify-natural-attack",ArcaneSession.Camp+Vector2.right*.7f);
            yield return 1.6f;Check(p.CurrentHp<beforeEnemy,"state machine telegraph resolves into real damage");attacker.OnAttacked(9999);p.Rest();
            yield return .35f;
            var flying=s.Spawn(EnemyRole.Wisp,"verify-air",new Vector2(-11.6f,-7));flying.ApplyControl(2,0,0);yield return .05f;
            Vector2 airAim=s.ResolveAim(flying.View.BodyPosition);Check(Vector2.Distance(airAim,flying.transform.position)<.01f,"flying body aim resolves to ground hitbox");
            float airHp=flying.CurrentHp;p.Cast(ArcaneSpell.Bolt,airAim);yield return .35f;Check(flying.CurrentHp<airHp,"visible flying target receives bolt");flying.OnAttacked(9999);
            p.Book.Save.research=20;Check(p.Book.Upgrade(ArcaneSpell.Bolt,false)&&p.Book.Rank(ArcaneSpell.Bolt)==2,"spell rank upgrade");
            Check(!p.Book.Upgrade(ArcaneSpell.Bolt,true),"combat upgrade forbidden");
            p.Book.Unlock(ArcaneSpell.Lightning);var distant=s.Spawn(EnemyRole.Brute,"verify-lightning",new Vector2(-10,-7));distant.ApplyControl(2,0,0);
            float distantHp=distant.CurrentHp;p.Cast(ArcaneSpell.Lightning,distant.transform.position);Check(distant.CurrentHp<distantHp,"lightning strikes aimed point outside player radius");distant.OnAttacked(9999);
            p.Book.Unlock(ArcaneSpell.Frost);Check(p.Book.Equip(0,ArcaneSpell.Frost,false),"equip unlocked spell");Check(!p.Book.Equip(1,ArcaneSpell.Bolt,false),"innate spell cannot occupy active slot");
            p.Book.Unlock(ArcaneSpell.Ward);yield return .4f;p.Cast(ArcaneSpell.Ward,p.transform.position);float shield=p.CurrentShield;p.InvulnerableUntil=0;p.OnAttacked(10);Check(shield>0&&p.CurrentHp==p.MaxHp&&p.CurrentShield==shield-10,"ward absorbs damage");
            p.Book.Save.phase=0;p.transform.position=new Vector2(-4,-1);s.Interact();s.Pause(false);Check(p.Book.Save.phase==1,"root investigation starts quest");
            foreach(var e in s.Enemies.ToArray())if(e.SpawnId.StartsWith("root-"))e.OnAttacked(9999);
            Check(p.Book.Save.phase==2&&p.Book.Rank(ArcaneSpell.Lightning)>0,"root encounter grants lightning");
            foreach(var e in s.Enemies.ToArray())if(e.SpawnId.StartsWith("brook-")||e.SpawnId.StartsWith("bridge-"))e.OnAttacked(9999);
            p.transform.position=new Vector2(-13,6);s.Interact();s.Pause(false);p.transform.position=new Vector2(5,1);s.Interact();s.Pause(false);
            Check(p.Book.Save.phase==3&&p.Book.Rank(ArcaneSpell.Ward)>0,"survey objectives open ruin chapter");
            foreach(var e in s.Enemies.ToArray())if(e.Boss)e.OnAttacked(9999);
            p.transform.position=ArcaneSession.Camp;s.Interact();s.Pause(false);Check(p.Book.Save.phase==5,"boss and report complete first field");
            s.Save();int research=p.Book.Save.research;
            for(int i=0;i<2;i++){
                SceneManager.LoadScene("RootBoundary");yield return .5f;
                s=UnityEngine.Object.FindObjectOfType<ArcaneSession>();s.GetComponent<ArcaneInput>().enabled=false;p=s.Player;
                Check(p.Book.Save.phase==5&&p.Book.Save.research==research,"scene reentry preserves progress "+(i+1));
                Check(!s.Enemies.Exists(e=>e.Boss),"defeated boss stays defeated "+(i+1));
            }
            p.InvulnerableUntil=0;p.OnAttacked(9999);Check(p.Dead,"player enters defeated state");yield return 1.5f;
            Check(!p.Dead&&p.CurrentHp==p.MaxHp&&Vector2.Distance(p.transform.position,ArcaneSession.Camp)<.1f,"death returns to camp with progress");
            Debug.Log("ARCANE_PLAYMODE_OK: gameplay commands, quest, growth and two scene reentries passed");
        }
        private static void Finish(int code){PlayerPrefs.DeleteKey(SessionState.GetString(Key,""));PlayerPrefs.Save();SessionState.SetBool(Active,false);EditorApplication.update-=Tick;SceneManager.sceneLoaded-=OnSceneLoaded;EditorApplication.Exit(code);}
    }
}
