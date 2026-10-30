using UnityEngine;
using ArcaneCasters.DataSystem;

namespace ArcaneCasters.GameSystem {
    public sealed class ArcaneActorView : MonoBehaviour {
        public SpriteRenderer Body { get; private set; }
        public float Altitude;
        public bool moving, casting;
        public Vector2 facing = Vector2.right;
        private ArcaneAssets assets;
        private float height, flash;
        private bool mage;
        public Vector2 BodyPosition => Body.transform.position;
        public void Init(ArcaneAssets library, string sprite, float size, float altitude = 0) {
            assets = library; height = size; Altitude = altitude; mage = sprite == "mage";
            Body = new GameObject("Body").AddComponent<SpriteRenderer>(); Body.transform.SetParent(transform, false);
            Body.sharedMaterial = assets.material;
            Body.sprite = mage ? assets.mageFrames[0] : assets.Get(sprite);
            RiftVisual.Disk(transform, Vector2.zero, .22f, .075f, new Color(0, .07f, .04f, .35f), assets.material, -5);
        }
        public void Flash() => flash = .12f;
        private void LateUpdate() {
            if (!Body) return;
            if (mage) {
                int direction = (Mathf.RoundToInt(Mathf.Atan2(facing.y, facing.x) / (Mathf.PI / 4)) + 8) % 8;
                int column = direction;
                int row = casting ? 3 : moving ? 1 + (Mathf.FloorToInt(Time.time * 9) % 2) : 0;
                Body.sprite = assets.mageFrames[row * 8 + column]; Body.flipX = false;
            }
            if (Body.sprite) Body.transform.localScale = Vector3.one * (height / Body.sprite.bounds.size.y);
            float bob = Altitude > 0 ? Mathf.Sin(Time.time * 3) * .06f : moving ? Mathf.Sin(Time.time * 16) * .015f : 0;
            Body.transform.localPosition = new Vector3(0, height * .42f + Altitude + bob, 0);
            Body.sortingOrder = -(int)(transform.position.y * 100) + 10;
            flash -= Time.deltaTime; Body.color = flash > 0 ? new Color(2, 1.6f, 1.6f) : Color.white;
        }
    }

    public static class RiftVisual {
        public static GameObject Ring(Vector2 center, float radius, Color color, Material material, float life = .5f) {
            var go = new GameObject("Spell cue"); var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.loop = true; line.positionCount = 48;
            line.startWidth = line.endWidth = .035f; line.startColor = line.endColor = color; line.sortingOrder = 20000;
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2 / 48; line.SetPosition(i, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius); }
            Object.Destroy(go, life); return go;
        }
        public static GameObject Line(Vector2 from, Vector2 to, Color color, Material material, float width = .06f, float life = .3f) {
            var go = new GameObject("Spell line"); var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.positionCount = 2; line.SetPosition(0, from); line.SetPosition(1, to); line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color; line.sortingOrder = 20001; Object.Destroy(go, life); return go;
        }
        public static void Disk(Transform parent, Vector2 position, float width, float height, Color color, Material material, int order) {
            var go = new GameObject("Ground shadow"); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var mesh = new Mesh(); var vertices = new Vector3[26]; var colors = new Color[26]; var uv = new Vector2[26]; var triangles = new int[72];
            for (int i = 0; i < 26; i++) { colors[i] = color; uv[i] = Vector2.one * .5f; }
            for (int i = 0; i <= 24; i++) { float a = i * Mathf.PI * 2 / 24; vertices[i + 1] = new Vector3(Mathf.Cos(a) * width, Mathf.Sin(a) * height); if (i < 24) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; } }
            mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles;
            go.AddComponent<RuntimeMeshOwner>().Owned = mesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var render = go.AddComponent<MeshRenderer>(); render.sharedMaterial = material; render.sortingOrder = order;
        }
    }
}
