using System;
using System.Collections.Generic;
using ArcaneCasters.DataSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ArcaneCasters.GameSystem {
    // Procedural sound bank. Each event has its own shape so hit, hurt, dodge and death read
    // apart even with the screen busy: bright clicks for the player's magic, dull thumps for
    // enemies taking damage, a low crunch for Mira getting hurt, an airy sweep for the dodge.
    public sealed class ArcaneAudio : MonoBehaviour {
        private const int Rate = 22050;
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private int next;
        private AudioSource ambience;
        private System.Random noise = new System.Random(7);

        private void Awake() {
            for (int i = 0; i < 8; i++) {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0;
                voices.Add(voice);
            }
            Build("card", .12f, t => (Sine(t, 1400 - t * 5200) * .5f + Noise() * .35f) * Env(t, .004f, 30));
            Build("frost", .35f, t => (Sine(t, 1760) * .35f + Sine(t, 2637) * .25f + Noise() * .1f) * Env(t, .005f, 9));
            Build("lightning", .5f, t => (Noise() * (Random.value < .08f ? 1f : .35f) + Sine(t, 90) * .3f) * Env(t, .002f, 7));
            Build("summon", .55f, t => (Sine(t, 70 + t * 40) * .7f + Noise() * .25f) * Env(t, .02f, 4.5f));
            Build("ward", .45f, t => (Sine(t, 880 + Mathf.Sin(t * 60) * 30) * .3f + Sine(t, 1320) * .2f) * Env(t, .03f, 5));
            Build("roots", .4f, t => (Sine(t, 180 + Mathf.Sin(t * 90) * 60) * .5f + Noise() * .2f) * Env(t, .01f, 6));
            Build("dodge", .22f, t => Noise() * .55f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .22f)) * (.4f + t * 3));
            Build("dodged", .18f, t => (Sine(t, 2200) * .3f + Sine(t, 3300) * .2f) * Env(t, .002f, 22));
            Build("enemy-hit", .12f, t => (Sine(t, 220 - t * 900) * .7f + Noise() * .45f * Mathf.Exp(-t * 80)) * Env(t, .001f, 26));
            Build("enemy-down", .35f, t => (Sine(t, 520 - t * 1100) * .5f + Noise() * .2f) * Env(t, .002f, 10));
            Build("boss-down", 1.1f, t => (Sine(t, 110 - t * 40) * .6f + Noise() * .35f * Mathf.Exp(-t * 3)) * Env(t, .01f, 2.4f));
            Build("player-hurt", .26f, t => (Sine(t, 150 - t * 260) * .8f + Noise() * .5f * Mathf.Exp(-t * 40)) * Env(t, .001f, 12));
            Build("ward-hit", .18f, t => (Sine(t, 990) * .4f + Noise() * .2f) * Env(t, .001f, 20));
            Build("player-down", 1.2f, t => (Sine(t, 330 * Mathf.Pow(.5f, t * 1.4f)) * .5f + Sine(t, 247 * Mathf.Pow(.5f, t * 1.4f)) * .35f) * Env(t, .02f, 2));
            Build("warning", .3f, t => Sine(t, 300 + t * 700) * .28f * Env(t, .02f, 6));
            Build("enemy-shot", .18f, t => (Noise() * .5f + Sine(t, 330) * .3f) * Env(t, .003f, 16));
            Build("slam", .45f, t => (Sine(t, 60) * .9f + Noise() * .5f * Mathf.Exp(-t * 18)) * Env(t, .002f, 7));
            Build("thud", .25f, t => (Sine(t, 90) * .8f + Noise() * .3f) * Env(t, .002f, 14));
            Build("heal", .45f, t => (Sine(t, 660) * .3f + Sine(t, 990 + t * 300) * .25f) * Env(t, .01f, 6));
            Build("chime", .6f, t => (Sine(t, t < .12f ? 784 : t < .24f ? 988 : 1319) * .35f) * Env(t, .005f, 4));
            Build("click", .05f, t => Sine(t, 1800) * .25f * Env(t, .001f, 80));
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = Wind();
            ambience.loop = true;
            ambience.volume = .07f;
            ambience.spatialBlend = 0;
            ambience.Play();
        }

        private float Noise() => (float)(noise.NextDouble() * 2 - 1);
        private static float Sine(float t, float frequency) => Mathf.Sin(2 * Mathf.PI * frequency * t);
        private static float Env(float t, float attack, float decay) => Mathf.Clamp01(t / attack) * Mathf.Exp(-t * decay);

        private void Build(string name, float length, Func<float, float> wave) {
            int count = Mathf.CeilToInt(Rate * length);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave((float)i / Rate), -1, 1);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }

        private AudioClip Wind() {
            int count = Rate * 4;
            var data = new float[count];
            float low = 0;
            for (int i = 0; i < count; i++) {
                low += (Noise() - low) * .02f;
                float t = (float)i / count;
                data[i] = low * (.6f + .4f * Mathf.Sin(t * Mathf.PI * 2));
            }
            var clip = AudioClip.Create("Forest wind", count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void Play(string name, float volume, float pitchJitter = .06f) {
            if (!clips.TryGetValue(name, out var clip)) return;
            var voice = voices[next];
            next = (next + 1) % voices.Count;
            voice.pitch = 1 + Random.Range(-pitchJitter, pitchJitter);
            voice.PlayOneShot(clip, volume);
        }

        public void Cast(ArcaneSpell spell) {
            switch (spell) {
                case ArcaneSpell.Frost: Play("frost", .45f); break;
                case ArcaneSpell.Lightning: Play("lightning", .6f); break;
                case ArcaneSpell.Summon: break; // the golem plays its own rumble
                case ArcaneSpell.Ward: Play("ward", .5f); break;
                case ArcaneSpell.Roots: Play("roots", .5f); break;
                case ArcaneSpell.Dash: Play("dodge", .6f); break;
                default: Play("card", .32f, .1f); break;
            }
        }
        public void Summon() => Play("summon", .6f);
        public void EnemyHit() => Play("enemy-hit", .55f, .1f);
        public void EnemyDown(bool boss) => Play(boss ? "boss-down" : "enemy-down", boss ? .8f : .5f);
        public void PlayerHurt() => Play("player-hurt", .75f);
        public void WardHit() => Play("ward-hit", .5f);
        public void Dodged() => Play("dodged", .45f);
        public void PlayerDown() => Play("player-down", .7f, 0);
        public void Warning() => Play("warning", .35f);
        public void EnemyShot() => Play("enemy-shot", .35f);
        public void Slam(bool boss) => Play("slam", boss ? .85f : .6f);
        public void Thud() => Play("thud", .6f);
        public void Heal() => Play("heal", .5f, 0);
        public void Chime() => Play("chime", .5f, 0);
        public void Click() => Play("click", .5f, 0);

        private void OnDestroy() {
            foreach (var clip in clips.Values) if (clip) Destroy(clip);
            if (ambience && ambience.clip) Destroy(ambience.clip);
        }
    }
}
