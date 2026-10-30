using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArcaneCasters.GameSystem {
    // Engine-rendered development captures; this does not synthesize OS input.
    public sealed class ArcaneRenderCapture : MonoBehaviour {
        private IEnumerator Start(){
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--arcane-capture-dir");
            if(index<0||index+1>=args.Length){Destroy(this);yield break;}
            string directory=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(directory);
            var session=GetComponent<ArcaneSession>();yield return new WaitForSeconds(1);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(directory,"camp.png"));
            yield return new WaitForSeconds(.4f);
            session.Interact();yield return new WaitForEndOfFrame();Capture(Path.Combine(directory,"dialogue.png"));
            yield return new WaitForSecondsRealtime(.4f);session.Pause(false);
            Debug.Log("ARCANE_CAPTURE_OK");
            PlayerPrefs.DeleteKey(session.SaveKey);PlayerPrefs.Save();
            Application.Quit();
#else
            Destroy(this);yield break;
#endif
        }
        private static void Capture(string path){
            var camera=Camera.main;
            foreach(var canvas in FindObjectsOfType<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            Canvas.ForceUpdateCanvases();
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=previous;Destroy(image);target.Release();Destroy(target);
        }
    }
}
