using System;
using System.Collections;
using System.IO;
using System.Linq;
using ArcaneCasters.DataSystem;
using ArcaneCasters.InputSystem;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // Development-build visual review: drives the game through its own commands (no OS input)
    // and saves what the window shows at each step. Run with --arcane-capture-dir <directory>.
    public sealed class ArcaneRenderCapture : MonoBehaviour {
        private string directory;
        private ArcaneSession session;
        private ArcaneInput input;

        private IEnumerator Start() {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--arcane-capture-dir");
            if (index < 0 || index + 1 >= args.Length) { Destroy(this); yield break; }
            directory = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(directory);
            session = GetComponent<ArcaneSession>();
            input = GetComponent<ArcaneInput>();
            input.enabled = false; // scripted review: game commands only, no live keyboard or mouse
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("01-camp-start");
            yield return WaitFor(() => session.UiMode == "dialogue", 4);
            yield return Shot("02-briefing-orin");
            input.AdvanceDialogue?.Invoke();
            yield return Shot("03-briefing-mira");
            yield return CloseDialogue();

            var p = session.Player;
            foreach (var (name, move) in new[] { ("04-walk-right", Vector2.right), ("05-walk-up", Vector2.up), ("06-walk-left", Vector2.left), ("07-walk-down", Vector2.down) }) {
                p.Move = move;
                p.Aim = move;
                yield return new WaitForSecondsRealtime(.45f);
                yield return Shot(name);
            }
            p.Move = Vector2.zero;

            yield return Teleport(session.Field.Marker('G') + new Vector2(.4f, -2.2f), Vector2.up);
            yield return Shot("08-root-prompt");
            session.Interact();
            yield return WaitFor(() => session.UiMode == "dialogue", 2);
            yield return Shot("09-root-dialogue");
            yield return CloseDialogue();
            yield return new WaitForSecondsRealtime(.4f);
            yield return WaitFor(() => session.Enemies.Any(e => e && e.Telegraphing), 4);
            yield return Shot("10-combat-telegraph");
            for (int i = 0; i < 4; i++) {
                var target = session.Nearest(p.transform.position, 8);
                if (target) p.Cast(ArcaneSpell.Bolt, target.transform.position);
                yield return new WaitForSecondsRealtime(.12f);
            }
            yield return Shot("11-combat-bolts");
            p.Cast(ArcaneSpell.Summon, p.transform.position + Vector3.right * 1.5f);
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("12-combat-golem");

            input.ToggleBook?.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            yield return Shot("13-journal");
            input.ToggleBook?.Invoke();
            input.TogglePause?.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            yield return Shot("14-pause");
            input.TogglePause?.Invoke();

            foreach (var e in session.Enemies.ToArray()) if (e) e.OnAttacked(9999);
            yield return Teleport(session.Field.Marker('C') + new Vector2(1.2f, -1.6f), Vector2.up);
            yield return Shot("15-brook");
            yield return Teleport(session.Field.Marker('T') + new Vector2(-1.4f, -1.2f), Vector2.right);
            yield return Shot("16-stones");
            yield return Teleport(session.Field.SealCenter + new Vector2(0, -2f), Vector2.up);
            yield return Shot("17-ruin-seal");
            p.Book.Save.phase = 3;
            session.Field.SetSealOpen(true);
            session.SpawnUnlockedEncounters(true);
            yield return Teleport(session.Field.Marker('W') + new Vector2(0, -4.2f), Vector2.up);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("18-ruin-boss");
            yield return WaitFor(() => session.Enemies.Any(e => e && e.Boss && e.Telegraphing), 4);
            yield return Shot("19-boss-telegraph");
            p.InvulnerableUntil = 0;
            p.OnAttacked(9999);
            yield return new WaitForSecondsRealtime(.9f);
            yield return Shot("20-player-down");
            Debug.Log("ARCANE_CAPTURE_OK " + directory);
            PlayerPrefs.DeleteKey(session.SaveKey);
            PlayerPrefs.Save();
            Application.Quit();
#else
            Destroy(this);
            yield break;
#endif
        }

        private IEnumerator Teleport(Vector2 at, Vector2 facing) {
            var p = session.Player;
            p.Move = Vector2.zero;
            p.transform.position = at;
            p.GetComponent<Rigidbody2D>().position = at;
            p.Aim = facing;
            yield return new WaitForSecondsRealtime(.5f);
        }

        private IEnumerator CloseDialogue() {
            for (int i = 0; i < 10 && session.UiMode == "dialogue"; i++) {
                input.AdvanceDialogue?.Invoke();
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds) {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        }

        private IEnumerator Shot(string name) {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            Destroy(image);
            Debug.Log("ARCANE_CAPTURE " + name);
        }
    }
}
