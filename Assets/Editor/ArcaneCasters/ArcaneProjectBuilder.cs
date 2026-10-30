using System;
using System.Collections.Generic;
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

namespace ArcaneCasters.Editor {
    // Imports the first-field art with its measured frame data and writes the scene.
    // The scene holds only the session, camera and input; the field itself is built at
    // runtime from Assets/ArcaneCasters/Data/root-boundary.txt by ArcaneField.
    public static class ArcaneProjectBuilder {
        public const string ScenePath = "Assets/Scenes/ArcaneCasters/RootBoundary.unity";
        private const string Root = "Assets/ArcaneCasters";
        private const string Art = Root + "/Art";
        private const float CharacterPixelsPerUnit = 128;

        // World height of each prop in units; pixels per unit follow from the image height.
        private static readonly Dictionary<string, (float height, float pivotY)> PropSizes = new Dictionary<string, (float, float)> {
            { "tree-broad", (4.4f, .05f) }, { "tree-pine", (5.0f, .04f) }, { "bush", (1.25f, .12f) }, { "boulders", (1.4f, .14f) },
            { "tent", (2.3f, .1f) }, { "campfire", (1.0f, .2f) }, { "bench", (.85f, .18f) }, { "world-root", (2.7f, .12f) },
            { "crystals", (1.6f, .14f) }, { "signpost", (2.2f, .06f) }, { "ruin-arch", (3.3f, .08f) }, { "ruin-pillar", (2.6f, .08f) },
            { "rune-stone", (1.7f, .1f) },
        };

        [Serializable] private sealed class StripInfo { public int[] cell; public float[] pivot; public string[] names; }

        [MenuItem("Arcane Casters/Import Art and Rebuild First Field")]
        public static void Build() {
            Directory.CreateDirectory("Assets/Scenes/ArcaneCasters");
            AssetDatabase.Refresh();
            var assets = AssetDatabase.LoadAssetAtPath<ArcaneAssets>(Root + "/FieldAssets.asset");
            if (!assets) { assets = ScriptableObject.CreateInstance<ArcaneAssets>(); AssetDatabase.CreateAsset(assets, Root + "/FieldAssets.asset"); }
            assets.material = SpriteMaterial();
            assets.groundMaterial = GroundMaterial(out assets.grass, out assets.dirt, out assets.water, out assets.stone);
            assets.font = AssetDatabase.LoadAssetAtPath<Font>(Art + "/Jua-Regular.ttf");
            assets.spells = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/spells.json");
            assets.layout = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/root-boundary.txt");
            assets.frameSets = Directory.GetFiles(Art + "/Characters", "*.png").Concat(Directory.GetFiles(Art + "/Creatures", "*.png"))
                .Select(path => path.Replace('\\', '/')).OrderBy(path => path)
                .Select(path => new ArcaneAssets.FrameSet { name = Path.GetFileNameWithoutExtension(path), frames = ImportStrip(path) }).ToArray();
            assets.props = PropSizes.Keys.Select(name => new ArcaneAssets.Art { name = name, sprite = ImportProp(name) }).ToArray();
            assets.effects = ImportFolder(Art + "/Effects", 256, new Vector2(.5f, .5f));
            assets.icons = ImportFolder(Art + "/Icons", 100, new Vector2(.5f, .5f));
            assets.portraits = ImportFolder(Art + "/Portraits", 100, new Vector2(.5f, 0));
            foreach (var set in assets.frameSets) if (set.frames.Length == 0) throw new InvalidOperationException("No frames for " + set.name);
            if (!assets.layout) throw new InvalidOperationException("Missing layout " + Root + "/Data/root-boundary.txt");
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            WriteScene(assets);
            Debug.Log("ARCANE_BUILD_SCENE_OK");
        }

