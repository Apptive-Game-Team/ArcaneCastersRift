using System;
using UnityEngine;

namespace ArcaneCasters.DataSystem {
    public enum ArcaneSpell { None = -1, Bolt, Summon, Lightning, Frost, Roots, Ward, Dash }

    [Serializable]
    public class SpellStats {
        public float damage, cooldown, cost, speed, health, duration, radius, stun, slow, freeze, absorb, invincible, burst, splash, splashDamage;
        public int pierce, chains;
    }

    [Serializable]
    public class SpellDefinition {
        public string id, name, role, description, source, icon;
        public bool innate;
        public SpellStats[] levels;
    }

    [Serializable]
    public class SpellCatalog {
        public SpellDefinition[] spells;
    }

    [Serializable]
    public sealed class ArcaneSave {
        public int version = 1, level = 1, experience, research, phase;
        public bool brook, rocks, vine, briefed;
        public int step;                       // chapter-specific sub-progress, such as cleared waves
        public string[] flags = Array.Empty<string>();
        public int[] ranks = { 1, 1, 0, 0, 0, 0, 1 };
        public int[] slots = { -1, 1, -1 };
        public string[] defeated = Array.Empty<string>();
    }

    public sealed class ArcaneSpellBook {
        public ArcaneSave Save { get; private set; }
        public SpellCatalog Catalog { get; }
        public ArcaneSpellBook(TextAsset catalog, ArcaneSave save = null) {
            Catalog = JsonUtility.FromJson<SpellCatalog>(catalog.text);
            Save = save ?? new ArcaneSave();
            Validate();
        }
        private void Validate() {
            if (Save.version != 1 || Save.ranks == null || Save.ranks.Length != 7 || Save.slots == null || Save.slots.Length != 3) Save = new ArcaneSave();
            for (int i = 0; i < 7; i++) Save.ranks[i] = Mathf.Clamp(Save.ranks[i], 0, 3);
            foreach (int i in new[] { 0, 1, 6 }) Save.ranks[i] = Mathf.Max(1, Save.ranks[i]);
            for (int i = 0; i < 3; i++) {
                int id = Save.slots[i];
                if (id < 1 || id > 5 || Save.ranks[id] == 0 || Array.IndexOf(Save.slots, id) < i) Save.slots[i] = -1;
            }
            Save.level = Mathf.Clamp(Save.level, 1, 99); Save.phase = Mathf.Clamp(Save.phase, 0, 6);
            Save.research = Mathf.Clamp(Save.research, 0, 10000); Save.experience = Mathf.Clamp(Save.experience, 0, 100000);
            Save.defeated ??= Array.Empty<string>();
            Save.flags ??= Array.Empty<string>();
            Save.phase = Mathf.Clamp(Save.phase, 0, 6);
        }
        public int Rank(ArcaneSpell spell) => spell >= ArcaneSpell.Bolt && spell <= ArcaneSpell.Dash ? Save.ranks[(int)spell] : 0;
        public SpellDefinition Definition(ArcaneSpell spell) => Catalog.spells[(int)spell];
        public SpellStats Stats(ArcaneSpell spell) => Definition(spell).levels[Mathf.Clamp(Rank(spell) - 1, 0, 2)];
        public ArcaneSpell Slot(int slot) => (ArcaneSpell)Save.slots[slot];
        public bool Unlock(ArcaneSpell spell) {
            if (Rank(spell) != 0 || spell < ArcaneSpell.Bolt || spell > ArcaneSpell.Dash) return false;
            Save.ranks[(int)spell] = 1;
            if (!Definition(spell).innate) for (int i = 0; i < 3; i++) if (Save.slots[i] == -1) { Save.slots[i] = (int)spell; break; }
            return true;
        }
        public bool Upgrade(ArcaneSpell spell, bool inCombat) {
            int rank = Rank(spell), cost = rank == 1 ? 2 : 3;
            if (inCombat || rank == 0 || rank >= 3 || Save.research < cost) return false;
            Save.research -= cost; Save.ranks[(int)spell]++; return true;
        }
        public bool Equip(int slot, ArcaneSpell spell, bool inCombat) {
            if (inCombat || slot < 0 || slot > 2 || spell < ArcaneSpell.Summon || spell > ArcaneSpell.Ward || Rank(spell) == 0) return false;
            for (int i = 0; i < 3; i++) if (Save.slots[i] == (int)spell) Save.slots[i] = -1;
            Save.slots[slot] = (int)spell; return true;
        }
    }
}
