using System;
using UnityEngine;

namespace ArcaneCasters.DataSystem {
    // Every sprite and texture the first field draws. ArcaneProjectBuilder fills it from Assets/ArcaneCasters/Art.
    public sealed class ArcaneAssets : ScriptableObject {
        [Serializable] public struct Art { public string name; public Sprite sprite; }
        // Frames follow the order in the strip's .frames.json: mira-* = idle, walk1, walk2, cast;
        // orin = idle, talk; creatures = base, windup.
        [Serializable] public struct FrameSet { public string name; public Sprite[] frames; }
        // Per chapter: its layout grid and the ground material carrying that chapter's four textures.
        [Serializable] public struct ChapterArt { public string id; public TextAsset layout; public Material groundMaterial; }

        public FrameSet[] frameSets = Array.Empty<FrameSet>();
        public Art[] props = Array.Empty<Art>();
        public Art[] icons = Array.Empty<Art>();
        public Art[] effects = Array.Empty<Art>();
        public Art[] portraits = Array.Empty<Art>();
        public Texture2D grass, dirt, water, stone;
        public TextAsset spells;
        public TextAsset layout;
        public ChapterArt[] chapters = Array.Empty<ChapterArt>();
        public Sprite appIcon;
        public Font font;
        public Material material;
        public Material groundMaterial;

        public Sprite[] Frames(string key) {
            foreach (var set in frameSets) if (set.name == key) return set.frames;
            return Array.Empty<Sprite>();
        }

        public ChapterArt Chapter(string id) {
            foreach (var chapter in chapters) if (chapter.id == id) return chapter;
            return new ChapterArt { id = id, layout = layout, groundMaterial = groundMaterial };
        }

        public Sprite Prop(string key) => Find(props, key);
        public Sprite Icon(string key) => Find(icons, key);
        public Sprite Effect(string key) => Find(effects, key);
        public Sprite Portrait(string key) => Find(portraits, key);

        private static Sprite Find(Art[] entries, string key) {
            foreach (var entry in entries) if (entry.name == key) return entry.sprite;
            return null;
        }
    }
}
