using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    public sealed class ArcaneTelemetry : MonoBehaviour {
        private ArcaneSession session;
        private StreamWriter writer;
        private float nextSnapshot;

        [Serializable]
        private sealed class EnemyState {
            public string id;
            public float x, y, hp;
            public bool alerted, telegraphing, boss;
        }

        [Serializable]
        private sealed class Snapshot {
            public float t;
            public int frame;
            public float x, y, hp, maxHp, mana, shield;
            public bool dead, paused;
            public int phase;
            public bool brook, rocks;
            public int level, research;
            public float[] ready = new float[7];
            public float aimX, aimY;
            public int enemies;
            public string interaction, objective, ui;
            public int casts;
            public float camX, camY, viewHalfHeight;
            public int potions;
            public EnemyState[] enemyList;
            public float mouseX, mouseY;
            public int screenW, screenH;
        }

        private void Start() {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "--arcane-telemetry");
            if (index < 0 || index + 1 >= arguments.Length) {
                Destroy(this);
                return;
            }

            session = GetComponent<ArcaneSession>();
            if (!session) {
                Destroy(this);
                return;
            }

            string path = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            writer = new StreamWriter(path, true, new UTF8Encoding(false));
        }

        private void Update() {
            if (writer == null || !session || !session.Player || Time.unscaledTime < nextSnapshot) return;
            nextSnapshot = Time.unscaledTime + .1f;

            var player = session.Player;
            var save = player.Book.Save;
            Vector3 position = player.transform.position;
            Vector3 mouse = Input.mousePosition;
            var snapshot = new Snapshot {
                t = Time.realtimeSinceStartup,
                frame = Time.frameCount,
                x = position.x,
                y = position.y,
                hp = player.CurrentHp,
                maxHp = player.MaxHp,
                mana = player.Mana,
                shield = player.CurrentShield,
                dead = player.Dead,
                paused = session.Paused,
                phase = save.phase,
                brook = save.brook,
                rocks = save.rocks,
                level = save.level,
                research = save.research,
                aimX = player.Aim.x,
                aimY = player.Aim.y,
                interaction = session.Interaction,
                objective = session.Objective,
                ui = session.UiMode,
                casts = player.CastCount,
                mouseX = mouse.x,
                mouseY = mouse.y,
                screenW = Screen.width,
                screenH = Screen.height
            };
            var view = Camera.main;
            if (view) { snapshot.camX = view.transform.position.x; snapshot.camY = view.transform.position.y; snapshot.viewHalfHeight = view.orthographicSize; }
            snapshot.potions = player.Potions;
            var list = new System.Collections.Generic.List<EnemyState>();
            foreach (var enemy in session.Enemies) {
                if (!enemy || enemy.Dead) continue;
                list.Add(new EnemyState { id = enemy.SpawnId, x = enemy.transform.position.x, y = enemy.transform.position.y, hp = enemy.CurrentHp, alerted = enemy.Alerted, telegraphing = enemy.Telegraphing, boss = enemy.Boss });
            }
            snapshot.enemyList = list.ToArray();
            for (int index = 0; index < snapshot.ready.Length; index++) {
                snapshot.ready[index] = player.Ready[index] - Time.time;
            }
            foreach (var enemy in session.Enemies) {
                if (enemy && !enemy.Dead) snapshot.enemies++;
            }

            writer.WriteLine(JsonUtility.ToJson(snapshot));
            writer.Flush();
        }

        private void OnDestroy() {
            writer?.Dispose();
            writer = null;
        }
    }
}
