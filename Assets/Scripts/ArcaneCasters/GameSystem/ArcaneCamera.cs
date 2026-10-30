using UnityEngine;
namespace ArcaneCasters.GameSystem {
    public sealed class ArcaneCamera : MonoBehaviour {
        public ArcaneSession Session;
        private void LateUpdate(){
            if(!Session||!Session.Player)return;
            Vector3 p=Session.Player.transform.position;
            var camera=GetComponent<Camera>();float halfW=camera.orthographicSize*camera.aspect;
            p.x=Mathf.Clamp(p.x,-20+halfW,20-halfW);p.y=Mathf.Clamp(p.y,-12+camera.orthographicSize,12-camera.orthographicSize);p.z=-10;
            transform.position=Vector3.Lerp(transform.position,p,1-Mathf.Exp(-8*Time.unscaledDeltaTime));
            if(Time.unscaledTime<Session.ShakeUntil)transform.position+=(Vector3)Random.insideUnitCircle*.04f;
        }
    }
}
