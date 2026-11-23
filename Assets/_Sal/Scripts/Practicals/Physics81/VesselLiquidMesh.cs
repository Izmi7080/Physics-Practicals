using UnityEngine;

namespace VirtualLab.Physics81
{
    // A tapered interior fitted to the vessel, with a level free surface as the vessel tips.
    internal sealed class VesselLiquidMesh
    {
        const int Segments = 40, Rings = 5;
        readonly SetupVessel vessel;
        readonly Mesh mesh;
        readonly float lowRadius, highRadius;
        readonly Vector3[] vertices = new Vector3[Segments * Rings + 2];
        readonly Vector2[] directions = new Vector2[Segments];
        float lastVolume = -1;
        Vector3 lastUp;

        public VesselLiquidMesh(SetupVessel vessel, float lowRadius, float highRadius)
        {
            this.vessel = vessel;
            this.lowRadius = lowRadius;
            this.highRadius = highRadius;
            for (int i = 0; i < Segments; i++)
            {
                float angle = 2 * Mathf.PI * i / Segments;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            var liquid = vessel.Liquid;
            liquid.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            liquid.localScale = Vector3.one;
            mesh = new Mesh { name = vessel.Body.name + " fitted water" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Segments; i++)
            {
                int next = (i + 1) % Segments;
                for (int ring = 0; ring < Rings - 1; ring++)
                {
                    int a = ring * Segments + i, b = ring * Segments + next;
                    triangles.AddRange(new[] { a, a + Segments, b + Segments, a, b + Segments, b });
                }
                int top = (Rings - 1) * Segments;
                triangles.AddRange(new[] { vertices.Length - 2, i, next, vertices.Length - 1, top + next, top + i });
            }
            mesh.SetTriangles(triangles, 0);
            liquid.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        public float Surface(float volume)
        {
            float fraction = Mathf.Clamp01(volume / vessel.Capacity);
            if (Mathf.Approximately(lowRadius, highRadius)) return vessel.Bottom + vessel.FillHeight * fraction;
            float low = 0, high = 1;
            float taper = highRadius - lowRadius;
            float target = fraction * Integral(1);
            for (int i = 0; i < 12; i++)
            {
                float mid = (low + high) * .5f;
                if (Integral(mid) < target) low = mid; else high = mid;
            }
            return vessel.Bottom + vessel.FillHeight * (low + high) * .5f;

            float Integral(float h) => h * (lowRadius * lowRadius + h * (lowRadius * taper + h * taper * taper / 3f));
        }

        public void Draw(float volume)
        {
            Vector3 up = vessel.Body.InverseTransformDirection(Vector3.up);
            if (Mathf.Abs(lastVolume - volume) < .0001f && (lastUp - up).sqrMagnitude < 1e-8f) return;
            lastVolume = volume;
            lastUp = up;
            float bottom = vessel.Bottom;
            float height = vessel.FillHeight;
            float target = vessel.Surface(volume);
            var tilt = new Vector2(-up.x, -up.z) / Mathf.Max(.3f, up.y);
            float centralHeight = target;
            if (tilt.sqrMagnitude > .0001f)
            {
                // Preserve the filled fraction while the horizontal surface clips the bottom or mouth.
                float lower = bottom - lowRadius * tilt.magnitude, upper = bottom + height + lowRadius * tilt.magnitude;
                for (int pass = 0; pass < 11; pass++)
                {
                    centralHeight = (lower + upper) * .5f;
                    float total = 0;
                    foreach (var direction in directions) total += EdgeHeight(centralHeight, Vector2.Dot(tilt, direction));
                    float average = (centralHeight + 2 * total / Segments) / 3;
                    if (average < target) lower = centralHeight; else upper = centralHeight;
                }
            }

            for (int i = 0; i < Segments; i++)
            {
                Vector2 direction = directions[i];
                float top = EdgeHeight(centralHeight, Vector2.Dot(tilt, direction));
                if (tilt.sqrMagnitude < .0001f) top = Mathf.Min(bottom + height, top + .00075f);
                for (int ring = 0; ring < Rings; ring++)
                {
                    float y = Mathf.Lerp(bottom, top, ring / (Rings - 1f));
                    float radius = Mathf.Lerp(lowRadius, highRadius, (y - bottom) / height);
                    Vector2 xz = vessel.Centre + direction * radius;
                    vertices[ring * Segments + i] = new Vector3(xz.x, y, xz.y);
                }
            }
            vertices[vertices.Length - 2] = new Vector3(vessel.Centre.x, bottom, vessel.Centre.y);
            vertices[vertices.Length - 1] = new Vector3(vessel.Centre.x, Mathf.Clamp(centralHeight, bottom, bottom + height), vessel.Centre.y);
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        float EdgeHeight(float centre, float slope)
        {
            float start = vessel.Bottom, span = vessel.FillHeight;
            float taper = (highRadius - lowRadius) / span;
            float y = (centre + slope * (lowRadius - taper * start)) / (1 - slope * taper);
            return Mathf.Clamp(y, start + .00015f, start + span);
        }
    }
}
