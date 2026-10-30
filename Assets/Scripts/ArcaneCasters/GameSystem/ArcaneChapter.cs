using System;
using System.Collections.Generic;
using ArcaneCasters.DataSystem;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // Story rules of one playable chapter: who is played, where research targets and fights are,
    // what the objective is and what each interaction does. ArcaneSession owns the shared systems.
    public abstract class ArcaneChapter {
        public readonly struct Encounter {
            public readonly char Marker; public readonly EnemyRole Role; public readonly string Id; public readonly int Gate;
            public Encounter(char marker, EnemyRole role, string id, int gate) { Marker = marker; Role = role; Id = id; Gate = gate; }
        }

        public sealed class Target {
            public string Id;
            public Vector2 Position;
            public float Radius, PromptHeight;
        }

        public static ArcaneChapter Create(string id) {
            switch (id) {
                case StoneVillageChapter.ChapterId: return new StoneVillageChapter();
                default: return new RootBoundaryChapter();
            }
        }

        protected ArcaneSession Session { get; private set; }
        protected ArcaneSave Progress => Session.Progress;
        protected ArcaneField Field => Session.Field;

        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string PlayerName { get; }
        public abstract string PlayerFrames { get; }
        public abstract ArcaneField.Biome Biome { get; }
        // Chapter 1 keeps the original save key so existing saves stay valid.
        public virtual string SaveSuffix => "." + Id;
        public virtual Encounter[] Encounters => Array.Empty<Encounter>();
        public virtual Vector2 RespawnPoint => Field.Marker('P');
        public readonly List<Target> Targets = new List<Target>();

        public void Attach(ArcaneSession session) => Session = session;

        public abstract void Begin();
        public virtual void Tick() { }
        public abstract string Objective { get; }
        public abstract bool HasGoal { get; }
        public abstract Vector2 Goal { get; }
        public abstract bool Available(Target target);
        public abstract string Label(Target target);
        public abstract void Interact(Target target);
        public virtual void EnemyDefeated(ArcaneEnemy enemy) { }
        public virtual void SpeakerChanged(string speaker) { }
        public virtual void PlayerRespawned() { }
        public virtual bool IsDark(Vector2 point) => false;

        // Dialogue portrait for a speaker; rock-tribe speakers use their world sprite.
        public virtual Sprite Portrait(string speaker) {
            switch (speaker) {
                case ArcaneSession.Mira: return Session.Assets.Portrait("mira");
                case ArcaneSession.Orin: return Session.Assets.Portrait("orin");
                case ArcaneSession.Fin: return Session.Assets.Portrait("fin");
                default: return null;
            }
        }

        protected Target AddTarget(string id, Vector2 at, float radius, float promptHeight) {
            var target = new Target { Id = id, Position = at, Radius = radius, PromptHeight = promptHeight };
            Targets.Add(target);
            return target;
        }

        protected static DialogueLine Line(string speaker, string text) => new DialogueLine(speaker, text);
    }

    // Chapter 1: Mira's field study at the World Tree root.
    public sealed class RootBoundaryChapter : ArcaneChapter {
        public const string ChapterId = "root-boundary";
        private ArcaneActorView orin;

        public override string Id => ChapterId;
        public override string Title => "1장 · 세계수 뿌리";
        public override string PlayerName => ArcaneSession.Mira;
        public override string PlayerFrames => "mira";
        public override ArcaneField.Biome Biome => ArcaneField.Biome.Forest;
        public override string SaveSuffix => "";

        private static readonly Encounter[] Roster = {
            new Encounter('1', EnemyRole.Imp, "root-1", 1), new Encounter('2', EnemyRole.Imp, "root-2", 1), new Encounter('3', EnemyRole.Spitter, "root-3", 1),
            new Encounter('x', EnemyRole.Brute, "grove-1", 0), new Encounter('y', EnemyRole.Charger, "grove-2", 0),
            new Encounter('4', EnemyRole.Wisp, "brook-1", 0), new Encounter('5', EnemyRole.Spitter, "brook-2", 0),
            new Encounter('6', EnemyRole.Charger, "bridge-1", 0), new Encounter('7', EnemyRole.Brute, "bridge-2", 0),
            new Encounter('8', EnemyRole.Wisp, "ruin-1", 3), new Encounter('9', EnemyRole.Spitter, "ruin-2", 3), new Encounter('W', EnemyRole.Warden, "warden", 3),
        };
        public override Encounter[] Encounters => Roster;

        private Vector2 OrinPoint => Field.Marker('O');
        private static DialogueLine M(string text) => Line(ArcaneSession.Mira, text);
        private static DialogueLine O(string text) => Line(ArcaneSession.Orin, text);

        public override void Begin() {
            Field.SetSealOpen(Progress.phase >= 3);
            orin = new GameObject("Orin").AddComponent<ArcaneActorView>();
            orin.transform.position = OrinPoint;
            orin.Init(Session.Assets, "orin", 0, facesRight: true);
            AddTarget("orin", OrinPoint, 1.9f, 2.2f);
            AddTarget("root", Field.Marker('G'), 2.6f, .9f);
            AddTarget("brook", Field.Marker('C'), 1.9f, 1.9f);
            AddTarget("stones", Field.Marker('T'), 1.9f, 1.9f);
            AddTarget("vine", Field.Marker('V'), 1.8f, 1.4f);
            AddTarget("seal", Field.SealCenter, 1.8f, 1.2f);
            if (!Progress.briefed) Session.After(.9f, Briefing);
        }

        public override void Tick() {
            if (!orin || !Session.Player) return;
            Vector2 toPlayer = (Vector2)Session.Player.transform.position - OrinPoint;
            orin.facing = toPlayer.sqrMagnitude > .01f ? toPlayer : Vector2.down;
        }

        public override void SpeakerChanged(string speaker) { if (orin) orin.talking = speaker == ArcaneSession.Orin; }

        private void Briefing() {
            if (Progress.briefed) return;
            if (!Session.CanTalk) { Session.After(.3f, Briefing); return; }
            Progress.briefed = true;
            Session.Save();
            Session.Talk(() => Session.Toast("WASD 이동 · 마우스 조준 · 좌클릭 마력탄 · Space 회피 · F 조사"),
                O("왔구나, 미라. 오늘은 세계수 뿌리의 마력부터 재 보거라."),
                M("네, 선생님. 핀은요?"),
                O("돌부족 마을로 갔다. 자네들 기록은 돌아와서 나란히 놓고 보자꾸나."),
                M("그럼 제 쪽이 먼저 정리돼 있어야겠네요. 다녀올게요."));
        }

        public override string Objective {
            get {
                switch (Progress.phase) {
                    case 0: return "세계수 뿌리 조사하기";
                    case 1: return "뿌리 곁의 불씨 정리하기";
                    case 2: return !Progress.brook && !Progress.rocks ? "개울 결정과 돌무리 조사하기" : !Progress.brook ? "개울의 결정 조사하기" : "돌무리 조사하기";
                    case 3: return "유적의 문지기 제압하기";
                    case 4: return "야영지로 돌아가 선생님께 보고하기";
                    default: return "첫 현장 연구 완료 · 숲 더 둘러보기";
                }
            }
        }

        public override bool HasGoal => Progress.phase < 5;

        public override Vector2 Goal {
            get {
                switch (Progress.phase) {
                    case 0: case 1: return Field.Marker('G');
                    case 2:
                        if (Progress.brook) return Field.Marker('T');
                        if (Progress.rocks) return Field.Marker('C');
                        Vector2 p = Session.Player.transform.position;
                        return Vector2.Distance(p, Field.Marker('C')) < Vector2.Distance(p, Field.Marker('T')) ? Field.Marker('C') : Field.Marker('T');
                    case 3: return Field.Marker('W');
                    default: return OrinPoint;
                }
            }
        }

        public override bool Available(Target target) {
            switch (target.Id) {
                case "root": return Progress.phase == 0;
                case "brook": return !Progress.brook;
                case "stones": return !Progress.rocks;
                case "vine": return !Progress.vine;
                case "seal": return !Field.SealOpen;
                default: return true;
            }
        }

        public override string Label(Target target) {
            switch (target.Id) {
                case "orin": return Progress.phase == 4 ? "선생님께 보고" : "선생님과 이야기 · 휴식";
                case "root": return "세계수 뿌리 조사";
                case "brook": return "개울의 결정 조사";
                case "stones": return "돌무리 조사";
                case "vine": return "덩굴 표본 채집";
                case "seal": return "봉인 살펴보기";
                default: return "";
            }
        }

        public override void Interact(Target target) {
            switch (target.Id) {
                case "orin": TalkToOrin(); break;
                case "root":
                    Session.Talk(RootAwakens,
                        M("잠깐. 뿌리에 열이 남아 있어. 이 숲에서 이런 열이 날 리가 없는데."),
                        M("…불씨? 뿌리 밑에서 뭔가 올라와."));
                    break;
                case "brook":
                    if (Session.InCombat) { Session.Toast("먼저 주변의 불씨를 정리하자."); return; }
                    Progress.brook = true;
                    Session.Talk(() => { Session.Unlock(ArcaneSpell.Frost); CheckProgress(); },
                        M("물가의 결정이 불씨를 식히고 있어. 조건은… 차가운 흐름이 계속 닿는 것."),
                        M("이 흐름을 카드에 옮겨 보자. 빙결 다음에 낙뢰를 쓰면 더 잘 들을 거야."));
                    break;
                case "stones":
                    if (Session.InCombat) { Session.Toast("먼저 주변을 안전하게 만들자."); return; }
                    Progress.rocks = true;
                    Session.Talk(() => { Session.Unlock(ArcaneSpell.Ward); CheckProgress(); },
                        M("돌부족의 보호 무늬야. 선이 유적 쪽으로 이어져 있어."),
                        M("핀이 봤으면 먼저 만져 봤겠지. …기록부터 하자."));
                    break;
                case "vine":
                    if (Session.InCombat) { Session.Toast("덩굴 근처가 아직 위험해."); return; }
                    Progress.vine = true;
                    Session.Talk(() => Session.Unlock(ArcaneSpell.Roots), M("덩굴이 움직이는 걸 붙잡아 두네. 표본으로 남겨 두자."));
                    break;
                case "seal":
                    Session.Toast("유적이 봉인돼 있어. 개울과 돌무리부터 조사하자.");
                    break;
            }
        }

        private void TalkToOrin() {
            if (Session.InCombat) { Session.Toast("주변이 안전해지면 선생님께 가자."); return; }
            Session.Player.Rest();
            if (Progress.phase == 4) {
                Progress.phase = 5;
                Session.Save();
                Session.Talk(() => { Session.Toast("첫 현장 연구 완료 · 일시 정지 메뉴에서 2장 핀의 기록을 열 수 있어."); Session.Audio.Chime(); },
                    M("선생님, 다녀왔어요. 뿌리 밑에서 불씨가 올라왔고, 유적의 문지기가 길을 막고 있었어요."),
                    O("직접 본 것이냐?"),
                    M("네. 문지기 눈이 붉게 빛났어요. 개울 결정이랑 돌무리 무늬도 여기 적어 왔어요."),
                    O("흠. 여기까지는 잘했구나. 결론은 아직 적지 말거라."),
                    O("핀이 돌아오면 두 기록을 나란히 놓고 보자. 오늘은 쉬어라."));
                return;
            }
            if (Progress.phase >= 5) { Session.Talk(null, O("쉬었다 가거라. 숲은 아직 넓다.")); return; }
            Session.Talk(null, O(Progress.phase == 0 ? "뿌리는 길 따라 북동쪽이다. 서두르지 말고 살펴보거라." : "몸부터 추스르거라. 그다음은 어찌 되었느냐?"));
        }

        private void RootAwakens() {
            if (Progress.phase != 0) return;
            Progress.phase = 1;
            Session.SpawnUnlockedEncounters(true);
            Session.Toast("불씨가 깨어났다! 정리하자.");
            Session.Save();
        }

        public override void EnemyDefeated(ArcaneEnemy enemy) {
            if (enemy.Boss) {
                Progress.phase = 4;
                Session.Toast("문지기가 멈췄어. 야영지로 돌아가 선생님께 보고하자.");
            }
            CheckProgress();
        }

        private void CheckProgress() {
            if (Progress.phase == 1 && Session.AllDefeated("root-1", "root-2", "root-3")) {
                Progress.phase = 2;
                Session.Unlock(ArcaneSpell.Lightning);
                Session.Toast("뿌리의 불씨를 정리했어. 이제 개울과 돌무리를 조사하자.");
            }
            if (Progress.phase == 2 && Progress.brook && Progress.rocks) {
                Progress.phase = Session.AllDefeated("warden") ? 4 : 3;
                if (Progress.phase != 3) return;
                Field.SetSealOpen(true);
                Session.SpawnUnlockedEncounters(true);
                Session.Audio.Chime();
                Session.Toast("유적의 봉인이 풀렸어. 문지기를 확인하러 가자.");
            }
        }
    }
}
