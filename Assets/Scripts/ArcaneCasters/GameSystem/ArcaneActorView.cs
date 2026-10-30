using System.Collections;
using ArcaneCasters.DataSystem;
using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // Draws one actor from its frame strip. The transform position is the ground contact point:
    // shadow, sorting, collision and every hit test use it; flying actors lift only the body sprite.
    public sealed class ArcaneActorView : MonoBehaviour {
        public SpriteRenderer Body { get; private set; }
        public float Altitude { get; private set; }
        public Vector2 facing = Vector2.down;
        public bool moving, casting, windup, talking;

        private Sprite[] frames, down, side, up;
        private bool directional, artFacesRight;
        private Transform shadow;
        private float hurtUntil, punchUntil;
        private Color hurtTint = new Color(1f, .55f, .5f);
        private float phase;

        public Vector2 BodyPosition => Body ? (Vector2)Body.bounds.center : (Vector2)transform.position;
        public Bounds BodyBounds => Body ? Body.bounds : new Bounds(transform.position, Vector3.one);

        // set: "mira" (directional), or a strip name such as "enemy-imp" or "orin".
        public void Init(ArcaneAssets assets, string set, float altitude = 0, bool facesRight = false) {
            Altitude = altitude;
            artFacesRight = facesRight;
            directional = set == "mira";
            if (directional) {
                down = assets.Frames("mira-down");
                side = assets.Frames("mira-side");
                up = assets.Frames("mira-up");
                frames = down;
            } else {
                frames = assets.Frames(set);
            }
            Body = new GameObject("Body").AddComponent<SpriteRenderer>();
            Body.transform.SetParent(transform, false);
            Body.sharedMaterial = assets.material;
            Body.sprite = frames.Length > 0 ? frames[0] : null;
            float width = Body.sprite ? Body.sprite.bounds.size.x : .6f;
            float shadowWidth = Mathf.Clamp(width * (directional ? .2f : .34f), .18f, 1.3f) * (altitude > 0 ? .8f : 1f);
            shadow = ArcaneEffects.Shadow(transform, shadowWidth, assets.material).transform;
            phase = Random.value * 10f;
        }

        public void Hurt(Color tint, float duration = .12f) { hurtTint = tint; hurtUntil = Time.time + duration; punchUntil = Time.time + .1f; }

        public void FadeOut(float duration) => StartCoroutine(Fade(duration));

        private IEnumerator Fade(float duration) {
            for (float t = 0; t < duration; t += Time.deltaTime) {
                if (Body) Body.color = new Color(1, 1, 1, 1 - t / duration);
                yield return null;
            }
        }

        private void LateUpdate() {
            if (!Body || frames == null || frames.Length == 0) return;
            Body.sprite = PickFrame();
            float bob = Altitude > 0 ? Mathf.Sin((Time.time + phase) * 3f) * .06f : 0;
            Body.transform.localPosition = new Vector3(0, Altitude + bob, 0);
            float squash = !directional && moving && Altitude <= 0 ? 1 + Mathf.Sin((Time.time + phase) * 14f) * .035f : 1;
            float punch = Time.time < punchUntil ? 1.07f : 1f;
            Body.transform.localScale = new Vector3(punch / squash, punch * squash, 1);
            int order = ArcaneSorting.At(transform.position.y);
            Body.sortingOrder = order;
            if (shadow) shadow.GetComponent<MeshRenderer>().sortingOrder = order - 2;
            if (Body.color.a > .99f || Time.time < hurtUntil) Body.color = Time.time < hurtUntil ? hurtTint : Color.white;
        }

        private Sprite PickFrame() {
            if (directional) {
                Sprite[] set;
                bool flip = false;
                if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y) * .8f) { set = side; flip = facing.x < 0; }
                else set = facing.y > 0 ? up : down;
                Body.flipX = flip;
                if (casting) return set[3];
                if (!moving) return set[0];
                int step = Mathf.FloorToInt(Time.time * 8f) % 4;
                return step == 0 ? set[1] : step == 2 ? set[2] : set[0];
            }
            Body.flipX = artFacesRight ? facing.x < -.05f : facing.x > .05f;
            if ((windup || talking) && frames.Length > 1) return frames[1];
            return frames[0];
        }
    }

    // Short-lived visuals: sprites, rings, lanes, shadows. Telegraphs live in ArcaneTelegraph.
    public static class ArcaneEffects {
        public static SpriteRenderer Sprite(ArcaneAssets assets, string effect, Vector2 at, float size, float life, float rotation = 0, int order = 25000, Color? tint = null) {
            var sprite = assets.Effect(effect) ?? assets.Icon(effect);
            if (!sprite) return null;
            var render = new GameObject(effect + " effect").AddComponent<SpriteRenderer>();
            render.transform.position = at;
            render.transform.rotation = Quaternion.Euler(0, 0, rotation);
            render.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y, .01f));
            render.sprite = sprite;
            render.sharedMaterial = assets.material;
            render.sortingOrder = order;
            render.color = tint ?? Color.white;
            render.gameObject.AddComponent<FadeAndDestroy>().Begin(life);
            return render;
        }

        public static GameObject Ring(Vector2 center, float radius, Color color, Material material, float life = .5f, float width = .05f, int order = 20000) {
            var go = new GameObject("Ring");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 48;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < 48; i++) {
                float a = i * Mathf.PI * 2 / 48;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            Object.Destroy(go, life);
            return go;
        }

        public static GameObject Line(Vector2 from, Vector2 to, Color color, Material material, float width = .06f, float life = .3f, int order = 20001) {
            var go = new GameObject("Line");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            Object.Destroy(go, life);
            return go;
        }

        public static GameObject Shadow(Transform parent, float width, Material material) {
            var go = Disk(parent, width, width * .38f, new Color(0, .06f, .03f, .38f), material, -1);
            go.name = "Ground shadow";
            return go;
        }

        public static GameObject Disk(Transform parent, float width, float height, Color color, Material material, int order) {
            var go = new GameObject("Disk");
            if (parent) go.transform.SetParent(parent, false);
            var mesh = new Mesh();
            var vertices = new Vector3[26];
            var colors = new Color[26];
            var uv = new Vector2[26];
            var triangles = new int[72];
            for (int i = 0; i < 26; i++) { colors[i] = color; uv[i] = Vector2.one * .5f; }
            for (int i = 0; i <= 24; i++) {
                float a = i * Mathf.PI * 2 / 24;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * width, Mathf.Sin(a) * height);
                if (i < 24) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uv;
            mesh.triangles = triangles;
            go.AddComponent<RuntimeMeshOwner>().Owned = mesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var render = go.AddComponent<MeshRenderer>();
            render.sharedMaterial = material;
            render.sortingOrder = order;
            return go;
        }
    }

    public sealed class FadeAndDestroy : MonoBehaviour {
        private float start, life;
        private SpriteRenderer render;
        private Vector3 scale;
        public void Begin(float duration) { start = Time.time; life = Mathf.Max(.05f, duration); render = GetComponent<SpriteRenderer>(); scale = transform.localScale; }
        private void Update() {
            float t = (Time.time - start) / life;
            if (t >= 1) { Destroy(gameObject); return; }
            if (!render) return;
            var color = render.color;
            color.a = t < .6f ? color.a : Mathf.Lerp(1, 0, (t - .6f) / .4f);
            render.color = color;
            transform.localScale = scale * (1 + t * .15f);
        }
    }
}
