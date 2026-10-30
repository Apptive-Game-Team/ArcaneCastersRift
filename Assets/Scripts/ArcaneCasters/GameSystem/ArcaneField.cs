using System.Collections.Generic;
using ArcaneCasters.DataSystem;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // Sorting shared by everything standing on the field: lower on screen draws in front.
    public static class ArcaneSorting {
        public const int Ground = -32000, GroundDecal = -31000, Telegraph = -30500;
        public static int At(float y) => Mathf.RoundToInt(-y * 100);
    }

    // Builds the first field from the layout grid in Assets/ArcaneCasters/Data/root-boundary.txt
    // (written by ArtSource/tools/make_layout.py). Ground paint, colliders, vegetation and the
    // positions of people, research targets and encounters all come from that one grid.
    public sealed class ArcaneField : MonoBehaviour {
        // Forest: chapter 1 woods. Highland: chapter 2 rocky hills with a cave ('c' floor).
        public enum Biome { Forest, Highland }
        private const int SplatPixelsPerCell = 8;
        private static readonly string TerrainChars = "#.=~p%cr";
        private Biome biome;
        private Material groundMaterial;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public Rect Bounds => new Rect(-Width / 2f, -Height / 2f, Width, Height);
        public bool SealOpen { get; private set; }

        private char[] terrain;   // row-major, row 0 is the top (north) edge
        private char[] raw;
        private readonly Dictionary<char, List<Vector2>> markers = new Dictionary<char, List<Vector2>>();
        private readonly List<SpriteRenderer> canopies = new List<SpriteRenderer>();
        private ArcaneAssets assets;
        private GameObject seal;
        private LineRenderer sealGlow;
        private Texture2D splat;
        private Transform viewer;

        public static ArcaneField Create(ArcaneAssets library, Transform parent, string chapterId, Biome biome) {
            var art = library.Chapter(chapterId);
            var field = new GameObject(chapterId + " field").AddComponent<ArcaneField>();
            field.transform.SetParent(parent, false);
            field.assets = library;
            field.biome = biome;
            field.groundMaterial = art.groundMaterial ? art.groundMaterial : library.groundMaterial;
            field.Parse((art.layout ? art.layout : library.layout).text);
            field.BuildGround();
            field.BuildColliders();
            field.BuildSeal();
            field.BuildVegetation();
            field.BuildProps();
            return field;
        }

        public void Follow(Transform target) => viewer = target;

        public Vector2 Marker(char key) => markers.TryGetValue(key, out var list) && list.Count > 0 ? list[0] : Vector2.zero;
        public IReadOnlyList<Vector2> Markers(char key) => markers.TryGetValue(key, out var list) ? list : new List<Vector2>();

        public Vector2 CellCenter(int col, int row) => new Vector2(-Width / 2f + col + .5f, Height / 2f - row - .5f);

        public bool TryCell(Vector2 point, out int col, out int row) {
            col = Mathf.FloorToInt(point.x + Width / 2f);
            row = Mathf.FloorToInt(Height / 2f - point.y);
            return col >= 0 && col < Width && row >= 0 && row < Height;
        }

        public bool Walkable(Vector2 point) {
            if (!TryCell(point, out int col, out int row)) return false;
            if (!SealOpen && raw[row * Width + col] == 'Z') return false;
            return !Blocks(terrain[row * Width + col]);
        }

        public Vector2 SealCenter {
            get {
                var cells = Markers('Z');
                if (cells.Count == 0) return Vector2.zero;
                Vector2 sum = Vector2.zero;
                foreach (var cell in cells) sum += cell;
                return sum / cells.Count;
            }
        }

        public void SetSealOpen(bool open) {
            SealOpen = open;
            if (seal) seal.SetActive(!open);
        }

        private static bool Blocks(char cell) => cell == '#' || cell == '~' || cell == 'r';

        public bool IsCave(Vector2 point) => TryCell(point, out int col, out int row) && terrain[row * Width + col] == 'c';

        private bool NearCave(int col, int row, int reach) {
            for (int r = row - reach; r <= row + reach; r++)
                for (int c = col - reach; c <= col + reach; c++)
                    if (r >= 0 && c >= 0 && r < Height && c < Width && terrain[r * Width + c] == 'c') return true;
            return false;
        }

        private void Parse(string text) {
            var lines = new List<string>();
            foreach (var line in text.Split('\n')) {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0) lines.Add(trimmed);
            }
            Height = lines.Count;
            Width = lines[0].Length;
            raw = new char[Width * Height];
            terrain = new char[Width * Height];
            for (int row = 0; row < Height; row++)
                for (int col = 0; col < Width; col++)
                    raw[row * Width + col] = col < lines[row].Length ? lines[row][col] : '#';
            for (int row = 0; row < Height; row++) {
                for (int col = 0; col < Width; col++) {
                    char cell = raw[row * Width + col];
                    terrain[row * Width + col] = TerrainChars.IndexOf(cell) >= 0 ? cell : GroundUnderMarker(col, row);
                    if (TerrainChars.IndexOf(cell) < 0) {
                        if (!markers.TryGetValue(cell, out var list)) markers[cell] = list = new List<Vector2>();
                        list.Add(CellCenter(col, row));
                    }
                }
            }
        }

        // A marker hides the ground under it; use the most common walkable ground around it.
        private char GroundUnderMarker(int col, int row) {
            var counts = new Dictionary<char, int>();
            for (int dy = -1; dy <= 1; dy++) {
                for (int dx = -1; dx <= 1; dx++) {
                    int c = col + dx, r = row + dy;
                    if (c < 0 || r < 0 || c >= Width || r >= Height) continue;
                    char around = raw[r * Width + c];
                    if (".=p%c".IndexOf(around) < 0) continue;
                    counts[around] = counts.TryGetValue(around, out int n) ? n + 1 : 1;
                }
            }
            char best = '.';
            int most = 0;
            foreach (var pair in counts) if (pair.Value > most) { best = pair.Key; most = pair.Value; }
            return best;
        }

        private char TerrainAt(int col, int row) {
            col = Mathf.Clamp(col, 0, Width - 1);
            row = Mathf.Clamp(row, 0, Height - 1);
            return terrain[row * Width + col];
        }

        // ---------- ground ----------
        private void BuildGround() {
            int width = Width * SplatPixelsPerCell, height = Height * SplatPixelsPerCell;
            splat = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Field splat" };
            var pixels = new Color32[width * height];
            for (int py = 0; py < height; py++) {
                for (int px = 0; px < width; px++) {
                    float wx = -Width / 2f + (px + .5f) / SplatPixelsPerCell;
                    float wy = -Height / 2f + (py + .5f) / SplatPixelsPerCell;
                    // Organic edges: nudge the lookup by a smooth deterministic offset.
                    float nx = wx + .28f * Mathf.Sin(wy * 1.7f + wx * .6f) + .12f * Mathf.Sin(wx * 4.1f);
                    float ny = wy + .28f * Mathf.Sin(wx * 1.5f - wy * .4f) + .12f * Mathf.Sin(wy * 3.7f);
                    Vector4 weights = SampleWeights(nx, ny);
                    pixels[py * width + px] = new Color(weights.x, weights.y, weights.z, weights.w);
                }
            }
            splat.SetPixels32(pixels);
            splat.Apply(false, true);

            var mesh = new Mesh { name = "Field ground" };
            float halfW = Width / 2f, halfH = Height / 2f;
            mesh.vertices = new[] { new Vector3(-halfW, -halfH), new Vector3(halfW, -halfH), new Vector3(halfW, halfH), new Vector3(-halfW, halfH) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            var ground = new GameObject("Ground");
            ground.transform.SetParent(transform, false);
            ground.AddComponent<RuntimeMeshOwner>().Owned = mesh;
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var render = ground.AddComponent<MeshRenderer>();
            render.sharedMaterial = groundMaterial;
            render.sortingOrder = ArcaneSorting.Ground;
            var block = new MaterialPropertyBlock();
            block.SetTexture("_Splat", splat);
            render.SetPropertyBlock(block);
        }

        // Bilinear blend of the four nearest cell centres: (dirt, water, stone, forest).
        private Vector4 SampleWeights(float wx, float wy) {
            float cx = wx + Width / 2f - .5f, cy = Height / 2f - wy - .5f;
            int c0 = Mathf.FloorToInt(cx), r0 = Mathf.FloorToInt(cy);
            float tx = cx - c0, ty = cy - r0;
            Vector4 a = CellWeights(TerrainAt(c0, r0)), b = CellWeights(TerrainAt(c0 + 1, r0));
            Vector4 c = CellWeights(TerrainAt(c0, r0 + 1)), d = CellWeights(TerrainAt(c0 + 1, r0 + 1));
            return Vector4.Lerp(Vector4.Lerp(a, b, tx), Vector4.Lerp(c, d, tx), ty);
        }

        private static Vector4 CellWeights(char cell) {
            switch (cell) {
                case '=': return new Vector4(1, 0, 0, 0);
                case '~': return new Vector4(0, 1, 0, .2f);
                case 'p': return new Vector4(0, 0, 1, 0);
                case '%': return new Vector4(0, 0, 1, 0);
                case '#': return new Vector4(0, 0, 0, 1);
                case 'c': return new Vector4(0, 0, 1, .35f);
                case 'r': return new Vector4(0, 0, 1, .8f);
                default: return Vector4.zero;
            }
        }

        // ---------- collision ----------
        private void BuildColliders() {
            var root = new GameObject("Field collision");
            root.transform.SetParent(transform, false);
            int wall = LayerMask.NameToLayer("Wall");
            root.layer = wall;
            for (int row = 0; row < Height; row++) {
                int start = -1;
                for (int col = 0; col <= Width; col++) {
                    bool blocked = col < Width && Blocks(terrain[row * Width + col]);
                    if (blocked && start < 0) start = col;
                    if (blocked || start < 0) continue;
                    AddBox(root.transform, wall, CellCenter(start, row) + new Vector2((col - start - 1) / 2f, 0), new Vector2(col - start, 1));
                    start = -1;
                }
            }
            // Keep everything inside the map even where an edge cell is open.
            AddBox(root.transform, wall, new Vector2(0, Height / 2f + .5f), new Vector2(Width + 2, 1));
            AddBox(root.transform, wall, new Vector2(0, -Height / 2f - .5f), new Vector2(Width + 2, 1));
            AddBox(root.transform, wall, new Vector2(Width / 2f + .5f, 0), new Vector2(1, Height + 2));
            AddBox(root.transform, wall, new Vector2(-Width / 2f - .5f, 0), new Vector2(1, Height + 2));
        }

        private static void AddBox(Transform parent, int layer, Vector2 center, Vector2 size) {
            var go = new GameObject("Blocked");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        private void BuildSeal() {
            var cells = Markers('Z');
            if (cells.Count == 0) return;
            seal = new GameObject("Ruin seal");
            seal.transform.SetParent(transform, false);
            seal.layer = LayerMask.NameToLayer("Wall");
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var cell in cells) { minX = Mathf.Min(minX, cell.x - .5f); maxX = Mathf.Max(maxX, cell.x + .5f); }
            float y = cells[0].y;
            var box = seal.AddComponent<BoxCollider2D>();
            box.offset = new Vector2((minX + maxX) / 2f, y);
            box.size = new Vector2(maxX - minX, .9f);
            if (biome == Biome.Highland) return; // the big stone golem itself holds this gate
            sealGlow = seal.AddComponent<LineRenderer>();
            sealGlow.sharedMaterial = assets.material;
            sealGlow.useWorldSpace = true;
            sealGlow.positionCount = 24;
            sealGlow.widthMultiplier = .16f;
            sealGlow.sortingOrder = ArcaneSorting.At(y) + 5;
            for (int i = 0; i < 24; i++) {
                float t = i / 23f;
                sealGlow.SetPosition(i, new Vector3(Mathf.Lerp(minX, maxX, t), y + Mathf.Sin(t * Mathf.PI * 6) * .12f));
            }
            foreach (float x in new[] { minX + .1f, maxX - .1f }) PlaceProp("rune-stone", new Vector2(x, y + .05f), .8f, false, solid: false);
        }

        // ---------- vegetation ----------
        private void BuildVegetation() {
            if (biome == Biome.Highland) { BuildHighland(); return; }
            var root = new GameObject("Forest");
            root.transform.SetParent(transform, false);
            int[] distance = DistanceToWalkable();
            var trees = new List<Vector2>();
            for (int row = 0; row < Height; row++) {
                for (int col = 0; col < Width; col++) {
                    char cell = terrain[row * Width + col];
                    float roll = Hash(col, row, 1);
                    Vector2 jitter = new Vector2(Hash(col, row, 2) - .5f, Hash(col, row, 3) - .5f) * .6f;
                    Vector2 at = CellCenter(col, row) + jitter;
                    if (cell == '~') {
                        if (distance[row * Width + col] == 1 && roll < .1f) PlaceProp("boulders", at, .65f, Hash(col, row, 4) < .5f, solid: false, parent: root.transform);
                        continue;
                    }
                    if (cell != '#') continue;
                    int d = distance[row * Width + col];
                    bool canopyOverOpenGround = OpenGroundAbove(col, row);
                    if (d <= 2 && canopyOverOpenGround) {
                        // A tree here would hide the path behind its canopy; keep the edge low.
                        if (roll < (d == 1 ? .85f : .5f)) PlaceProp(roll < .14f ? "boulders" : "bush", at, d == 1 ? .95f : 1.15f, Hash(col, row, 5) < .5f, solid: false, parent: root.transform);
                        continue;
                    }
                    float spacing = d <= 2 ? 1.5f : 1.9f;
                    if (roll > (d <= 2 ? .8f : .62f) || TooClose(trees, at, spacing)) {
                        if (d <= 2 && roll < .95f && Hash(col, row, 6) < .45f) PlaceProp("bush", at, 1.05f, Hash(col, row, 7) < .5f, solid: false, parent: root.transform);
                        continue;
                    }
                    trees.Add(at);
                    string kind = Hash(col, row, 8) < .32f ? "tree-pine" : "tree-broad";
                    float scale = .88f + Hash(col, row, 9) * .26f;
                    var tree = PlaceProp(kind, at, scale, Hash(col, row, 10) < .5f, solid: false, parent: root.transform);
                    if (tree) canopies.Add(tree);
                }
            }
        }

        // Rocky hills: pines and outcrops outside, dark walls and stalagmites around the cave.
        private void BuildHighland() {
            var root = new GameObject("Hills and cave");
            root.transform.SetParent(transform, false);
            int[] distance = DistanceToWalkable();
            var placed = new List<Vector2>();
            for (int row = 0; row < Height; row++) {
                for (int col = 0; col < Width; col++) {
                    char cell = terrain[row * Width + col];
                    if (cell != '#' && cell != 'r') continue;
                    int d = distance[row * Width + col];
                    float roll = Hash(col, row, 1);
                    Vector2 at = CellCenter(col, row) + new Vector2(Hash(col, row, 2) - .5f, Hash(col, row, 3) - .5f) * .5f;
                    bool flip = Hash(col, row, 4) < .5f;
                    if (cell == 'r' && !NearCave(col, row, 2)) {
                        // Mountain body: a cliff of outcrops where it meets the hills, bare dark rock above.
                        bool cliffEdge = OpenGroundBelow(col, row);
                        if (cliffEdge && roll < .8f && !TooClose(placed, at, 1.3f)) { PlaceProp(roll < .4f ? "cave-wall" : "rock-outcrop", at, .9f, flip, solid: false, parent: root.transform); placed.Add(at); }
                        else if (!cliffEdge && roll < .1f && !TooClose(placed, at, 2.5f)) { PlaceProp("rock-column", at, .8f, flip, solid: false, parent: root.transform); placed.Add(at); }
                        continue;
                    }
                    if (NearCave(col, row, 2)) {
                        if (d > 2 || roll > .7f || TooClose(placed, at, 1.2f)) continue;
                        string[] cave = { "cave-wall", "stalagmites", "rock-column", "rubble", "cave-crystals" };
                        string pick = cave[Mathf.Min(cave.Length - 1, (int)(Hash(col, row, 5) * cave.Length))];
                        if (pick == "cave-crystals" && Hash(col, row, 6) > .35f) pick = "cave-wall";
                        PlaceProp(pick, at, .85f, flip, solid: false, parent: root.transform);
                        placed.Add(at);
                        continue;
                    }
                    bool canopyOverOpenGround = OpenGroundAbove(col, row);
                    if (d <= 2 && canopyOverOpenGround) {
                        if (roll < .55f && !TooClose(placed, at, 1.1f)) { PlaceProp(roll < .2f ? "boulder-ridge" : "boulders", at, .8f, flip, solid: false, parent: root.transform); placed.Add(at); }
                        continue;
                    }
                    if (roll > (d <= 2 ? .75f : .55f) || TooClose(placed, at, d <= 2 ? 1.5f : 1.9f)) continue;
                    var kind = Hash(col, row, 7) < .55f ? "tree-pine" : "rock-outcrop";
                    var prop = PlaceProp(kind, at, .85f + Hash(col, row, 8) * .25f, flip, solid: false, parent: root.transform);
                    if (prop && kind == "tree-pine") canopies.Add(prop);
                    placed.Add(at);
                }
            }
        }

        private bool OpenGroundBelow(int col, int row) {
            for (int r = row + 1; r <= row + 2; r++)
                for (int c = col - 1; c <= col + 1; c++)
                    if (r < Height && c >= 0 && c < Width && !Blocks(terrain[r * Width + c]) && terrain[r * Width + c] != 'c') return true;
            return false;
        }

        private bool OpenGroundAbove(int col, int row) {
            for (int r = row - 4; r < row; r++)
                for (int c = col - 1; c <= col + 1; c++)
                    if (r >= 0 && c >= 0 && c < Width && !Blocks(terrain[r * Width + c])) return true;
            return false;
        }

        private static bool TooClose(List<Vector2> placed, Vector2 point, float spacing) {
            foreach (var other in placed) if ((other - point).sqrMagnitude < spacing * spacing) return true;
            return false;
        }

        private int[] DistanceToWalkable() {
            var distance = new int[Width * Height];
            var queue = new Queue<int>();
            for (int i = 0; i < distance.Length; i++) {
                bool open = !Blocks(terrain[i]);
                distance[i] = open ? 0 : int.MaxValue;
                if (open) queue.Enqueue(i);
            }
            while (queue.Count > 0) {
                int index = queue.Dequeue();
                int col = index % Width, row = index / Width;
                for (int dy = -1; dy <= 1; dy++) {
                    for (int dx = -1; dx <= 1; dx++) {
                        int c = col + dx, r = row + dy;
                        if (c < 0 || r < 0 || c >= Width || r >= Height) continue;
                        int next = r * Width + c;
                        if (distance[next] <= distance[index] + 1) continue;
                        distance[next] = distance[index] + 1;
                        queue.Enqueue(next);
                    }
                }
            }
            return distance;
        }

        private static float Hash(int col, int row, int salt) {
            unchecked {
                uint h = (uint)(col * 73856093) ^ (uint)(row * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        // ---------- props ----------
        private void BuildProps() {
            if (biome == Biome.Highland) { BuildVillageProps(); return; }
            var root = new GameObject("Camp and ruins");
            root.transform.SetParent(transform, false);
            var parent = root.transform;
            PlaceAll('H', "tent", 1f, parent, new Vector2(1.7f, .55f));
            PlaceAll('F', "campfire", 1f, parent, new Vector2(.7f, .35f));
            PlaceAll('E', "bench", 1f, parent, new Vector2(1.2f, .3f));
            PlaceAll('L', "signpost", 1f, parent, new Vector2(.3f, .25f));
            PlaceAll('G', "world-root", 1f, parent, new Vector2(2.6f, .7f));
            PlaceAll('C', "crystals", 1f, parent, new Vector2(.9f, .45f));
            PlaceAll('T', "rune-stone", 1f, parent, new Vector2(.8f, .45f));
            PlaceAll('N', "rune-stone", .8f, parent, new Vector2(.65f, .35f));
            PlaceAll('A', "ruin-arch", 1f, parent, new Vector2(3f, .6f));
            PlaceAll('I', "ruin-pillar", 1f, parent, new Vector2(.8f, .45f));
            foreach (var vine in Markers('V')) {
                PlaceDecal("thorns", vine + new Vector2(0, .1f), 1.6f, parent);
                PlaceProp("bush", vine + new Vector2(0, .25f), .9f, false, solid: true, size: new Vector2(.9f, .4f), parent: parent);
            }
        }

        private void BuildVillageProps() {
            var root = new GameObject("Village and cave");
            root.transform.SetParent(transform, false);
            var parent = root.transform;
            PlaceAll('H', "stone-hut", 1f, parent, new Vector2(1.9f, .7f));
            PlaceAll('A', "cairn", 1f, parent, new Vector2(.6f, .35f));
            PlaceAll('I', "quarry-blocks", 1f, parent, new Vector2(1.3f, .55f));
            PlaceAll('T', "tool-rack", 1f, parent, new Vector2(1.0f, .3f));
            var stones = Markers('o');
            if (stones.Count > 0) {
                Vector2 middle = Vector2.zero;
                foreach (var stone in stones) middle += stone;
                PlaceFlat("play-ring", middle / stones.Count + Vector2.down * .2f, 3.4f, parent);
            }
            foreach (var mouth in Markers('M')) PlaceProp("cliff-cave", mouth + Vector2.down * .35f, 1.15f, false, solid: false, parent: parent);
            // Spring pool art over the water cells by the east observation spot.
            foreach (var golem in Markers('S')) PlaceFlat("spring-pool", golem + new Vector2(2.6f, .2f), 3.2f, parent);
            foreach (var door in Markers('D')) {
                PlaceProp("stele", door + new Vector2(-2.6f, -.4f), .9f, false, solid: true, size: new Vector2(.9f, .4f), parent: parent);
                PlaceProp("stele", door + new Vector2(2.6f, -.4f), .9f, true, solid: true, size: new Vector2(.9f, .4f), parent: parent);
                PlaceFlat("red-cracks", door + new Vector2(0, -2.2f), 2.6f, parent);
            }
            foreach (var spot in Markers('3')) PlaceFlat("red-cracks", spot + Vector2.down * .3f, 1.6f, parent);
            foreach (var spot in Markers('1')) PlaceFlat("red-cracks", spot + Vector2.down * .3f, 1.4f, parent);
        }

        private void PlaceFlat(string prop, Vector2 at, float width, Transform parent) {
            var sprite = assets.Prop(prop);
            if (!sprite) return;
            var render = new GameObject(prop).AddComponent<SpriteRenderer>();
            render.transform.SetParent(parent, false);
            render.transform.position = at;
            render.sprite = sprite;
            render.sharedMaterial = assets.material;
            render.transform.localScale = Vector3.one * (width / Mathf.Max(sprite.bounds.size.x, .01f));
            render.sortingOrder = ArcaneSorting.GroundDecal + 5;
        }

        private void PlaceAll(char key, string sprite, float scale, Transform parent, Vector2 footprint) {
            foreach (var point in Markers(key)) PlaceProp(sprite, point, scale, false, solid: true, size: footprint, parent: parent);
        }

        private void PlaceDecal(string effect, Vector2 at, float size, Transform parent) {
            var sprite = assets.Effect(effect);
            if (!sprite) return;
            var render = new GameObject(effect).AddComponent<SpriteRenderer>();
            render.transform.SetParent(parent, false);
            render.transform.position = at;
            render.sprite = sprite;
            render.sharedMaterial = assets.material;
            render.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, .01f));
            render.sortingOrder = ArcaneSorting.GroundDecal;
            render.color = new Color(1, 1, 1, .9f);
        }

        private SpriteRenderer PlaceProp(string key, Vector2 at, float scale, bool flip, bool solid, Vector2 size = default, Transform parent = null) {
            var sprite = assets.Prop(key);
            if (!sprite) return null;
            var render = new GameObject(key).AddComponent<SpriteRenderer>();
            render.transform.SetParent(parent ? parent : transform, false);
            render.transform.position = at;
            render.transform.localScale = new Vector3(scale, scale, 1);
            render.sprite = sprite;
            render.flipX = flip;
            render.sharedMaterial = assets.material;
            render.sortingOrder = ArcaneSorting.At(at.y);
            if (solid) {
                render.gameObject.layer = LayerMask.NameToLayer("Wall");
                var box = render.gameObject.AddComponent<BoxCollider2D>();
                box.size = size / scale;
                box.offset = new Vector2(0, size.y / scale / 2f);
            }
            return render;
        }

        // ---------- per frame ----------
        private void LateUpdate() {
            if (sealGlow && seal.activeSelf) {
                float pulse = .55f + .35f * Mathf.Sin(Time.time * 3f);
                sealGlow.startColor = sealGlow.endColor = new Color(1f, .62f, .2f, pulse);
            }
            if (!viewer) return;
            Vector2 p = viewer.position;
            foreach (var canopy in canopies) {
                if (!canopy) continue;
                var bounds = canopy.bounds;
                bool behind = p.y > canopy.transform.position.y + .15f && p.y < bounds.max.y - .3f
                              && p.x > bounds.min.x + .4f && p.x < bounds.max.x - .4f;
                var color = canopy.color;
                float target = behind ? .45f : 1f;
                color.a = Mathf.MoveTowards(color.a, target, Time.unscaledDeltaTime * 4f);
                canopy.color = color;
            }
        }

        private void OnDestroy() {
            if (splat) Destroy(splat);
        }
    }
}
