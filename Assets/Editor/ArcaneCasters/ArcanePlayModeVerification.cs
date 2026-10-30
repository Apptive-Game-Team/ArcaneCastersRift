using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneCasters.DataSystem;
using ArcaneCasters.GameSystem;
using ArcaneCasters.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneCasters.Editor {
    // Accelerated play-mode check through game commands. It does not send OS input and does not
    // judge how the game looks; Tools/Verification covers real input, captures cover visuals.
    [InitializeOnLoad]
    public static class ArcanePlayModeVerification {
        private const string Active = "Arcane.Verify.Active", Key = "Arcane.Verify.Key";
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        private static double deadline, next;
        private static int passed;

        static ArcanePlayModeVerification() {
            EditorApplication.playModeStateChanged += StateChanged;
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void Run() {
            SessionState.SetBool(Active, true);
            SessionState.SetString(Key, "ArcaneCastersRift.Verify." + Guid.NewGuid().ToString("N"));
            EditorSceneManager.OpenScene(ArcaneProjectBuilder.ScenePath);
            UnityEngine.Object.FindObjectOfType<ArcaneSession>().SaveKey = SessionState.GetString(Key, "");
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange state) {
            if (!SessionState.GetBool(Active, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            routines.Clear();
            routines.Push(Verify());
            passed = 0;
            deadline = EditorApplication.timeSinceStartup + 200;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            var session = UnityEngine.Object.FindObjectOfType<ArcaneSession>();
            if (session) session.SaveKey = SessionState.GetString(Key, "");
        }

        private static void Tick() {
            if (!EditorApplication.isPlaying || routines.Count == 0) return;
            try {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Verification timeout");
                if (EditorApplication.timeSinceStartup < next) return;
                var current = routines.Peek();
                if (!current.MoveNext()) {
                    routines.Pop();
                    if (routines.Count == 0) Finish(0);
                    return;
                }
                if (current.Current is IEnumerator nested) { routines.Push(nested); return; }
                next = EditorApplication.timeSinceStartup + (current.Current is float seconds ? seconds : 0);
            } catch (Exception error) {
                Debug.LogError("ARCANE_PLAYMODE_FAIL: " + error);
                Finish(1);
            }
        }

        private static void Check(bool condition, string name) {
            if (!condition) throw new Exception(name);
            passed++;
            Debug.Log("ARCANE_CHECK_OK: " + name);
        }

        private static IEnumerator CloseDialogues(ArcaneSession s) {
            var input = s.GetComponent<ArcaneInput>();
            for (int i = 0; i < 12 && s.UiMode == "dialogue"; i++) { input.AdvanceDialogue?.Invoke(); yield return .05f; }
            yield return .1f;
        }

        private static void Place(ArcaneCaster p, Vector2 at) {
            p.Move = Vector2.zero;
            p.transform.position = at;
            p.GetComponent<Rigidbody2D>().position = at;
        }

        private static IEnumerator Verify() {
            yield return .5f;
            var s = UnityEngine.Object.FindObjectOfType<ArcaneSession>();
            s.GetComponent<ArcaneInput>().enabled = false;
            var p = s.Player;
            var field = s.Field;
            Check(p && p.Book != null && field, "scene creates Mira, spell book and field");
            for (float briefingWait = 0; briefingWait < 5 && s.UiMode != "dialogue"; briefingWait += .1f) yield return .1f;
            Check(s.UiMode == "dialogue", "new game opens Orin's briefing");
            yield return CloseDialogues(s);
            Check(!s.Paused && s.UiMode == "play", $"closing the briefing resumes play (paused={s.Paused}, ui={s.UiMode}, timeScale={Time.timeScale})");
            Check(s.Enemies.Count == 6, "only the unlocked encounters exist at the start (6)");

            // Visible ground and collision come from one grid: check every cell.
            int mismatches = 0;
            for (int row = 0; row < field.Height; row++)
                for (int col = 0; col < field.Width; col++) {
                    Vector2 c = field.CellCenter(col, row);
                    bool walkable = field.Walkable(c);
                    bool solid = Physics2D.OverlapPoint(c, s.Walls);
                    bool propFootprint = solid && walkable;
                    if (!walkable && !solid) mismatches++;
                    if (walkable && solid && !propFootprint) mismatches++;
                }
            Check(mismatches == 0, "every blocked cell has a collider");
            Check(Reachable(field, s.StartPoint, field.Marker('G')) && Reachable(field, s.StartPoint, field.Marker('C')) && Reachable(field, s.StartPoint, field.Marker('T'))
                  && Reachable(field, s.StartPoint, field.Marker('V')) && Reachable(field, s.StartPoint, s.OrinPoint), "start reaches every research target on foot");
            Check(!Reachable(field, s.StartPoint, field.Marker('W')), "ruin is sealed before the surveys");

            Vector2 origin = p.transform.position;
            p.Move = Vector2.right;
            yield return .45f;
            p.Move = Vector2.zero;
            Check(Vector2.Distance(origin, p.transform.position) > .7f, "physics movement");
            float hp = p.CurrentHp;
            p.OnAttacked(30);
            Check(Mathf.Approximately(p.CurrentHp, hp - 30), "Creature damage");
            Check(p.Heal() && Mathf.Approximately(p.CurrentHp, p.MaxHp), "limited potion recovery");
            yield return .4f;
            Check(p.Cast(ArcaneSpell.Summon, p.transform.position + Vector3.right), "summon spends mana");
            Check(p.Mana <= 70.1f && s.Golem, "summon Creature instantiated");
            Check(!p.Cast(ArcaneSpell.Summon, p.transform.position), "cooldown rejects repeated summon");
            UnityEngine.Object.Destroy(s.Golem.gameObject);

            Vector2 camp = s.StartPoint;
            Place(p, camp);
            var target = s.Spawn(EnemyRole.Imp, "verify-bolt", camp + Vector2.right * 2.6f);
            target.ApplyControl(3, 0, 0);
            float enemyHp = target.CurrentHp;
            yield return .45f;
            Check(p.Cast(ArcaneSpell.Bolt, target.transform.position), "bolt command");
            yield return .5f;
            Check(target.CurrentHp < enemyHp, "bolt damages enemy");
            int rewards = s.RewardsIssued;
            target.OnAttacked(9999);
            target.OnAttacked(9999);
            Check(s.RewardsIssued == rewards + 1, "enemy death grants reward once");
            yield return .5f;

            // Telegraph and damage match: step out of the imp's lane and nothing lands.
            p.Rest();
            Place(p, camp);
            p.InvulnerableUntil = 0;
            var imp = s.Spawn(EnemyRole.Imp, "verify-lane", camp + Vector2.left * 1.5f);
            float waited = 0;
            while (!imp.Telegraphing && waited < 3) { waited += .05f; yield return .05f; }
            Check(imp.Telegraphing, "imp telegraphs before lunging");
            Place(p, camp + Vector2.up * 1.0f);
            float before = p.CurrentHp;
            yield return 1.0f;
            Check(Mathf.Approximately(p.CurrentHp, before), "leaving the warned lane avoids the lunge");
            waited = 0;
            while (imp.Telegraphing && waited < 3) { waited += .05f; yield return .05f; }
            yield return .3f;
            Place(p, (Vector2)imp.transform.position + Vector2.right * 1.4f);
            waited = 0;
            while (!imp.Telegraphing && waited < 4) { waited += .05f; yield return .05f; }
            before = p.CurrentHp;
            yield return 1.0f;
            Check(p.CurrentHp < before, $"standing in the warned lane takes the lunge (hp {before} -> {p.CurrentHp}, telegraphed={waited < 4})");
            imp.OnAttacked(9999);
            p.Rest();
            yield return .5f;

            var flying = s.Spawn(EnemyRole.Wisp, "verify-air", camp + Vector2.right * 2.6f);
            flying.ApplyControl(3, 0, 0);
            yield return .1f;
            Vector2 airAim = s.ResolveAim(flying.View.BodyPosition);
            Check(Vector2.Distance(airAim, flying.transform.position) < .01f, "aiming at the flying body targets its ground hitbox");
            float airHp = flying.CurrentHp;
            p.Cast(ArcaneSpell.Bolt, airAim);
            yield return .5f;
            Check(flying.CurrentHp < airHp, "visible flying target receives bolt");
            flying.OnAttacked(9999);

            p.Book.Save.research = 20;
            Check(p.Book.Upgrade(ArcaneSpell.Bolt, false) && p.Book.Rank(ArcaneSpell.Bolt) == 2, "spell rank upgrade");
            Check(!p.Book.Upgrade(ArcaneSpell.Bolt, true), "combat upgrade forbidden");
            p.Book.Unlock(ArcaneSpell.Lightning);
            var distant = s.Spawn(EnemyRole.Brute, "verify-lightning", camp + Vector2.right * 3f);
            distant.ApplyControl(3, 0, 0);
            yield return .1f;
            float distantHp = distant.CurrentHp;
            yield return .8f;
            p.Cast(ArcaneSpell.Lightning, distant.transform.position);
            Check(distant.CurrentHp < distantHp, "lightning strikes the aimed point");
            distant.OnAttacked(9999);
            p.Book.Unlock(ArcaneSpell.Frost);
            Check(p.Book.Equip(0, ArcaneSpell.Frost, false), "equip unlocked spell");
            Check(!p.Book.Equip(1, ArcaneSpell.Bolt, false), "innate spell cannot occupy active slot");
            p.Book.Unlock(ArcaneSpell.Ward);
            yield return .5f;
            p.Cast(ArcaneSpell.Ward, p.transform.position);
            float shield = p.CurrentShield;
            p.InvulnerableUntil = 0;
            p.OnAttacked(10);
            Check(shield > 0 && Mathf.Approximately(p.CurrentHp, p.MaxHp) && Mathf.Approximately(p.CurrentShield, shield - 10), "ward absorbs damage");

            // Story run.
            Place(p, field.Marker('G') + Vector2.down * 1.6f);
            yield return .2f;
            Check(s.CurrentTarget != null && s.CurrentTarget.Id == "root", "root prompt appears near the world root");
            s.Interact();
            yield return .1f;
            Check(s.UiMode == "dialogue", "root investigation opens dialogue");
            yield return CloseDialogues(s);
            Check(p.Book.Save.phase == 1 && s.Enemies.Count(e => e && e.SpawnId.StartsWith("root-")) == 3, "root investigation wakes three embers");
            foreach (var e in s.Enemies.ToArray()) if (e && e.SpawnId.StartsWith("root-")) e.OnAttacked(9999);
            yield return .1f;
            Check(p.Book.Save.phase == 2 && p.Book.Rank(ArcaneSpell.Lightning) > 0, "root encounter grants lightning");
            foreach (var e in s.Enemies.ToArray()) if (e && (e.SpawnId.StartsWith("brook-") || e.SpawnId.StartsWith("bridge-"))) e.OnAttacked(9999);
            yield return .1f;
            Place(p, field.Marker('C') + Vector2.down * 1.2f);
            yield return .1f;
            s.Interact();
            yield return CloseDialogues(s);
            Place(p, field.Marker('T') + Vector2.left * 1.2f);
            yield return .1f;
            s.Interact();
            yield return CloseDialogues(s);
            Check(p.Book.Save.phase == 3 && p.Book.Rank(ArcaneSpell.Ward) > 0 && field.SealOpen, "surveys open the ruin seal");
            Check(Reachable(field, s.StartPoint, field.Marker('W')), "ruin is reachable once the seal opens");
            Check(s.Enemies.Any(e => e && e.Boss), "warden appears after the seal opens");
            foreach (var e in s.Enemies.ToArray()) if (e && e.Boss) e.OnAttacked(9999);
            yield return .1f;
            Check(p.Book.Save.phase == 4, "warden defeat moves to the report");
            Place(p, s.OrinPoint + Vector2.right * 1.2f);
            yield return .1f;
            s.Interact();
            yield return CloseDialogues(s);
            Check(p.Book.Save.phase == 5, "report to Orin completes the first field");
            s.Save();
            int research = p.Book.Save.research;
            for (int i = 0; i < 2; i++) {
                SceneManager.LoadScene("RootBoundary");
                yield return 1.2f;
                s = UnityEngine.Object.FindObjectOfType<ArcaneSession>();
                s.GetComponent<ArcaneInput>().enabled = false;
                p = s.Player;
                Check(p.Book.Save.phase == 5 && p.Book.Save.research == research, "scene reentry preserves progress " + (i + 1));
                Check(!s.Enemies.Exists(e => e && e.Boss) && s.Field.SealOpen, "defeated warden and open seal persist " + (i + 1));
                Check(s.UiMode == "play", "briefing does not repeat on reentry " + (i + 1));
            }
            Place(p, s.Field.Marker('G'));
            p.InvulnerableUntil = 0;
            p.OnAttacked(9999);
            Check(p.Dead, "player enters defeated state");
            yield return 2.8f;
            Check(!p.Dead && Mathf.Approximately(p.CurrentHp, p.MaxHp) && Vector2.Distance(p.transform.position, s.StartPoint) < .1f, "defeat returns to camp with progress");
            yield return StoneVillage();
            Debug.Log("ARCANE_PLAYMODE_OK: " + passed + " checks passed through game commands");
        }

        private static int Mismatches(ArcaneSession s) {
            var field = s.Field;
            int mismatches = 0;
            for (int row = 0; row < field.Height; row++)
                for (int col = 0; col < field.Width; col++) {
                    Vector2 c = field.CellCenter(col, row);
                    if (!field.Walkable(c) && !Physics2D.OverlapPoint(c, s.Walls)) mismatches++;
                }
            return mismatches;
        }

        private static IEnumerator Interact(ArcaneSession s, Vector2 at) {
            Place(s.Player, at);
            yield return .15f;
            s.Interact();
            yield return .1f;
            yield return CloseDialogues(s);
        }

        // Chapter 2: Fin at the stone golem village, from arrival to the voice at the open door.
        private static IEnumerator StoneVillage() {
            SceneManager.LoadScene("StoneVillage");
            yield return 1.2f;
            var s = UnityEngine.Object.FindObjectOfType<ArcaneSession>();
            s.GetComponent<ArcaneInput>().enabled = false;
            var p = s.Player;
            var field = s.Field;
            Check(s.ChapterId == StoneVillageChapter.ChapterId && s.Chapter.PlayerName == ArcaneSession.Fin, "chapter 2 loads with Fin");
            for (float wait = 0; wait < 5 && s.UiMode != "dialogue"; wait += .1f) yield return .1f;
            Check(s.UiMode == "dialogue", "chapter 2 opens with Fin's arrival");
            yield return CloseDialogues(s);
            Check(Mismatches(s) == 0, "chapter 2: every blocked cell has a collider");
            Check(!Reachable(field, s.StartPoint, field.Marker('K')), "chapter 2: the big golem holds the gate");
            yield return Interact(s, field.Marker('B') + Vector2.down * 2.2f);
            Check(s.Progress.phase == 1 && field.SealOpen && Reachable(field, s.StartPoint, field.Marker('K')), "greeting the big golem opens the village");
            foreach (var stone in field.Markers('o')) yield return Interact(s, stone + Vector2.down * 1.0f);
            Check(s.Progress.phase == 2, "rolling three stones with the mini-rocks names Mok");
            Check(Reachable(field, s.StartPoint, field.Marker('Q')) && Reachable(field, s.StartPoint, field.Marker('S')) && Reachable(field, s.StartPoint, field.Marker('D')), "quarry, spring and the cave door are reachable on foot");
            yield return Interact(s, field.Marker('Q') + Vector2.down * 1.6f);
            yield return Interact(s, field.Marker('S') + Vector2.left * 1.6f);
            yield return CloseDialogues(s);
            Check(s.Progress.phase == 3 && p.Book.Rank(ArcaneSpell.Ward) > 0 && p.Book.Rank(ArcaneSpell.Frost) > 0, "both observations send Mok running to the cave");
            Check(s.Enemies.Count(e => e && !e.Dead) == 5, "embers leak out on the way to the cave (5)");
            foreach (var e in s.Enemies.ToArray()) if (e) e.OnAttacked(9999);
            Place(p, new Vector2(-6f, 11f));
            yield return .3f;
            Check(s.Progress.phase == 4 && s.Darkness > 0, "entering the cave darkens the view");
            yield return Interact(s, field.Marker('D') + Vector2.down * 2.4f);
            Check(s.Progress.phase == 5, "reaching Mok at the door opens it");
            for (float wait = 0; wait < 30 && s.Progress.phase < 6; wait += .3f) {
                foreach (var e in s.Enemies.ToArray()) if (e && !e.Dead) e.OnAttacked(9999);
                yield return .3f;
            }
            Check(s.Progress.phase == 6 && s.Progress.step == 3, "three fixed waves end the chapter");
            yield return 1.6f;
            Check(s.UiMode == "ending", "the voice and the ending card appear");
            s.Save();
            SceneManager.LoadScene("StoneVillage");
            yield return 2.0f;
            s = UnityEngine.Object.FindObjectOfType<ArcaneSession>();
            Check(s.Progress.phase == 6 && s.UiMode == "ending", "chapter 2 completion persists on reentry");
        }

        private static bool Reachable(ArcaneField field, Vector2 from, Vector2 to) {
            if (!field.TryCell(from, out int startCol, out int startRow) || !field.TryCell(to, out int goalCol, out int goalRow)) return false;
            var seen = new HashSet<int> { startRow * field.Width + startCol };
            var queue = new Queue<(int, int)>();
            queue.Enqueue((startCol, startRow));
            while (queue.Count > 0) {
                var (col, row) = queue.Dequeue();
                if (col == goalCol && row == goalRow) return true;
                foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) {
                    int c = col + dc, r = row + dr;
                    if (c < 0 || r < 0 || c >= field.Width || r >= field.Height) continue;
                    if (!seen.Add(r * field.Width + c)) continue;
                    Vector2 center = field.CellCenter(c, r);
                    bool isGoal = c == goalCol && r == goalRow;
                    if (!isGoal && !field.Walkable(center)) continue;
                    queue.Enqueue((c, r));
                }
            }
            return false;
        }

        private static void Finish(int code) {
            PlayerPrefs.DeleteKey(SessionState.GetString(Key, ""));
            PlayerPrefs.DeleteKey(SessionState.GetString(Key, "") + "." + StoneVillageChapter.ChapterId);
            PlayerPrefs.Save();
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EditorApplication.Exit(code);
        }
    }
}
