using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneCasters.DataSystem;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    public readonly struct DialogueLine {
        public readonly string Speaker, Text;
        public DialogueLine(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    // The first field run: progress, research targets, encounters and save data.
    public sealed class ArcaneSession : MonoBehaviour {
        public const string Mira = "미라", Orin = "오린";

        private readonly struct Encounter {
            public readonly char Marker; public readonly EnemyRole Role; public readonly string Id; public readonly int Gate;
            public Encounter(char marker, EnemyRole role, string id, int gate) { Marker = marker; Role = role; Id = id; Gate = gate; }
        }

        // Ids are save keys; gate is the story phase from which the encounter exists.
        private static readonly Encounter[] Encounters = {
            new Encounter('1', EnemyRole.Imp, "root-1", 1), new Encounter('2', EnemyRole.Imp, "root-2", 1), new Encounter('3', EnemyRole.Spitter, "root-3", 1),
            new Encounter('x', EnemyRole.Brute, "grove-1", 0), new Encounter('y', EnemyRole.Charger, "grove-2", 0),
            new Encounter('4', EnemyRole.Wisp, "brook-1", 0), new Encounter('5', EnemyRole.Spitter, "brook-2", 0),
            new Encounter('6', EnemyRole.Charger, "bridge-1", 0), new Encounter('7', EnemyRole.Brute, "bridge-2", 0),
            new Encounter('8', EnemyRole.Wisp, "ruin-1", 3), new Encounter('9', EnemyRole.Spitter, "ruin-2", 3), new Encounter('W', EnemyRole.Warden, "warden", 3),
        };

        public sealed class Target {
            public string Id;
            public Vector2 Position;
            public float Radius, PromptHeight;
        }

        public ArcaneAssets Assets;
        public string SaveKey = "ArcaneCastersRift.Unity.v1";
        public ArcaneField Field { get; private set; }
        public ArcaneCaster Player { get; private set; }
        public ArcaneGolem Golem;
        public readonly List<ArcaneEnemy> Enemies = new List<ArcaneEnemy>();
        public readonly List<Target> Targets = new List<Target>();
        public bool Paused { get; private set; }
        public string UiMode = "play";
        public bool InCombat => Player && Enemies.Any(e => e && !e.Dead && e.Alerted && Vector2.Distance(e.transform.position, Player.transform.position) < 7);
        public LayerMask Walls => LayerMask.GetMask("Wall");
        public Vector2 StartPoint => Field.Marker('P');
        public Vector2 OrinPoint => Field.Marker('O');
        public ArcaneAudio Audio { get; private set; }
        public float ShakeUntil;
        public int RewardsIssued { get; private set; }

        public event Action<string> OnToast;
        public event Action<IReadOnlyList<DialogueLine>, Action> OnDialogue;
        public event Action<Vector2, string, Color> OnFloatText;
        public event Action<float> OnPlayerHurt;
        public event Action OnPlayerDown, OnPlayerRespawn;

        private HashSet<string> defeated;
        private ArcaneActorView orin;
        private SpriteRenderer compass;
        private LineRenderer beacon;

        private ArcaneSave Progress => Player.Book.Save;

        public string Objective {
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

        public bool HasGoal => Progress.phase < 5;

        public Vector2 Goal {
            get {
                var save = Progress;
                switch (save.phase) {
                    case 0: case 1: return Field.Marker('G');
                    case 2:
                        if (save.brook) return Field.Marker('T');
                        if (save.rocks) return Field.Marker('C');
                        Vector2 p = Player.transform.position;
                        return Vector2.Distance(p, Field.Marker('C')) < Vector2.Distance(p, Field.Marker('T')) ? Field.Marker('C') : Field.Marker('T');
                    case 3: return Field.Marker('W');
                    default: return OrinPoint;
                }
            }
        }

        private void Start() {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--arcane-capture-dir") >= 0) SaveKey = "ArcaneCasters.Capture." + Guid.NewGuid().ToString("N");
            int saveKeyIndex = Array.IndexOf(args, "--arcane-save-key");
            if (saveKeyIndex >= 0 && saveKeyIndex + 1 < args.Length) SaveKey = "ArcaneCasters.Test." + args[saveKeyIndex + 1];
#endif
            Audio = gameObject.AddComponent<ArcaneAudio>();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            gameObject.AddComponent<ArcaneRenderCapture>();
            gameObject.AddComponent<ArcaneTelemetry>();
#endif
            Field = ArcaneField.Create(Assets, transform);
            ArcaneSave save = null;
            try { if (PlayerPrefs.HasKey(SaveKey)) save = JsonUtility.FromJson<ArcaneSave>(PlayerPrefs.GetString(SaveKey)); }
            catch (Exception) { Toast("연구 기록을 읽지 못했어. 새 기록으로 시작할게."); }
            var book = new ArcaneSpellBook(Assets.spells, save);
            defeated = new HashSet<string>(book.Save.defeated);
            Player = new GameObject("Mira").AddComponent<ArcaneCaster>();
            Player.transform.position = StartPoint;
            Player.Init(this, book);
            Field.Follow(Player.transform);
            Field.SetSealOpen(book.Save.phase >= 3);
            orin = new GameObject("Orin").AddComponent<ArcaneActorView>();
            orin.transform.position = OrinPoint;
            orin.Init(Assets, "orin", 0, facesRight: true);
            BuildTargets();
            SpawnUnlockedEncounters(false);
            BuildGuides();
            if (!book.Save.briefed) Invoke(nameof(Briefing), .9f);
        }

        private void BuildTargets() {
            Targets.Add(new Target { Id = "orin", Position = OrinPoint, Radius = 1.9f, PromptHeight = 2.2f });
            Targets.Add(new Target { Id = "root", Position = Field.Marker('G'), Radius = 2.6f, PromptHeight = .9f });
            Targets.Add(new Target { Id = "brook", Position = Field.Marker('C'), Radius = 1.9f, PromptHeight = 1.9f });
            Targets.Add(new Target { Id = "stones", Position = Field.Marker('T'), Radius = 1.9f, PromptHeight = 1.9f });
            Targets.Add(new Target { Id = "vine", Position = Field.Marker('V'), Radius = 1.8f, PromptHeight = 1.4f });
            Targets.Add(new Target { Id = "seal", Position = Field.SealCenter, Radius = 1.8f, PromptHeight = 1.2f });
        }

        private void BuildGuides() {
            compass = new GameObject("Goal direction").AddComponent<SpriteRenderer>();
            compass.sprite = ArrowSprite();
            compass.sharedMaterial = Assets.material;
            compass.color = new Color(1, .64f, .16f, .9f);
            compass.sortingOrder = ArcaneSorting.Telegraph + 10;
            compass.transform.localScale = Vector3.one * .42f;
            beacon = new GameObject("Goal beacon").AddComponent<LineRenderer>();
            beacon.sharedMaterial = Assets.material;
            beacon.positionCount = 2;
            beacon.startWidth = .5f;
            beacon.endWidth = .05f;
            beacon.startColor = new Color(1, .7f, .25f, .45f);
            beacon.endColor = new Color(1, .7f, .25f, 0);
            beacon.sortingOrder = 30000;
        }

        private void Update() {
            if (!Player || !compass) return;
            bool show = HasGoal && !Paused && !Player.Dead;
            Vector2 goal = Goal, p = Player.transform.position;
            Vector2 direction = (goal - p).normalized;
            float distanceToGoal = Vector2.Distance(goal, p);
            compass.enabled = show && !InCombat && distanceToGoal > 3;
            compass.transform.position = p + direction * 1.1f + Vector2.up * .1f;
            compass.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
            beacon.enabled = show && distanceToGoal > 5;
            float pulse = .35f + .15f * Mathf.Sin(Time.time * 2.5f);
            beacon.startColor = new Color(1, .7f, .25f, pulse);
            beacon.SetPosition(0, goal + Vector2.up * .1f);
            beacon.SetPosition(1, goal + Vector2.up * 3.6f);
            if (orin) {
                Vector2 toPlayer = p - OrinPoint;
                orin.facing = toPlayer.sqrMagnitude > .01f ? toPlayer : Vector2.down;
            }
        }

        private static Sprite ArrowSprite() {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                float half = (1 - y / (float)(size - 1)) * size / 2f;
                for (int x = 0; x < size; x++) {
                    float d = Mathf.Abs(x + .5f - size / 2f);
                    bool notch = y < size * .35f && d < (size * .35f - y) * .6f;
                    byte a = notch ? (byte)0 : (byte)(Mathf.Clamp01(half - d + .5f) * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        }

        // ---------- dialogue ----------
        private void Talk(Action then, params DialogueLine[] lines) {
            if (OnDialogue == null) { then?.Invoke(); return; }
            OnDialogue.Invoke(lines, then);
        }

        private static DialogueLine M(string text) => new DialogueLine(Mira, text);
        private static DialogueLine O(string text) => new DialogueLine(Orin, text);

        public void SetSpeaker(string speaker) { if (orin) orin.talking = speaker == Orin; }

        private void Briefing() {
            if (Progress.briefed) return;
            if (OnDialogue == null || Paused) { Invoke(nameof(Briefing), .3f); return; }
            Progress.briefed = true;
            Save();
            Talk(() => Toast("WASD 이동 · 마우스 조준 · 좌클릭 마력탄 · Space 회피 · F 조사"),
                O("왔구나, 미라. 오늘은 세계수 뿌리의 마력부터 재 보거라."),
                M("네, 선생님. 핀은요?"),
                O("돌부족 마을로 갔다. 자네들 기록은 돌아와서 나란히 놓고 보자꾸나."),
                M("그럼 제 쪽이 먼저 정리돼 있어야겠네요. 다녀올게요."));
        }

        // ---------- interaction ----------
        public Target CurrentTarget {
            get {
                if (!Player) return null;
                Vector2 p = Player.transform.position;
                Target best = null;
                float bestDistance = float.MaxValue;
                foreach (var target in Targets) {
                    if (!Available(target)) continue;
                    float distance = Vector2.Distance(p, target.Position);
                    if (distance > target.Radius || distance >= bestDistance) continue;
                    best = target;
                    bestDistance = distance;
                }
                return best;
            }
        }

        private bool Available(Target target) {
            var save = Progress;
            switch (target.Id) {
                case "root": return save.phase == 0;
                case "brook": return !save.brook;
                case "stones": return !save.rocks;
                case "vine": return !save.vine;
                case "seal": return !Field.SealOpen;
                default: return true;
            }
        }

        public string Interaction {
            get {
                var target = CurrentTarget;
                if (target == null) return "";
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
        }

        public void Interact() {
            var target = CurrentTarget;
            if (target == null || Paused || Player.Dead) return;
            var save = Progress;
            switch (target.Id) {
                case "orin": TalkToOrin(save); break;
                case "root":
                    Talk(RootAwakens,
                        M("잠깐. 뿌리에 열이 남아 있어. 이 숲에서 이런 열이 날 리가 없는데."),
                        M("…불씨? 뿌리 밑에서 뭔가 올라와."));
                    break;
                case "brook":
                    if (InCombat) { Toast("먼저 주변의 불씨를 정리하자."); return; }
                    save.brook = true;
                    Talk(() => { Unlock(ArcaneSpell.Frost); CheckProgress(); },
                        M("물가의 결정이 불씨를 식히고 있어. 조건은… 차가운 흐름이 계속 닿는 것."),
                        M("이 흐름을 카드에 옮겨 보자. 빙결 다음에 낙뢰를 쓰면 더 잘 들을 거야."));
                    break;
                case "stones":
                    if (InCombat) { Toast("먼저 주변을 안전하게 만들자."); return; }
                    save.rocks = true;
                    Talk(() => { Unlock(ArcaneSpell.Ward); CheckProgress(); },
                        M("돌부족의 보호 무늬야. 선이 유적 쪽으로 이어져 있어."),
                        M("핀이 봤으면 먼저 만져 봤겠지. …기록부터 하자."));
                    break;
                case "vine":
                    if (InCombat) { Toast("덩굴 근처가 아직 위험해."); return; }
                    save.vine = true;
                    Talk(() => Unlock(ArcaneSpell.Roots), M("덩굴이 움직이는 걸 붙잡아 두네. 표본으로 남겨 두자."));
                    break;
                case "seal":
                    Toast("유적이 봉인돼 있어. 개울과 돌무리부터 조사하자.");
                    break;
            }
            Save();
        }

        private void TalkToOrin(ArcaneSave save) {
            if (InCombat) { Toast("주변이 안전해지면 선생님께 가자."); return; }
            Player.Rest();
            if (save.phase == 4) {
                save.phase = 5;
                Save();
                Talk(() => { Toast("첫 현장 연구 완료 · 기록이 저장됐어."); Audio.Chime(); },
                    M("선생님, 다녀왔어요. 뿌리 밑에서 불씨가 올라왔고, 유적의 문지기가 길을 막고 있었어요."),
                    O("직접 본 것이냐?"),
                    M("네. 문지기 눈이 붉게 빛났어요. 개울 결정이랑 돌무리 무늬도 여기 적어 왔어요."),
                    O("흠. 여기까지는 잘했구나. 결론은 아직 적지 말거라."),
                    O("핀이 돌아오면 두 기록을 나란히 놓고 보자. 오늘은 쉬어라."));
                return;
            }
            if (save.phase >= 5) { Talk(null, O("쉬었다 가거라. 숲은 아직 넓다.")); return; }
            Talk(null, O(save.phase == 0 ? "뿌리는 길 따라 북동쪽이다. 서두르지 말고 살펴보거라." : "몸부터 추스르거라. 그다음은 어찌 되었느냐?"));
        }

        private void RootAwakens() {
            if (Progress.phase != 0) return;
            Progress.phase = 1;
            SpawnUnlockedEncounters(true);
            Toast("불씨가 깨어났다! 정리하자.");
            Save();
        }

        public void SpawnUnlockedEncounters(bool announce) {
            foreach (var encounter in Encounters) {
                if (Progress.phase < encounter.Gate || defeated.Contains(encounter.Id)) continue;
                if (Enemies.Any(e => e && e.SpawnId == encounter.Id)) continue;
                Vector2 at = Field.Marker(encounter.Marker);
                var enemy = Spawn(encounter.Role, encounter.Id, at);
                if (announce && enemy) ArcaneEffects.Sprite(Assets, "spark", at + Vector2.up * .4f, 1.2f, .4f, 0, 22000, new Color(1, .55f, .3f));
            }
        }

        private void CheckProgress() {
            var save = Progress;
            if (save.phase == 1 && new[] { "root-1", "root-2", "root-3" }.All(defeated.Contains)) {
                save.phase = 2;
                Unlock(ArcaneSpell.Lightning);
                Toast("뿌리의 불씨를 정리했어. 이제 개울과 돌무리를 조사하자.");
            }
            if (save.phase == 2 && save.brook && save.rocks) {
                save.phase = defeated.Contains("warden") ? 4 : 3;
                if (save.phase == 3) {
                    Field.SetSealOpen(true);
                    SpawnUnlockedEncounters(true);
                    Audio.Chime();
                    Toast("유적의 봉인이 풀렸어. 문지기를 확인하러 가자.");
                }
            }
        }

        private void Unlock(ArcaneSpell spell) {
            if (!Player.Book.Unlock(spell)) return;
            Player.Book.Save.research += 2;
            Audio.Chime();
            Toast(Player.Book.Definition(spell).name + " 카드 획득 · [K]에서 장착과 강화");
            Save();
        }

        // ---------- combat ----------
        public Vector2 ResolveAim(Vector2 cursor) {
            ArcaneEnemy best = null;
            float bestDistance = float.MaxValue;
            foreach (var enemy in Enemies) {
                if (!enemy || enemy.Dead) continue;
                var bounds = enemy.View.BodyBounds;
                bounds.Expand(.3f);
                bool onBody = bounds.Contains(new Vector3(cursor.x, cursor.y, bounds.center.z));
                bool onShadow = Vector2.Distance(enemy.transform.position, cursor) < enemy.HitRadius + .25f;
                if (!onBody && !onShadow) continue;
                float distance = Vector2.Distance(enemy.View.BodyPosition, cursor);
                if (distance < bestDistance) { best = enemy; bestDistance = distance; }
            }
            return best ? (Vector2)best.transform.position : cursor;
        }

        public ArcaneEnemy Spawn(EnemyRole role, string id, Vector2 position) {
            if (defeated.Contains(id)) return null;
            var enemy = new GameObject(id).AddComponent<ArcaneEnemy>();
            enemy.transform.position = position;
            enemy.Init(this, role, id);
            return enemy;
        }

        public ArcaneEnemy Nearest(Vector2 point, float range) =>
            Enemies.Where(e => e && !e.Dead && Vector2.Distance(point, e.transform.position) < range)
                   .OrderBy(e => Vector2.Distance(point, e.transform.position)).FirstOrDefault();

        public Vector2 Steer(Vector2 from, Vector2 to) {
            Vector2 direction = (to - from).normalized;
            foreach (float angle in new[] { 0, 35f, -35f, 70f, -70f, 110f, -110f }) {
                Vector2 turn = Quaternion.Euler(0, 0, angle) * direction;
                if (!Physics2D.CircleCast(from, .24f, turn, .65f, Walls)) return turn;
            }
            return Vector2.zero;
        }

        public void EnemyDefeated(ArcaneEnemy enemy) {
            if (!defeated.Add(enemy.SpawnId)) return;
            RewardsIssued++;
            Enemies.Remove(enemy);
            Player.Reward(enemy.Boss ? 150 : 35);
            Player.Book.Save.research += enemy.Boss ? 3 : 1;
            if (enemy.Boss) {
                Progress.phase = 4;
                Toast("문지기가 멈췄어. 야영지로 돌아가 선생님께 보고하자.");
            }
            CheckProgress();
            Save();
        }

        // Friendly area damage (golem splash, dash burst).
        public void Blast(Vector2 center, float radius, float damage, bool friendly) {
            ArcaneEffects.Ring(center, radius, new Color(.5f, 1, .8f, .8f), Assets.material, .25f);
            foreach (var enemy in Enemies.ToArray())
                if (enemy && !enemy.Dead && ArcaneHitShapes.InCircle(center, radius, enemy.transform.position, enemy.HitRadius)) enemy.OnAttacked(damage);
        }

        // Enemy area damage; the same circle the telegraph drew.
        public void EnemyBlast(Vector2 center, float radius, float damage) {
            if (ArcaneHitShapes.InCircle(center, radius, Player.transform.position, ArcaneHitShapes.PlayerRadius)) Player.OnAttacked(damage);
            if (Golem && ArcaneHitShapes.InCircle(center, radius, Golem.transform.position, .45f)) Golem.OnAttacked(damage);
        }

        public void Dodged(Vector2 at) {
            Audio.Dodged();
            FloatText(at + Vector2.up * 1.6f, "회피", new Color(.75f, .95f, 1f));
        }

        public void PlayerHurt(float amount) => OnPlayerHurt?.Invoke(amount);

        public void PlayerDown() {
            OnPlayerDown?.Invoke();
            Toast("쓰러졌어… 연구 기록은 무사해. 야영지에서 다시 시작하자.");
        }

        public void PlayerRespawned() {
            foreach (var enemy in Enemies.ToArray()) if (enemy && !enemy.Dead) enemy.ResetToHome();
            if (Golem) Destroy(Golem.gameObject);
            Save();
            OnPlayerRespawn?.Invoke();
        }

        public void Toast(string message) => OnToast?.Invoke(message);
        public void FloatText(Vector2 p, string text, Color color) => OnFloatText?.Invoke(p, text, color);
        public void Shake(float duration) => ShakeUntil = Time.unscaledTime + duration;
        public void Pause(bool value) { Paused = value; Time.timeScale = value ? 0 : 1; }

        public void Save() {
            if (!Player) return;
            Player.Book.Save.defeated = defeated.ToArray();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Player.Book.Save));
            PlayerPrefs.Save();
        }

        private void OnApplicationQuit() => Save();
        private void OnDestroy() { Time.timeScale = 1; }
    }
}
