using System.Collections;
using ArcaneCasters.DataSystem;
using ArcaneCasters.GameSystem;
using ArcaneCasters.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using Util.ChangeListener;

namespace ArcaneCasters.UISystem {
    [DefaultExecutionOrder(200)]
    public sealed class ArcaneHud : MonoBehaviour,IChangeListener<ArcaneCaster> {
        public ArcaneSession Session;
        public ArcaneInput Controls;
        private RectTransform canvas;
        private Text health,mana,quest,interaction,toast,level,bossText;
        private Image hpFill,mpFill,bossFill;
        private GameObject book,dialogue,pause;
        private readonly Text[] slots=new Text[5];
        private Text bookTitle,detail,upgradeLabel;
        private ArcaneSpell selected=ArcaneSpell.Bolt;
        private float toastUntil;
        private static readonly Color Navy=new Color(.05f,.13f,.22f,.96f),Orange=new Color(1,.6f,.11f),Muted=new Color(.65f,.76f,.82f);
        private IEnumerator Start(){
            yield return null;
            var root=new GameObject("Field journal HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            canvas=root.GetComponent<RectTransform>();
            Panel(canvas,"Vitals",new Vector2(24,20),new Vector2(225,100),Navy);
            level=Label(canvas,"FIELD RESEARCH / 01",new Vector2(38,29),new Vector2(200,24),14,Orange);
            hpFill=Bar(canvas,new Vector2(38,62),new Vector2(190,8),new Color(.86f,.27f,.21f));
            mpFill=Bar(canvas,new Vector2(38,86),new Vector2(190,5),new Color(.2f,.55f,.92f));
            health=Label(canvas,"",new Vector2(38,44),new Vector2(190,18),12,Color.white);mana=Label(canvas,"",new Vector2(38,93),new Vector2(190,18),11,Muted);
            var objectivePanel=Panel(canvas,"Objective",new Vector2(-310,20),new Vector2(286,80),Navy,new Vector2(1,1));
            Label(objectivePanel,"현장 기록  /  세계수의 불씨",new Vector2(15,10),new Vector2(260,20),12,Orange);
            quest=Label(objectivePanel,"",new Vector2(15,34),new Vector2(260,38),16,Color.white);
            interaction=Label(canvas,"",new Vector2(-220, -145),new Vector2(440,35),18,Color.white,new Vector2(.5f,0));interaction.alignment=TextAnchor.MiddleCenter;
            toast=Label(canvas,"",new Vector2(-330,135),new Vector2(660,50),17,Color.white,new Vector2(.5f,1));toast.alignment=TextAnchor.MiddleCenter;
            string[] keys={"LMB","Q","E / RMB","1","SPACE"};
            for(int i=0;i<5;i++){
                var card=Panel(canvas,"Spell "+i,new Vector2(-147+i*60,-77),new Vector2(52,52),Navy,new Vector2(.5f,0));
                Label(card,keys[i],new Vector2(0,4),new Vector2(52,15),9,Muted).alignment=TextAnchor.MiddleCenter;
                slots[i]=Label(card,"",new Vector2(0,20),new Vector2(52,30),15,Orange);slots[i].alignment=TextAnchor.MiddleCenter;
            }
            Button(canvas,"R  회복",new Vector2(169,-64),new Vector2(72,34),()=>Session.Player.Heal(),new Vector2(.5f,0));
            Button(canvas,"K  마법",new Vector2(-84,-28),new Vector2(168,24),ToggleBook,new Vector2(.5f,0));
            bossFill=Bar(canvas,new Vector2(-180,30),new Vector2(360,8),new Color(.8f,.25f,.25f),new Vector2(.5f,1));
            bossText=Label(canvas,"",new Vector2(-180,45),new Vector2(360,25),14,Color.white,new Vector2(.5f,1));bossText.alignment=TextAnchor.MiddleCenter;
            BuildBook();BuildDialogue();BuildPause();
            Session.OnToast+=ShowToast;Session.OnDialogue+=ShowDialogue;Session.OnFloatText+=FloatText;
            Session.Player.Status.AddListener(this);OnChanged(Session.Player);
            Controls.ToggleBook=ToggleBook;Controls.TogglePause=TogglePause;Controls.AdvanceDialogue=CloseDialogue;
            ShowToast("WASD 이동 · 마우스 조준 · F 조사 · Space 회피");
        }
        private void Update(){
            if(!canvas||!Session.Player)return;
            quest.text=Session.Objective+"  · "+Vector2.Distance(Session.Player.transform.position,Session.Goal).ToString("0")+"m";
            interaction.text=Session.Paused?"":Session.Interaction;toast.enabled=Time.unscaledTime<toastUntil;
            ArcaneSpell[] spells={ArcaneSpell.Bolt,Session.Player.Book.Slot(0),Session.Player.Book.Slot(1),Session.Player.Book.Slot(2),ArcaneSpell.Dash};
            string[] glyphs={"탄","골렘","낙뢰","빙결","덩굴","장막","회피"};
            for(int i=0;i<5;i++){
                var s=spells[i];float cd=s==ArcaneSpell.None?0:Session.Player.Ready[(int)s]-Time.time;
                slots[i].text=s==ArcaneSpell.None?"—":cd>0?cd.ToString("0.0"):glyphs[(int)s];slots[i].color=cd>0?Muted:Orange;
            }
            var boss=Session.Enemies.Find(e=>e&&e.Boss&&e.Alerted&&!e.Dead);
            bossFill.transform.parent.gameObject.SetActive(boss);bossText.gameObject.SetActive(boss);
            if(boss){bossFill.fillAmount=boss.CurrentHp/boss.MaxHp;bossText.text="유적의 문지기";}
        }
        public void OnChanged(ArcaneCaster p){
            if(!health)return;health.text=$"생명 {p.CurrentHp:0} / {p.MaxHp:0}"+(p.CurrentShield>0?$"  +{p.CurrentShield:0}":"");mana.text=$"마력 {p.Mana:0} / 100    회복약 {p.Potions}";
            hpFill.fillAmount=p.CurrentHp/p.MaxHp;mpFill.fillAmount=p.Mana/100;level.text=$"FIELD RESEARCH / {p.Book.Save.level:00}";
        }
        private void BuildBook(){
            var panel=Panel(canvas,"Spell journal",new Vector2(-410,85),new Vector2(820,550),Navy,new Vector2(.5f,1));book=panel.gameObject;
            bookTitle=Label(panel,"",new Vector2(30,24),new Vector2(690,34),24,Color.white);Button(panel,"닫기",new Vector2(720,24),new Vector2(70,34),ToggleBook);
            for(int i=0;i<7;i++){var s=(ArcaneSpell)i;Button(panel,Session.Player.Book.Definition(s).name,new Vector2(30,83+i*56),new Vector2(190,46),()=>{selected=s;RefreshBook();});}
            detail=Label(panel,"",new Vector2(252,90),new Vector2(525,290),19,Color.white);detail.lineSpacing=1.25f;
            var upgrade=Button(panel,"",new Vector2(252,408),new Vector2(300,44),()=>{if(!Session.Player.Book.Upgrade(selected,Session.InCombat))ShowToast("배운 마법과 연구점을 확인해 줘. 전투 중에는 바꿀 수 없어.");Session.Save();RefreshBook();});upgradeLabel=upgrade.GetComponentInChildren<Text>();
            for(int i=0;i<3;i++){int slot=i;Button(panel,new[]{"Q에 장착","E에 장착","1에 장착"}[i],new Vector2(252+i*170,468),new Vector2(150,40),()=>{if(!Session.Player.Book.Equip(slot,selected,Session.InCombat))ShowToast("공격·회피는 고정이야. 배운 마법을 골라 줘.");Session.Save();RefreshBook();});}
            book.SetActive(false);
        }
        private void RefreshBook(){
            var p=Session.Player;var d=p.Book.Definition(selected);int rank=p.Book.Rank(selected);var s=p.Book.Stats(selected);
            bookTitle.text="마법 연구  /  연구점 "+p.Book.Save.research;
            detail.text=d.name+"  "+(rank>0?$"{rank} / 3 단계":"미획득")+"\n<size=14><color=#9db9ca>"+d.role+"</color></size>\n\n"+d.description+"\n\n<size=15>"+(rank==0?"발견 장소: "+d.source:$"피해 {s.damage:0}   마력 {s.cost:0}   재사용 {s.cooldown:0.0}초")+"</size>";
            upgradeLabel.text=rank==0?"아직 발견하지 못했어":rank>=3?"최대 단계":$"강화하기  ·  연구점 {(rank==1?2:3)}";
        }
        public void ToggleBook(){if(dialogue&&dialogue.activeSelf)return;if(pause.activeSelf)pause.SetActive(false);book.SetActive(!book.activeSelf);Session.Pause(book.activeSelf);if(book.activeSelf)RefreshBook();}
        private void BuildDialogue(){
            var panel=Panel(canvas,"Dialogue",new Vector2(-485,-265),new Vector2(970,225),Navy,new Vector2(.5f,0));dialogue=panel.gameObject;
            var portrait=new GameObject("Portrait",typeof(RectTransform),typeof(Image));portrait.transform.SetParent(panel,false);Rect(portrait.GetComponent<RectTransform>(),new Vector2(0,-52),new Vector2(225,277),new Vector2(0,1));
            portrait.GetComponent<Image>().sprite=Session.Assets.mageFrames[0];portrait.GetComponent<Image>().preserveAspect=true;portrait.GetComponent<Image>().raycastTarget=false;
            Label(panel,"",new Vector2(245,22),new Vector2(670,30),23,Orange).name="Speaker";
            Label(panel,"",new Vector2(245,67),new Vector2(680,105),20,Color.white).name="Speech";
            Button(panel,"F / Enter  계속",new Vector2(735,178),new Vector2(205,32),CloseDialogue);dialogue.SetActive(false);
        }
        private void ShowDialogue(string speaker,string speech,string portrait){if(book.activeSelf)book.SetActive(false);dialogue.SetActive(true);dialogue.transform.Find("Speaker").GetComponent<Text>().text=speaker;dialogue.transform.Find("Speech").GetComponent<Text>().text=speech;dialogue.transform.Find("Portrait").GetComponent<Image>().sprite=speaker=="오린"?Session.Assets.Get("orin"):Session.Assets.mageFrames[6];Session.Pause(true);}
        public void CloseDialogue(){if(!dialogue||!dialogue.activeSelf)return;dialogue.SetActive(false);Session.Pause(false);}
        private void BuildPause(){var p=Panel(canvas,"Pause",new Vector2(-180,240),new Vector2(360,230),Navy,new Vector2(.5f,1));pause=p.gameObject;Label(p,"현장 연구 일시 정지",new Vector2(30,30),new Vector2(300,40),24,Color.white);Button(p,"계속하기",new Vector2(30,100),new Vector2(300,40),TogglePause);Button(p,"저장하고 종료",new Vector2(30,155),new Vector2(300,40),()=>{Session.Save();Application.Quit();});pause.SetActive(false);}
        public void TogglePause(){if(dialogue.activeSelf){CloseDialogue();return;}if(book.activeSelf){ToggleBook();return;}pause.SetActive(!pause.activeSelf);Session.Pause(pause.activeSelf);}
        private void ShowToast(string value){toast.text=value;toastUntil=Time.unscaledTime+4;}
        private void FloatText(Vector2 position,string value,Color color){StartCoroutine(Float(position,value,color));}
        private IEnumerator Float(Vector2 position,string value,Color color){
            var label=Label(canvas,value,Vector2.zero,new Vector2(90,35),19,color,new Vector2(.5f,.5f));label.alignment=TextAnchor.MiddleCenter;
            for(float t=0;t<.65f;t+=Time.deltaTime){Vector2 screen=Camera.main.WorldToScreenPoint(position+Vector2.up*t*.8f);RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,screen,null,out var local);label.rectTransform.anchoredPosition=local+new Vector2(-45,18);yield return null;}
            Destroy(label.gameObject);
        }
        private RectTransform Panel(Transform parent,string name,Vector2 pos,Vector2 size,Color color,Vector2? anchor=null){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;var rect=go.GetComponent<RectTransform>();Rect(rect,pos,size,anchor??new Vector2(0,1));return rect;}
        private Text Label(Transform parent,string value,Vector2 pos,Vector2 size,int fontSize,Color color,Vector2? anchor=null){var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=Session.Assets.font;text.text=value;text.fontSize=fontSize;text.color=color;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;Rect(text.rectTransform,pos,size,anchor??new Vector2(0,1));return text;}
        private Button Button(Transform parent,string value,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,Vector2? anchor=null){var rect=Panel(parent,value,pos,size,new Color(.13f,.26f,.37f),anchor);rect.GetComponent<Image>().raycastTarget=true;var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(action);var text=Label(rect,value,Vector2.zero,size,15,Color.white);text.alignment=TextAnchor.MiddleCenter;return button;}
        private Image Bar(Transform parent,Vector2 pos,Vector2 size,Color color,Vector2? anchor=null){var bg=Panel(parent,"Bar",pos,size,new Color(.02f,.05f,.08f),anchor);var fill=Panel(bg,"Fill",Vector2.zero,size,color);var image=fill.GetComponent<Image>();image.sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f);image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;return image;}
        private static void Rect(RectTransform rect,Vector2 pos,Vector2 size,Vector2 anchor){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(pos.x,-pos.y);rect.sizeDelta=size;}
        private void OnDestroy(){if(Session){Session.OnToast-=ShowToast;Session.OnDialogue-=ShowDialogue;Session.OnFloatText-=FloatText;}}
    }
}