        private static Material SpriteMaterial() {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/FieldUnlit.mat");
            if (!material) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, Root + "/FieldUnlit.mat"); }
            return material;
        }

        private static Material GroundMaterial(out Texture2D grass, out Texture2D dirt, out Texture2D water, out Texture2D stone) {
            grass = ImportGround("grass");
            dirt = ImportGround("dirt");
            water = ImportGround("water");
            stone = ImportGround("stone");
            var shader = Shader.Find("ArcaneCasters/GroundSplat");
            if (!shader) throw new InvalidOperationException("GroundSplat shader did not compile");
            string path = Root + "/FieldGround.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetTexture("_Grass", grass);
            material.SetTexture("_Dirt", dirt);
            material.SetTexture("_Water", water);
            material.SetTexture("_Stone", stone);
            material.SetFloat("_Tiling", 4f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D ImportGround(string name) {
            string path = Art + "/Ground/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Strips come from ArtSource/tools/sprite_sheet.py: equal cells, measured ground pivot.
        private static Sprite[] ImportStrip(string path) {
            string jsonPath = path.Substring(0, path.Length - 4) + ".frames.json";
            var info = JsonUtility.FromJson<StripInfo>(File.ReadAllText(jsonPath));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            SpriteSettings(importer, CharacterPixelsPerUnit, TextureImporterCompression.Uncompressed);
            importer.spriteImportMode = SpriteImportMode.Multiple;
            string baseName = Path.GetFileNameWithoutExtension(path);
            var cells = new SpriteMetaData[info.names.Length];
            for (int i = 0; i < cells.Length; i++) {
                cells[i] = new SpriteMetaData {
                    name = baseName + "_" + i + "_" + info.names[i],
                    rect = new Rect(i * info.cell[0], 0, info.cell[0], info.cell[1]),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(info.pivot[0], info.pivot[1]),
                };
            }
#pragma warning disable 618
            importer.spritesheet = cells;
#pragma warning restore 618
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            return info.names.Select((name, i) => sprites.First(s => s.name == baseName + "_" + i + "_" + name)).ToArray();
        }

        private static Sprite ImportProp(string name) {
            string path = Art + "/Props/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            int height = ImageHeight(path);
            var (worldHeight, pivotY) = PropSizes[name];
            SpriteSettings(importer, height / worldHeight, TextureImporterCompression.CompressedHQ);
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, pivotY);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static ArcaneAssets.Art[] ImportFolder(string folder, float pixelsPerUnit, Vector2 pivot) {
            var result = new List<ArcaneAssets.Art>();
            foreach (var file in Directory.GetFiles(folder, "*.png").Select(f => f.Replace('\\', '/')).OrderBy(f => f)) {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file);
                SpriteSettings(importer, pixelsPerUnit, TextureImporterCompression.Uncompressed);
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                result.Add(new ArcaneAssets.Art { name = Path.GetFileNameWithoutExtension(file), sprite = AssetDatabase.LoadAssetAtPath<Sprite>(file) });
            }
            return result.ToArray();
        }

        private static void SpriteSettings(TextureImporter importer, float pixelsPerUnit, TextureImporterCompression compression) {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = compression;
        }

        // Reads the PNG header so the size is right even before the first import finishes.
        private static int ImageHeight(string path) {
            using (var stream = File.OpenRead(path)) {
                var header = new byte[24];
                stream.Read(header, 0, 24);
                return (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            }
        }

        private static void WriteScene(ArcaneAssets assets) {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Arcane field session");
            var session = root.AddComponent<ArcaneSession>();
            session.Assets = assets;
            var input = root.AddComponent<ArcaneInput>();
            input.Session = session;
            var hud = root.AddComponent<ArcaneHud>();
            hud.Session = session;
            hud.Controls = input;
            new GameObject("Input bindings").AddComponent<InputManager>();
            var events = new GameObject("UI events");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ArcaneCamera.ViewHalfHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .12f, .08f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(-14, -8, -10);
            cameraObject.AddComponent<ArcaneCamera>().Session = session;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Arcane Casters";
            PlayerSettings.companyName = "Apptive Game Team";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Arcane Casters/Build Windows")]
        public static void BuildWindows() {
            PlayerSettings.productName = "Arcane Casters";
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Run Import Art and Rebuild First Field first.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath },
                locationPathName = "Build/ArcaneCastersRift/ArcaneCastersRift.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("ARCANE_WINDOWS_BUILD_OK");
        }
    }
}
