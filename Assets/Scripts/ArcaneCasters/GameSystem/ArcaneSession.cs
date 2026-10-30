using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneCasters.DataSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneCasters.GameSystem {
    public readonly struct DialogueLine {
        public readonly string Speaker, Text;
        public DialogueLine(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    // Shared systems of a field run: player, field, enemies, combat, dialogue and save data.
    // The story rules of the chapter being played live in ArcaneChapter.
    public sealed class ArcaneSession : MonoBehaviour {
        public const string Mira = "미라", Orin = "오린", Fin = "핀";
        public static readonly (string id, string scene, string title)[] Chapters = {
            (RootBoundaryChapter.ChapterId, "RootBoundary", "1장 · 세계수 뿌리 (미라)"),
            (StoneVillageChapter.ChapterId, "StoneVillage", "2장 · 돌골렘 마을 (핀)"),
        };

        public ArcaneAssets Assets;
        public string ChapterId = RootBoundaryChapter.ChapterId;
        public string SaveKey = "ArcaneCastersRift.Unity.v1";
        public ArcaneChapter Chapter { get; private set; }
        public ArcaneField Field { get; private set; }
        public ArcaneCaster Player { get; private set; }
        public ArcaneGolem Golem;
        public readonly List<ArcaneEnemy> Enemies = new List<ArcaneEnemy>();
        public bool Paused { get; private set; }
        public bool Ended { get; private set; }
        public string UiMode = "play";
        public bool InCombat => Player && Enemies.Any(e => e && !e.Dead && e.Alerted && Vector2.Distance(e.transform.position, Player.transform.position) < 7);
        public LayerMask Walls => LayerMask.GetMask("Wall");
        public Vector2 StartPoint => Chapter.RespawnPoint;
        public Vector2 OrinPoint => Field.Marker('O');
        public ArcaneAudio Audio { get; private set; }
        public float ShakeUntil;
        public int RewardsIssued { get; private set; }
        public ArcaneSave Progress => Player.Book.Save;
        public bool CanTalk => OnDialogue != null && !Paused;
        // 0 outside, about .82 deep in a cave; the HUD draws it as a dark ring around the player.
        public float Darkness { get; private set; }

        public event Action<string> OnToast;
        public event Action<IReadOnlyList<DialogueLine>, Action> OnDialogue;
        public event Action<Vector2, string, Color> OnFloatText;
        public event Action<float> OnPlayerHurt;
        public event Action OnPlayerDown, OnPlayerRespawn;
        public event Action<string, string, string> OnEnding;

        private HashSet<string> defeated;
        private SpriteRenderer compass;
        private Mesh darkRing;
        private Transform darkRingTransform;
        private LineRenderer beacon;

        public string Objective => Chapter.Objective;
        public bool HasGoal => Chapter.HasGoal && !Ended;
        public Vector2 Goal => Chapter.Goal;

        private static bool chapterArgumentUsed;

        private void Start() {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // --arcane-chapter <id> opens another chapter at launch (verification and captures).
            string[] launch = Environment.GetCommandLineArgs();
            int chapterIndex = Array.IndexOf(launch, "--arcane-chapter");
            if (!chapterArgumentUsed && chapterIndex >= 0 && chapterIndex + 1 < launch.Length) {
                chapterArgumentUsed = true;
                if (launch[chapterIndex + 1] != ChapterId) { LoadChapter(launch[chapterIndex + 1]); return; }
            }
#endif
            Chapter = ArcaneChapter.Create(ChapterId);
            Chapter.Attach(this);
            SaveKey += Chapter.SaveSuffix;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--arcane-capture-dir") >= 0) SaveKey = "ArcaneCasters.Capture." + Guid.NewGuid().ToString("N");
            int saveKeyIndex = Array.IndexOf(args, "--arcane-save-key");
            if (saveKeyIndex >= 0 && saveKeyIndex + 1 < args.Length) SaveKey = "ArcaneCasters.Test." + args[saveKeyIndex + 1] + Chapter.SaveSuffix;
#endif
            Audio = gameObject.AddComponent<ArcaneAudio>();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            gameObject.AddComponent<ArcaneRenderCapture>();
            gameObject.AddComponent<ArcaneTelemetry>();
#endif
            Field = ArcaneField.Create(Assets, transform, Chapter.Id, Chapter.Biome);
            ArcaneSave save = null;
            try { if (PlayerPrefs.HasKey(SaveKey)) save = JsonUtility.FromJson<ArcaneSave>(PlayerPrefs.GetString(SaveKey)); }
            catch (Exception) { Toast("연구 기록을 읽지 못했어. 새 기록으로 시작할게."); }
            var book = new ArcaneSpellBook(Assets.spells, save);
            defeated = new HashSet<string>(book.Save.defeated);
            Player = new GameObject(Chapter.PlayerName).AddComponent<ArcaneCaster>();
            Player.Init(this, book, Chapter.PlayerFrames);
            Player.transform.position = Field.Marker('P');
            Field.Follow(Player.transform);
            Chapter.Begin();
            if (Progress.phase > 0 && Chapter.RespawnPoint != Field.Marker('P')) Player.transform.position = Chapter.RespawnPoint;
            SpawnUnlockedEncounters(false);
            BuildGuides();
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
            BuildDarkRing();
        }

        private void Update() {
            if (!Player || !compass) return;
            Chapter.Tick();
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
            // Caves close in around the player; outside the overlay fades away.
            Darkness = Mathf.MoveTowards(Darkness, Chapter.IsDark(p) ? .9f : 0f, Time.unscaledDeltaTime * 1.5f);
            darkRingTransform.position = p + Vector2.up * .6f;
            darkRingTransform.gameObject.SetActive(Darkness > .01f);
            if (Darkness > .01f) TintDarkRing(Darkness);
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

        // Vertex-coloured ring mesh: clear inside 2.8 units, dark from 5.6 units out to 30 units.
        private static readonly float[] DarkRadii = { 2.4f, 5.0f, 30f };
        private static readonly float[] DarkAlpha = { 0f, 1f, 1f };
        private const int DarkSegments = 48;

        private void BuildDarkRing() {
            darkRing = new Mesh { name = "Cave darkness" };
            var vertices = new Vector3[DarkRadii.Length * DarkSegments];
            for (int ring = 0; ring < DarkRadii.Length; ring++)
                for (int i = 0; i < DarkSegments; i++) {
                    float a = i * Mathf.PI * 2 / DarkSegments;
                    vertices[ring * DarkSegments + i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * DarkRadii[ring];
                }
            var triangles = new List<int>();
            for (int ring = 0; ring < DarkRadii.Length - 1; ring++)
                for (int i = 0; i < DarkSegments; i++) {
                    int a = ring * DarkSegments + i, b = ring * DarkSegments + (i + 1) % DarkSegments;
                    int c = a + DarkSegments, d = b + DarkSegments;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            darkRing.vertices = vertices;
            darkRing.uv = new Vector2[vertices.Length];
            darkRing.triangles = triangles.ToArray();
            TintDarkRing(0);
            var go = new GameObject("Cave darkness");
            go.AddComponent<RuntimeMeshOwner>().Owned = darkRing;
            go.AddComponent<MeshFilter>().sharedMesh = darkRing;
            var render = go.AddComponent<MeshRenderer>();
            render.sharedMaterial = Assets.material;
            render.sortingOrder = 31000;
            darkRingTransform = go.transform;
            go.SetActive(false);
        }

        private void TintDarkRing(float strength) {
            var colors = new Color[DarkRadii.Length * DarkSegments];
            for (int ring = 0; ring < DarkRadii.Length; ring++)
                for (int i = 0; i < DarkSegments; i++) colors[ring * DarkSegments + i] = new Color(.02f, .03f, .06f, DarkAlpha[ring] * strength);
            darkRing.colors = colors;
        }

        // ---------- story helpers used by chapters ----------
        public void After(float seconds, Action action) => StartCoroutine(Delay(seconds, action));

        private static IEnumerator Delay(float seconds, Action action) {
            yield return new WaitForSeconds(seconds);
            action();
        }

        public void Talk(Action then, params DialogueLine[] lines) {
            if (OnDialogue == null) { then?.Invoke(); return; }
            OnDialogue.Invoke(lines, then);
        }

        public void SetSpeaker(string speaker) => Chapter.SpeakerChanged(speaker);

        public bool AllDefeated(params string[] ids) => ids.All(defeated.Contains);

        public void Unlock(ArcaneSpell spell) {
            if (!Player.Book.Unlock(spell)) return;
            Player.Book.Save.research += 2;
            Audio.Chime();
            Toast(Player.Book.Definition(spell).name + " 카드 획득 · [K]에서 장착과 강화");
            Save();
        }

        public void ShowEnding(string voice, string title, string subtitle) {
            Ended = true;
            Player.Move = Vector2.zero;
            OnEnding?.Invoke(voice, title, subtitle);
        }

        public void LoadChapter(string id) {
            if (Player) Save();
            Time.timeScale = 1;
            foreach (var chapter in Chapters) if (chapter.id == id) SceneManager.LoadScene(chapter.scene);
        }

        public void RestartChapter() {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ---------- interaction ----------
        public ArcaneChapter.Target CurrentTarget {
            get {
                if (!Player || Ended) return null;
                Vector2 p = Player.transform.position;
                ArcaneChapter.Target best = null;
                float bestDistance = float.MaxValue;
                foreach (var target in Chapter.Targets) {
                    if (!Chapter.Available(target)) continue;
                    float distance = Vector2.Distance(p, target.Position);
                    if (distance > target.Radius || distance >= bestDistance) continue;
                    best = target;
                    bestDistance = distance;
                }
                return best;
            }
        }

        public string Interaction {
            get {
                var target = CurrentTarget;
                return target == null ? "" : Chapter.Label(target);
            }
        }

        public void Interact() {
            var target = CurrentTarget;
            if (target == null || Paused || Player.Dead) return;
            Chapter.Interact(target);
            Save();
        }

        public void SpawnUnlockedEncounters(bool announce) {
            foreach (var encounter in Chapter.Encounters) {
                if (Progress.phase < encounter.Gate || defeated.Contains(encounter.Id)) continue;
                if (Enemies.Any(e => e && e.SpawnId == encounter.Id)) continue;
                Vector2 at = Field.Marker(encounter.Marker);
                var enemy = Spawn(encounter.Role, encounter.Id, at);
                if (announce && enemy) ArcaneEffects.Sprite(Assets, "spark", at + Vector2.up * .4f, 1.2f, .4f, 0, 22000, new Color(1, .55f, .3f));
            }
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
            Chapter.EnemyDefeated(enemy);
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
            Toast("쓰러졌어… 연구 기록은 무사해. 다시 시작하자.");
        }

        public void PlayerRespawned() {
            foreach (var enemy in Enemies.ToArray()) if (enemy && !enemy.Dead) enemy.ResetToHome();
            if (Golem) Destroy(Golem.gameObject);
            Chapter.PlayerRespawned();
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
