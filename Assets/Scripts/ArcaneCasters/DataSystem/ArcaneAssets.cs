using System;
using UnityEngine;

namespace ArcaneCasters.DataSystem {
    // Every sprite and texture the first field draws. ArcaneProjectBuilder fills it from Assets/ArcaneCasters/Art.
    public sealed class ArcaneAssets : ScriptableObject {
        [Serializable] public struct Art { public string name; public Sprite sprite; }
        // Frames follow the order in the strip's .frames.json: mira-* = idle, walk1, walk2, cast;
        // orin = idle, talk; creatures = base, windup.
        [Serializable] public struct FrameSet { public string name; public Sprite[] frames; }

        public FrameSet[] frameSets = Array.Empty<FrameSet>();
        public Art[] props = Array.Empty<Art>();
        public Art[] icons = Array.Empty<Art>();
        public Art[] effects = Array.Empty<Art>();
        public Art[] portraits = Array.Empty<Art>();
        public Texture2D grass, dirt, water, stone;
        public TextAsset spells;
        public TextAsset layout;
        public Font font;
        public Material material;
        public Material groundMaterial;

        public Sprite[] Frames(string key) {
            foreach (var set in frameSets) if (set.name == key) return set.frames;
            return Array.Empty<Sprite>();
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
