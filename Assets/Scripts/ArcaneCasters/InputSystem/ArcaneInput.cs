using System.Collections;
using System.Collections.Generic;
using ArcaneCasters.DataSystem;
using ArcaneCasters.GameSystem;
using GyeMong.InputSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcaneCasters.InputSystem {
    [DefaultExecutionOrder(100)]
    public sealed class ArcaneInput : MonoBehaviour {
        public ArcaneSession Session;
        public System.Action ToggleBook,TogglePause,AdvanceDialogue;
        private InputManager manager;
        private Dictionary<ActionCode,KeyCode> previous;
        private IEnumerator Start() {
            yield return null;
            manager=InputManager.Instance;previous=new Dictionary<ActionCode,KeyCode>(manager.GetKeyActions());
            manager.SetKey(ActionCode.Dash,KeyCode.Space);manager.SetKey(ActionCode.Interaction,KeyCode.F);
        }
        private bool Press(ActionCode action) {
            if(!manager)return false;
            manager.GetKeyDown(action);
            return manager.GetKeyActive(action)&&Input.GetKeyDown(manager.GetKeyActions()[action]);
        }
        private void Update() {
            if(!manager||!Session||!Session.Player)return;
            if(Input.GetKeyDown(KeyCode.Escape)){TogglePause?.Invoke();return;}
            if(Input.GetKeyDown(KeyCode.K)){ToggleBook?.Invoke();return;}
            if(Session.Paused){if(Input.GetKeyDown(KeyCode.F)||Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))AdvanceDialogue?.Invoke();return;}
            var p=Session.Player;
            p.Move=new Vector2((manager.GetKey(ActionCode.MoveRight)?1:0)-(manager.GetKey(ActionCode.MoveLeft)?1:0),(manager.GetKey(ActionCode.MoveUp)?1:0)-(manager.GetKey(ActionCode.MoveDown)?1:0));
            Vector2 point=Session.ResolveAim(Camera.main.ScreenToWorldPoint(Input.mousePosition));p.Aim=(point-(Vector2)p.transform.position).normalized;
            if(!(EventSystem.current&&EventSystem.current.IsPointerOverGameObject())) {
                if(manager.GetKey(ActionCode.Attack))p.Cast(ArcaneSpell.Bolt,point);
                if(Press(ActionCode.Skill))p.Cast(p.Book.Slot(1),point);
            }
            if(Press(ActionCode.Dash))p.Cast(ArcaneSpell.Dash,point);
            if(Press(ActionCode.Heal))p.Heal();
            if(Press(ActionCode.Interaction))Session.Interact();
            if(Input.GetKeyDown(KeyCode.Q))p.Cast(p.Book.Slot(0),point);
            if(Input.GetKeyDown(KeyCode.E))p.Cast(p.Book.Slot(1),point);
            if(Input.GetKeyDown(KeyCode.Alpha1))p.Cast(p.Book.Slot(2),point);
        }
        private void OnDisable(){if(Session&&Session.Player)Session.Player.Move=Vector2.zero;}
        private void OnDestroy(){if(manager&&previous!=null)foreach(var entry in previous)manager.SetKey(entry.Key,entry.Value);}
    }
}
