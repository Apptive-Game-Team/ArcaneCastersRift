using UnityEngine;
namespace ArcaneCasters.GameSystem {
    public sealed class ArcaneAudio : MonoBehaviour {
        private AudioSource source;
        private AudioClip card,impact;
        private void Awake(){source=gameObject.AddComponent<AudioSource>();source.spatialBlend=0;source.volume=.16f;card=Tone("Card spark",520,.085f);impact=Tone("Impact",130,.11f);}
        private static AudioClip Tone(string name,float frequency,float length){
            int count=Mathf.CeilToInt(22050*length);var data=new float[count];
            for(int i=0;i<count;i++){float t=(float)i/22050;data[i]=Mathf.Sin(2*Mathf.PI*frequency*t*(1-t*2))*Mathf.Exp(-t*38)*(1-Mathf.Exp(-t*500));}
            var clip=AudioClip.Create(name,count,1,22050,false);clip.SetData(data,0);return clip;
        }
        public void Cast(){source.PlayOneShot(card);}
        public void Hit(){source.PlayOneShot(impact);}
        private void OnDestroy(){if(card)Destroy(card);if(impact)Destroy(impact);}
    }
}
