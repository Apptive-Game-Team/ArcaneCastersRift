using System;
using UnityEngine;

namespace ArcaneCasters.DataSystem {
    public sealed class ArcaneAssets : ScriptableObject {
        [Serializable] public struct Art { public string name; public Sprite sprite; }
        public Art[] art = Array.Empty<Art>();
        public Sprite[] mageFrames;
        public TextAsset spells;
        public Font font;
        public Material material;
        public Sprite Get(string key) {
            foreach (var entry in art) if (entry.name == key) return entry.sprite;
            return null;
        }
    }
}
