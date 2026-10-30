using System;
using System.IO;
using System.Linq;
using ArcaneCasters.DataSystem;
using ArcaneCasters.GameSystem;
using ArcaneCasters.InputSystem;
using ArcaneCasters.UISystem;
using GyeMong.InputSystem;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ArcaneCasters.Editor {
    public static class ArcaneProjectBuilder {
        public const string ScenePath="Assets/Scenes/ArcaneCasters/RootBoundary.unity";
        private const string Root="Assets/ArcaneCasters";
        [MenuItem("Arcane Casters/Create or Open First Field")]
        public static void Build(){
            Directory.CreateDirectory("Assets/Scenes/ArcaneCasters");
            var atlasPath=Root+"/Art/field-atlas.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);float width=texture.width/4f,height=texture.height/4f;
            var metadata=new SpriteMetaData[16];
            for(int row=0;row<4;row++)for(int col=0;col<4;col++)metadata[row*4+col]=new SpriteMetaData{name="cell_"+(row*4+col).ToString("00"),rect=new Rect(col*width,(3-row)*height,width,height),alignment=(int)SpriteAlignment.Center,pivot=Vector2.one*.5f};
            importer.spritesheet=metadata;importer.SaveAndReimport();
            var sprites=AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
            var groundPath=Root+"/Art/root-forest.png";var groundImporter=(TextureImporter)AssetImporter.GetAtPath(groundPath);
            groundImporter.textureType=TextureImporterType.Sprite;groundImporter.spriteImportMode=SpriteImportMode.Single;groundImporter.maxTextureSize=2048;groundImporter.mipmapEnabled=false;groundImporter.textureCompression=TextureImporterCompression.Uncompressed;groundImporter.SaveAndReimport();
            var assets=AssetDatabase.LoadAssetAtPath<ArcaneAssets>(Root+"/FieldAssets.asset");
            if(!assets){assets=ScriptableObject.CreateInstance<ArcaneAssets>();AssetDatabase.CreateAsset(assets,Root+"/FieldAssets.asset");}
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/FieldUnlit.mat");
            if(!material){material=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,Root+"/FieldUnlit.mat");}
            assets.material=material;assets.font=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Art/Jua-Regular.ttf");assets.spells=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Data/spells.json");
            string[] names={"imp","wisp","golem","warden","tree","gold-tree","ruin","rock"};
            assets.art=names.Select((name,index)=>new ArcaneAssets.Art{name=name,sprite=sprites[index+8]}).Concat(new[]{new ArcaneAssets.Art{name="orin",sprite=ImportPortrait()}}).ToArray();
            var mageFrames=ImportMage();assets.mageFrames=new Sprite[32];
            for(int row=0;row<4;row++)for(int col=0;col<8;col++)assets.mageFrames[row*8+col]=mageFrames[(row==1||row==3?8:0)+col];
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            if(File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath);Debug.Log("ARCANE_SCENE_EXISTS: existing scene preserved");return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Arcane field session");var session=root.AddComponent<ArcaneSession>();session.Assets=assets;
            var input=root.AddComponent<ArcaneInput>();input.Session=session;var hud=root.AddComponent<ArcaneHud>();hud.Session=session;hud.Controls=input;
            new GameObject("Input bindings").AddComponent<InputManager>();
            var events=new GameObject("UI events");events.AddComponent<EventSystem>();events.AddComponent<StandaloneInputModule>();
            var cameraGo=new GameObject("Main Camera");cameraGo.tag="MainCamera";var camera=cameraGo.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5.2f;camera.backgroundColor=new Color(.035f,.09f,.1f);cameraGo.AddComponent<AudioListener>();cameraGo.transform.position=new Vector3(-10,-6,-10);cameraGo.AddComponent<ArcaneCamera>().Session=session;
            var ground=new GameObject("Painted forest floor").AddComponent<SpriteRenderer>();ground.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(groundPath);ground.sharedMaterial=material;ground.sortingOrder=-20000;ground.transform.localScale=new Vector3(40/ground.sprite.bounds.size.x,26.6667f/ground.sprite.bounds.size.y,1);
            var walls=new GameObject("Forest boundaries • collision");
            for(int row=0;row<54;row++){
                int start=-1;
                for(int col=0;col<=80;col++){
                    bool blocked=col<80&&!Walkable(new Vector2(-19.75f+col*.5f,-13.25f+row*.5f));
                    if(blocked&&start<0)start=col;
                    if(!blocked&&start>=0){var wall=new GameObject("Root bank");wall.transform.SetParent(walls.transform);wall.layer=LayerMask.NameToLayer("Wall");wall.transform.position=new Vector2(-20+(start+col)*.25f,-13.25f+row*.5f);wall.AddComponent<BoxCollider2D>().size=new Vector2((col-start)*.5f,.5f);start=-1;}
                }
            }
            EditorSceneManager.SaveScene(scene,ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.productName="Arcane Casters";PlayerSettings.companyName="Apptive Game Team";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.runInBackground=true;
            AssetDatabase.SaveAssets();Debug.Log("ARCANE_BUILD_SCENE_OK");
        }
        private static Sprite ImportPortrait(){
            string path=Root+"/Art/orin.png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(importer==null)return null;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=1000;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Sprite[] ImportMage(){
            string path=Root+"/Art/mage-directions.png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.maxTextureSize=4096;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);float w=texture.width/8f,h=texture.height/2f;var cells=new SpriteMetaData[16];
            for(int row=0;row<2;row++)for(int col=0;col<8;col++)cells[row*8+col]=new SpriteMetaData{name="mage_"+(row*8+col).ToString("00"),rect=new Rect(col*w,(1-row)*h,w,h),alignment=(int)SpriteAlignment.Center,pivot=Vector2.one*.5f};
            importer.spritesheet=cells;importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        }
        private static readonly Vector2[] MainPath={new Vector2(-15,-6),new Vector2(-11,-8),new Vector2(-7,-5),new Vector2(-4,-2),new Vector2(-1,0),new Vector2(4,1),new Vector2(8,2),new Vector2(11,4),new Vector2(14,7)};
        private static readonly Vector2[] BrookPath={new Vector2(-4,0),new Vector2(-8,2),new Vector2(-11,4),new Vector2(-13,6)};
        public static bool Walkable(Vector2 p){
            if(p.x< -18||p.x>18||p.y< -11||p.y>11)return false;
            if(Vector2.Distance(p,new Vector2(-13,-7))<2.5f||Vector2.Distance(p,new Vector2(-1,0))<3.6f||Vector2.Distance(p,new Vector2(-13,6))<2.8f||Vector2.Distance(p,new Vector2(14,8))<3.2f)return true;
            return Corridor(p,MainPath,1.25f)||Corridor(p,BrookPath,1.25f);
        }
        private static bool Corridor(Vector2 p,Vector2[] points,float width){for(int i=1;i<points.Length;i++){var d=points[i]-points[i-1];float t=Mathf.Clamp01(Vector2.Dot(p-points[i-1],d)/d.sqrMagnitude);if(Vector2.Distance(p,points[i-1]+d*t)<width)return true;}return false;}
        [MenuItem("Arcane Casters/Build Windows")]
        public static void BuildWindows(){
            PlayerSettings.productName="Arcane Casters";
            if(!File.Exists(ScenePath))throw new InvalidOperationException("Create the field scene first.");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Build/ArcaneCastersRift/ArcaneCastersRift.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
            Debug.Log("ARCANE_WINDOWS_BUILD_OK");
        }
    }
}
