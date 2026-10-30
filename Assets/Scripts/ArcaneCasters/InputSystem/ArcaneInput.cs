using System.Collections;
using System.Collections.Generic;
using ArcaneCasters.DataSystem;
using ArcaneCasters.GameSystem;
using GyeMong.InputSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcaneCasters.InputSystem {
    // Keyboard and mouse for the first field. Movement keys come from GyeMong's InputManager
    // bindings; everything is read with Unity's Input directly so InputManager's own key-down
    // bookkeeping (which resets bindings when an action is missing) cannot swallow or remap a press.
    [DefaultExecutionOrder(100)]
    public sealed class ArcaneInput : MonoBehaviour {
        public ArcaneSession Session;
        public System.Action ToggleBook, TogglePause, AdvanceDialogue;
        public KeyCode DashKey = KeyCode.Space, InteractKey = KeyCode.F, HealKey = KeyCode.R;

        private InputManager manager;
        private Dictionary<ActionCode, KeyCode> previous;
        // A mouse button that was down while a menu was open, or that started on the UI,
        // must be released before it can cast. This stops clicks from leaking into attacks.
        private bool leftLatched = true, rightLatched = true;

        private IEnumerator Start() {
            yield return null;
            manager = InputManager.Instance;
            if (!manager) yield break;
            previous = new Dictionary<ActionCode, KeyCode>(manager.GetKeyActions());
            manager.SetKey(ActionCode.Dash, DashKey);
            manager.SetKey(ActionCode.Interaction, InteractKey);
        }

        private KeyCode Binding(ActionCode action, KeyCode fallback) {
            if (!manager) return fallback;
            var keys = manager.GetKeyActions();
            return keys != null && keys.TryGetValue(action, out var key) ? key : fallback;
        }

        private bool Held(ActionCode action, KeyCode fallback) => Input.GetKey(Binding(action, fallback));

        private bool PointerOnUi => EventSystem.current && EventSystem.current.IsPointerOverGameObject();

        private void Update() {
            if (!Session || !Session.Player) return;
            var p = Session.Player;
            if (Input.GetKeyDown(KeyCode.Escape)) { TogglePause?.Invoke(); Latch(); return; }
            if (Input.GetKeyDown(KeyCode.K)) { ToggleBook?.Invoke(); Latch(); return; }
            if (Session.Paused) {
                p.Move = Vector2.zero;
                Latch();
                if (Input.GetKeyDown(InteractKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(DashKey)) AdvanceDialogue?.Invoke();
                return;
            }
            if (p.Dead) { p.Move = Vector2.zero; return; }

            float x = (Held(ActionCode.MoveRight, KeyCode.D) ? 1 : 0) - (Held(ActionCode.MoveLeft, KeyCode.A) ? 1 : 0);
            float y = (Held(ActionCode.MoveUp, KeyCode.W) ? 1 : 0) - (Held(ActionCode.MoveDown, KeyCode.S) ? 1 : 0);
            p.Move = new Vector2(x, y);

            var camera = Camera.main;
            if (!camera) return;
            Vector2 point = Session.ResolveAim(camera.ScreenToWorldPoint(Input.mousePosition));
            Vector2 toCursor = point - (Vector2)p.transform.position;
            if (toCursor.sqrMagnitude > .0001f) p.Aim = toCursor.normalized;

            bool onUi = PointerOnUi;
            if (Input.GetMouseButtonDown(0) && onUi) leftLatched = true;
            if (Input.GetMouseButtonDown(1) && onUi) rightLatched = true;
            if (!Input.GetMouseButton(0)) leftLatched = false;
            if (!Input.GetMouseButton(1)) rightLatched = false;
            if (Input.GetMouseButton(0) && !leftLatched && !onUi) p.Cast(ArcaneSpell.Bolt, point);
            if (Input.GetMouseButtonDown(1) && !rightLatched && !onUi) Cast(p, p.Book.Slot(1), point);

            if (Input.GetKeyDown(DashKey)) p.Cast(ArcaneSpell.Dash, point);
            if (Input.GetKeyDown(HealKey)) p.Heal();
            if (Input.GetKeyDown(InteractKey)) Session.Interact();
            if (Input.GetKeyDown(KeyCode.Q)) Cast(p, p.Book.Slot(0), point);
            if (Input.GetKeyDown(KeyCode.E)) Cast(p, p.Book.Slot(1), point);
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Cast(p, p.Book.Slot(2), point);
        }

        private void Cast(ArcaneCaster p, ArcaneSpell spell, Vector2 point) {
            if (spell == ArcaneSpell.None) { Session.Toast("빈 칸이야. [K]에서 마법을 장착하자."); return; }
            p.Cast(spell, point);
        }

        private void Latch() {
            if (Input.GetMouseButton(0)) leftLatched = true;
            if (Input.GetMouseButton(1)) rightLatched = true;
        }

        private void OnDisable() { if (Session && Session.Player) Session.Player.Move = Vector2.zero; }

        private void OnDestroy() {
            if (manager && previous != null) foreach (var entry in previous) manager.SetKey(entry.Key, entry.Value);
        }
    }
}
