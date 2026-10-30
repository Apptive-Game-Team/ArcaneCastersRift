using UnityEngine;

namespace ArcaneCasters.GameSystem {
    // Ground warning for an enemy attack. The outline is the exact damage area and the fill
    // grows until it reaches the outline at the moment damage is applied, so position, size
    // and timing on screen are the values the hit test uses (see ArcaneHitShapes).
    public sealed class ArcaneTelegraph : MonoBehaviour {
        public static readonly Color Danger = new Color(1f, .36f, .14f);

        private enum Shape { Circle, Lane }
        private Shape shape;
        private float start, duration;
        private Transform fill;
        private MeshRenderer fillRender, baseRender;
        private LineRenderer outline;

        public static ArcaneTelegraph Circle(Material material, Vector2 center, float radius, float duration) {
            var telegraph = Create(material, center, duration, Shape.Circle);
            telegraph.baseRender = ArcaneEffects.Disk(telegraph.transform, radius, radius, new Color(Danger.r, Danger.g, Danger.b, .14f), material, ArcaneSorting.Telegraph).GetComponent<MeshRenderer>();
            var fillDisk = ArcaneEffects.Disk(telegraph.transform, radius, radius, new Color(Danger.r, Danger.g, Danger.b, .32f), material, ArcaneSorting.Telegraph + 1);
            telegraph.fill = fillDisk.transform;
            telegraph.fillRender = fillDisk.GetComponent<MeshRenderer>();
            telegraph.fill.localScale = Vector3.zero;
            telegraph.outline = OutlineCircle(telegraph.transform, radius, material);
            return telegraph;
        }

        // A rectangle from origin along direction: length long, halfWidth to each side.
        public static ArcaneTelegraph Lane(Material material, Vector2 origin, Vector2 direction, float length, float halfWidth, float duration) {
            var telegraph = Create(material, origin, duration, Shape.Lane);
            telegraph.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            telegraph.baseRender = Rect(telegraph.transform, length, halfWidth, new Color(Danger.r, Danger.g, Danger.b, .12f), material, ArcaneSorting.Telegraph);
            var fillRect = Rect(telegraph.transform, length, halfWidth, new Color(Danger.r, Danger.g, Danger.b, .3f), material, ArcaneSorting.Telegraph + 1);
            telegraph.fill = fillRect.transform;
            telegraph.fillRender = fillRect;
            telegraph.fill.localScale = new Vector3(0, 1, 1);
            var line = telegraph.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = .04f;
            line.SetPositions(new[] { new Vector3(0, -halfWidth), new Vector3(length, -halfWidth), new Vector3(length, halfWidth), new Vector3(0, halfWidth) });
            line.sortingOrder = ArcaneSorting.Telegraph + 2;
            telegraph.outline = line;
            return telegraph;
        }

        private static ArcaneTelegraph Create(Material material, Vector2 at, float duration, Shape shape) {
            var go = new GameObject(shape == Shape.Circle ? "Telegraph circle" : "Telegraph lane");
            go.transform.position = at;
            var telegraph = go.AddComponent<ArcaneTelegraph>();
            telegraph.shape = shape;
            telegraph.start = Time.time;
            telegraph.duration = Mathf.Max(.05f, duration);
            return telegraph;
        }

        private static LineRenderer OutlineCircle(Transform parent, float radius, Material material) {
            var go = new GameObject("Outline");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.widthMultiplier = .045f;
            line.sortingOrder = ArcaneSorting.Telegraph + 2;
            for (int i = 0; i < 40; i++) {
                float a = i * Mathf.PI * 2 / 40;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            return line;
        }

        private static MeshRenderer Rect(Transform parent, float length, float halfWidth, Color color, Material material, int order) {
            var go = new GameObject("Area");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh {
                vertices = new[] { new Vector3(0, -halfWidth), new Vector3(length, -halfWidth), new Vector3(length, halfWidth), new Vector3(0, halfWidth) },
                colors = new[] { color, color, color, color },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            go.AddComponent<RuntimeMeshOwner>().Owned = mesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var render = go.AddComponent<MeshRenderer>();
            render.sharedMaterial = material;
            render.sortingOrder = order;
            return render;
        }

        // Called when the attack resolves or is cancelled (stun, death).
        public void Finish(bool fired) {
            if (fired) {
                if (fill) fill.localScale = Vector3.one;
                if (outline) outline.startColor = outline.endColor = new Color(1, .95f, .7f, 1);
                Destroy(gameObject, .12f);
            } else {
                Destroy(gameObject);
            }
            enabled = false;
        }

        private void Update() {
            float t = Mathf.Clamp01((Time.time - start) / duration);
            if (fill) fill.localScale = shape == Shape.Circle ? new Vector3(t, t, 1) : new Vector3(t, 1, 1);
            if (outline) {
                float pulse = .65f + .35f * Mathf.Sin(Time.time * 18f);
                outline.startColor = outline.endColor = new Color(Danger.r, Danger.g, Danger.b, t > .8f ? 1f : pulse);
            }
            if (Time.time > start + duration + 1f) Destroy(gameObject);
        }
    }

    // The damage tests matching the telegraph shapes. Bodies are circles on the ground plane.
    public static class ArcaneHitShapes {
        public const float PlayerRadius = .2f;

        public static bool InCircle(Vector2 center, float radius, Vector2 body, float bodyRadius) =>
            (body - center).sqrMagnitude <= (radius + bodyRadius) * (radius + bodyRadius);

        public static bool InLane(Vector2 origin, Vector2 direction, float length, float halfWidth, Vector2 body, float bodyRadius) {
            Vector2 local = body - origin;
            float along = Vector2.Dot(local, direction);
            float across = Mathf.Abs(local.x * direction.y - local.y * direction.x);
            return along >= -bodyRadius && along <= length + bodyRadius && across <= halfWidth + bodyRadius;
        }

        public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b) {
            Vector2 d = b - a;
            float t = d.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(point - a, d) / d.sqrMagnitude);
            return Vector2.Distance(point, a + d * t);
        }
    }
}
