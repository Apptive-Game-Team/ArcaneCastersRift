using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneCasters.DataSystem;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // Chapter 2: Fin's field study at the stone golem village (docs/story/chapter-02-stone-village.md).
    // Rock-tribe characters never speak in sentences; the voice at the end gives one command and no source.
    public sealed class StoneVillageChapter : ArcaneChapter {
        public const string ChapterId = "stone-village";
        public const string Mok = "모크", MiniRock = "미니록", BigGolem = "큰 돌골렘";
        private const int Gate = 0, Play = 1, Observe = 2, Follow = 3, Cave = 4, Hold = 5, Ended = 6;

        private static readonly Encounter[] Roster = {
            new Encounter('1', EnemyRole.Imp, "path-1", Follow), new Encounter('2', EnemyRole.Spitter, "path-2", Follow),
            new Encounter('3', EnemyRole.Imp, "cave-1", Follow), new Encounter('4', EnemyRole.Charger, "cave-2", Follow),
            new Encounter('5', EnemyRole.Wisp, "cave-3", Follow),
        };
        // Fixed waves at the opened door: three groups, then the field falls silent.
        private static readonly EnemyRole[][] Waves = {
            new[] { EnemyRole.Imp, EnemyRole.Imp, EnemyRole.Spitter },
            new[] { EnemyRole.Charger, EnemyRole.Imp, EnemyRole.Wisp },
            new[] { EnemyRole.Brute, EnemyRole.Wisp, EnemyRole.Spitter, EnemyRole.Imp },
        };

        private ArcaneActorView bigGolem, mok, door;
        private readonly List<ArcaneActorView> miniRocks = new List<ArcaneActorView>();
        private readonly List<SpriteRenderer> playStones = new List<SpriteRenderer>();
        private readonly Queue<Vector2> mokRoute = new Queue<Vector2>();
        private bool wavesRunning;

        public override string Id => ChapterId;
        public override string Title => "2장 · 돌골렘 마을";
        public override string PlayerName => ArcaneSession.Fin;
        public override string PlayerFrames => "fin";
        public override ArcaneField.Biome Biome => ArcaneField.Biome.Highland;
        public override Encounter[] Encounters => Roster;
        public override Vector2 RespawnPoint => Progress.phase >= Follow ? Field.Marker('M') + Vector2.down * 1.6f : Field.Marker('P');

        private static DialogueLine F(string text) => Line(ArcaneSession.Fin, text);
        private bool Flag(string id) => Progress.flags != null && Progress.flags.Contains(id);
        private void SetFlag(string id) { if (!Flag(id)) Progress.flags = (Progress.flags ?? new string[0]).Concat(new[] { id }).ToArray(); }
        private int StonesRolled => Enumerable.Range(0, 3).Count(i => Flag("stone-" + i));
        private int Observed => (Flag("quarry") ? 1 : 0) + (Flag("spring") ? 1 : 0);

        public override void Begin() {
            Field.SetSealOpen(Progress.phase >= Play);
            Vector2 gate = Field.Marker('B');
            bigGolem = Npc("ally-golem", Progress.phase >= Play ? gate + Vector2.right * 2.4f : gate, 1.45f, true);
            foreach (var spot in Field.Markers('m')) miniRocks.Add(Npc("mok", spot, .85f, false));
            mok = Npc("mok", Progress.phase >= Follow ? Field.Marker('k') : Field.Marker('K'), 1f, false);
            mok.windup = Progress.phase >= Follow;
            door = Npc("portal", Field.Marker('D'), 1f, false);
            door.windup = Progress.phase >= Hold;
            var doorBody = door.gameObject.AddComponent<BoxCollider2D>();
            door.gameObject.layer = LayerMask.NameToLayer("Wall");
            doorBody.size = new Vector2(3.2f, .8f);
            doorBody.offset = new Vector2(0, .4f);
            var stones = Field.Markers('o');
            for (int i = 0; i < stones.Count; i++) {
                playStones.Add(Stone(stones[i], Flag("stone-" + i)));
                AddTarget("stone-" + i, stones[i], 1.5f, 1.0f);
            }
            AddTarget("gate", gate, 2.4f, 2.6f);
            AddTarget("quarry", Field.Marker('Q'), 2.0f, 2.4f);
            AddTarget("spring", Field.Marker('S'), 2.0f, 2.4f);
            AddTarget("door", Field.Marker('D') + Vector2.down * 1.4f, 3.2f, 3.4f);
            if (!Progress.briefed) Session.After(.9f, Arrival);
            if (Progress.phase == Hold) Session.After(.5f, () => Session.StartCoroutine(RunWaves()));
            if (Progress.phase >= Ended) Session.After(.6f, ShowEnding);
        }

        private ArcaneActorView Npc(string frames, Vector2 at, float scale, bool facesRight) {
            var view = new GameObject(frames + " npc").AddComponent<ArcaneActorView>();
            view.transform.position = at;
            view.transform.localScale = Vector3.one * scale;
            view.Init(Session.Assets, frames, 0, facesRight);
            view.facing = Vector2.down;
            return view;
        }

        private SpriteRenderer Stone(Vector2 at, bool rolled) {
            var render = new GameObject("Play stone").AddComponent<SpriteRenderer>();
            render.sprite = Session.Assets.Prop("boulders");
            render.sharedMaterial = Session.Assets.material;
            render.transform.position = at;
            render.transform.localScale = Vector3.one * .32f;
            render.sortingOrder = ArcaneSorting.At(at.y);
            render.enabled = !rolled;
            return render;
        }

        private void Arrival() {
            if (Progress.briefed) return;
            if (!Session.CanTalk) { Session.After(.3f, Arrival); return; }
            Progress.briefed = true;
            Session.Save();
            Session.Talk(() => Session.Toast("WASD 이동 · 마우스 조준 · 좌클릭 마력탄 · Space 회피 · F 조사"),
                F("여기가 돌골렘 마을이구나. 미라는 지금쯤 세계수에 도착했으려나."),
                F("선생님이 직접 보고 쓰라고 했지. 수첩… 응, 미라가 빌려준 거 있다."));
        }

        public override void Tick() {
            if (!Session.Player || !mok) return;
            Vector2 p = Session.Player.transform.position;
            if (bigGolem) bigGolem.facing = p - (Vector2)bigGolem.transform.position;
            foreach (var mini in miniRocks) {
                if (!mini) continue;
                mini.facing = p - (Vector2)mini.transform.position;
            }
            MoveMok(p);
            if (Progress.phase == Follow && Field.IsCave(p) && p.y > Field.Marker('M').y + 1f) {
                Progress.phase = Cave;
                Session.Toast("동굴 안이 어두워. 모크를 찾자.");
                Session.Save();
            }
        }

        private void MoveMok(Vector2 player) {
            Vector2 at = mok.transform.position;
            Vector2 target;
            float speed;
            if (mokRoute.Count > 0) {
                target = mokRoute.Peek();
                speed = 3.4f;
                if (Vector2.Distance(at, target) < .1f) { mokRoute.Dequeue(); return; }
            } else if (Progress.phase == Observe) {
                Vector2 offset = (at - player).sqrMagnitude > .01f ? (at - player).normalized * 1.3f : Vector2.right * 1.3f;
                target = player + offset;
                speed = Vector2.Distance(at, target) > 4 ? 6f : 3f;
            } else {
                mok.moving = false;
                return;
            }
            Vector2 next = Vector2.MoveTowards(at, target, speed * Time.deltaTime);
            mok.moving = (next - at).sqrMagnitude > .00001f;
            if (mok.moving) mok.facing = next - at;
            mok.transform.position = next;
        }

        public override string Objective {
            get {
                switch (Progress.phase) {
                    case Gate: return "마을 입구의 큰 돌골렘에게 다가가기";
                    case Play: return $"미니록들과 돌 굴리기 놀이 ({StonesRolled}/3)";
                    case Observe: return $"돌골렘 움직임 관찰하기 ({Observed}/2)";
                    case Follow: return "모크를 따라 동굴로";
                    case Cave: return "동굴 안쪽 확인하기";
                    case Hold: return "열린 문 앞에서 버티기";
                    default: return "핀의 기록은 여기서 끊겼다";
                }
            }
        }

        public override bool HasGoal => Progress.phase < Hold;

        public override Vector2 Goal {
            get {
                switch (Progress.phase) {
                    case Gate: return Field.Marker('B');
                    case Play:
                        var stones = Field.Markers('o');
                        Vector2 p = Session.Player.transform.position;
                        return Enumerable.Range(0, stones.Count).Where(i => !Flag("stone-" + i)).Select(i => stones[i])
                                         .OrderBy(s => Vector2.Distance(s, p)).DefaultIfEmpty(Field.Marker('K')).First();
                    case Observe: return !Flag("quarry") ? Field.Marker('Q') : Field.Marker('S');
                    case Follow: return Field.Marker('M');
                    default: return Field.Marker('D');
                }
            }
        }

        public override bool IsDark(Vector2 point) => Field.IsCave(point);

        public override bool Available(Target target) {
            switch (target.Id) {
                case "gate": return Progress.phase == Gate;
                case "quarry": return Progress.phase == Observe && !Flag("quarry");
                case "spring": return Progress.phase == Observe && !Flag("spring");
                case "door": return Progress.phase == Cave;
                default: return target.Id.StartsWith("stone-") && Progress.phase == Play && !Flag(target.Id);
            }
        }

        public override string Label(Target target) {
            switch (target.Id) {
                case "gate": return "큰 돌골렘에게 인사";
                case "quarry": return "채석장 골렘 관찰";
                case "spring": return "샘가 골렘 관찰";
                case "door": return "모크 붙잡기";
                default: return "돌 굴리기";
            }
        }

        public override void Interact(Target target) {
            switch (target.Id) {
                case "gate":
                    Session.Talk(OpenGate,
                        Line(BigGolem, "우웅."),
                        F("안녕하세요. 연구하러 왔어요. …지나가도 될까요?"),
                        Line(BigGolem, "우웅."));
                    break;
                case "quarry":
                    if (Session.InCombat) return;
                    SetFlag("quarry");
                    Session.Talk(() => { Session.Unlock(ArcaneSpell.Ward); AfterObservation(); },
                        F("돌을 올릴 때마다 마력이 발끝으로 내려가네. 무게를 땅에 맡기는 건가?"),
                        F("기록, 기록. …이 흐름이면 카드로 막을 수도 있겠다."));
                    break;
                case "spring":
                    if (Session.InCombat) return;
                    SetFlag("spring");
                    Session.Talk(() => { Session.Unlock(ArcaneSpell.Frost); AfterObservation(); },
                        F("떨어진 팔이 다시 붙을 때 흐름이 끊기지 않아. 탑에서 본 거랑 달라."),
                        F("물이 차가울수록 천천히 붙네. 이건 미라한테 보내 줘야지."));
                    break;
                case "door":
                    if (Session.InCombat) { Session.Toast("주변의 불씨부터 막자."); return; }
                    Session.Talk(OpenDoor,
                        F("모크! 거기 있었구나. 이리 와, 같이 나가자."),
                        Line(Mok, "꾸…"),
                        F("잠깐, 그건…!"));
                    break;
                default:
                    RollStone(target);
                    break;
            }
        }

        private void OpenGate() {
            if (Progress.phase != Gate) return;
            Progress.phase = Play;
            Session.StartCoroutine(Slide(bigGolem.transform, (Vector2)bigGolem.transform.position + Vector2.right * 2.4f, .9f));
            Field.SetSealOpen(true);
            Session.Toast("큰 돌골렘이 길을 비켜 줬다.");
            Session.Save();
        }

        private void RollStone(Target target) {
            int index = int.Parse(target.Id.Substring("stone-".Length));
            SetFlag(target.Id);
            var stone = playStones[index];
            Session.StartCoroutine(RollTo(stone, mok.transform.position));
            Session.FloatText((Vector2)mok.transform.position + Vector2.up * .9f, "꾸!", new Color(1f, .9f, .6f));
            Session.Audio.Click();
            if (StonesRolled == 1) Session.Talk(null, F("이번엔 누가 먼저 잡을래?"), Line(MiniRock, "꾸!"));
            if (StonesRolled < 3) { Session.Save(); return; }
            Session.Talk(() => { Progress.phase = Observe; Session.Toast("모크가 따라온다. 돌골렘이 움직이는 모습을 관찰하자."); Session.Save(); },
                F("또 너야? 세 번 다 네가 잡았네."),
                Line(Mok, "꾸?"),
                F("그럼 넌 모크 하자. 모크. 마음에 들어?"),
                Line(Mok, "꾸!"));
            Session.Save();
        }

        private void AfterObservation() {
            Session.Save();
            if (Observed < 2) return;
            Session.Talk(MokRunsOff,
                Line(Mok, "꾸…"),
                F("모크? 눈이 왜 그래."),
                F("어디 가? 같이 가!"));
        }

        private void MokRunsOff() {
            Progress.phase = Follow;
            mok.windup = true;
            mokRoute.Clear();
            foreach (var point in new[] { Field.Marker('K'), new Vector2(-2f, 4.5f), Field.Marker('M'), new Vector2(-2f, 10f), new Vector2(-9f, 11f),
                                          new Vector2(-9.5f, 13.5f), new Vector2(-2f, 14f), new Vector2(3.5f, 12f), Field.Marker('k') })
                mokRoute.Enqueue(point);
            Session.SpawnUnlockedEncounters(true);
            Session.Toast("땅 틈에서 불씨가 새어 나온다! 모크를 따라가자.");
            Session.Save();
        }

        private void OpenDoor() {
            if (Progress.phase != Cave) return;
            Progress.phase = Hold;
            door.windup = true;
            Session.Shake(.5f);
            Session.Audio.Slam(true);
            ArcaneEffects.Sprite(Session.Assets, "spark", Field.Marker('D') + Vector2.up * 1.8f, 3.4f, .6f, 0, 22000, new Color(1f, .4f, .25f));
            Session.Toast("문이 열렸다! 불지옥의 생물이 몰려온다. 버티자!");
            Session.Save();
            Session.StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves() {
            if (wavesRunning) yield break;
            wavesRunning = true;
            var spawns = Field.Markers('w');
            while (Progress.step < Waves.Length) {
                int wave = Progress.step;
                yield return new WaitForSeconds(wave == 0 ? 1.2f : 2f);
                var roles = Waves[wave];
                var spawned = new List<ArcaneEnemy>();
                for (int i = 0; i < roles.Length; i++) {
                    Vector2 at = spawns[i % spawns.Count] + new Vector2((i / spawns.Count) * .9f, 0);
                    string id = $"wave-{wave + 1}-{i + 1}";
                    if (Session.AllDefeated(id)) continue;
                    var enemy = Session.Spawn(roles[i], id, at);
                    if (enemy) spawned.Add(enemy);
                    ArcaneEffects.Sprite(Session.Assets, "spark", at + Vector2.up * .4f, 1.4f, .4f, 0, 22000, new Color(1f, .45f, .25f));
                }
                Session.Toast($"{wave + 1}번째 무리 ({wave + 1}/{Waves.Length})");
                while (spawned.Any(e => e && !e.Dead)) yield return null;
                Progress.step = wave + 1;
                Session.Save();
            }
            Progress.phase = Ended;
            Session.Save();
            yield return new WaitForSeconds(1.2f);
            ShowEnding();
        }

        private void ShowEnding() => Session.ShowEnding("세계수로 향해라.", "핀의 기록은 여기서 끊겼다", "다음 기록 — 세계수");

        public override void PlayerRespawned() {
            if (Progress.phase == Hold && !wavesRunning) Session.StartCoroutine(RunWaves());
        }

        public override void SpeakerChanged(string speaker) {
            if (bigGolem) bigGolem.talking = false;
        }

        public override Sprite Portrait(string speaker) {
            switch (speaker) {
                case Mok: case MiniRock: return FrameOf("mok", speaker == Mok && Progress.phase >= Follow ? 1 : 0);
                case BigGolem: return FrameOf("ally-golem", 0);
                default: return base.Portrait(speaker);
            }
        }

        private Sprite FrameOf(string set, int index) {
            var frames = Session.Assets.Frames(set);
            return frames.Length > index ? frames[index] : null;
        }

        private static IEnumerator Slide(Transform target, Vector2 to, float seconds) {
            Vector2 from = target.position;
            for (float t = 0; t < seconds; t += Time.deltaTime) {
                target.position = Vector2.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
            target.position = to;
        }

        private static IEnumerator RollTo(SpriteRenderer stone, Vector2 to) {
            Vector2 from = stone.transform.position;
            for (float t = 0; t < .6f; t += Time.deltaTime) {
                stone.transform.position = Vector2.Lerp(from, to, t / .6f);
                stone.transform.Rotate(0, 0, -540 * Time.deltaTime);
                stone.sortingOrder = ArcaneSorting.At(stone.transform.position.y);
                yield return null;
            }
            stone.enabled = false;
        }
    }
}
