using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiologyLab
{
    // Schematic geometry aligned to the imported dissection specimen.
    internal sealed class FemaleAnatomyModel : IDisposable
    {
        public const string RootName = "Female Reproductive Structures (Schematic)";
        private readonly Transform anatomy;
        private readonly List<GameObject> maleParts = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<Renderer, (Material[] Original, Material[] Ghost)> appearances =
            new Dictionary<Renderer, (Material[], Material[])>();
        private GameObject female;

        public FemaleAnatomyModel(Transform anatomy) { this.anatomy = anatomy; }

        public void Init()
        {
            foreach (Transform part in anatomy.Find("Internal Organs").GetComponentsInChildren<Transform>(true))
                if (part.name.StartsWith("Testis") || part.name.StartsWith("Tubule (Review)"))
                    maleParts.Add(part.gameObject);
        }

        public void Start()
        {
            female = new GameObject(RootName);
            female.transform.SetParent(anatomy, false);
            Shader shader = anatomy.Find("Internal Organs").GetComponentInChildren<Renderer>().sharedMaterial.shader;
            Material ovary = MakeMaterial(shader, "Ovarian tissue", new Color(.40f, .24f, .29f));
            Material ova = MakeMaterial(shader, "Ova", new Color(.28f, .21f, .23f));
            Material duct = MakeMaterial(shader, "Oviduct", new Color(.86f, .72f, .55f));
            Material sac = MakeMaterial(shader, "Ovisac", new Color(.68f, .36f, .33f));

            BuildSide(-1, "Right", ovary, ova, duct, sac);
            BuildSide(1, "Left", ovary, ova, duct, sac);
            SetFemale(false);
        }

        private void BuildSide(int side, string name, Material ovary, Material ova, Material duct, Material sac)
        {
            Transform kidney = anatomy.Find(side < 0 ? "Internal Organs/Kidney 1" : "Internal Organs/Kidney");
            Vector3 center = anatomy.InverseTransformPoint(kidney.GetComponent<Renderer>().bounds.center);
            center += new Vector3(side * .52f, .48f, .18f);
            var tissue = new Surface();
            AppendTube(tissue, 30, 18, t => center + new Vector3(side * .11f * Mathf.Sin(Mathf.PI * t), .06f * Mathf.Sin(t * 5f), Mathf.Lerp(.89f, -.89f, t)), OvaryProfile);
            int ovaStart = tissue.Triangles.Count;
            var random = new System.Random(side < 0 ? 814 : 291);
            // Embed irregularly spaced ova in one lobulated ovary mesh.
            for (int i = 0; i < 90; i++)
            {
                float t = .06f + .88f * (i + (float)random.NextDouble()) / 90f;
                float angle = i * 2.39996f;
                Vector2 profile = OvaryProfile(t);
                float radius = Mathf.Lerp(.048f, .084f, (float)random.NextDouble());
                Vector3 egg = center + new Vector3(
                    side * .11f * Mathf.Sin(Mathf.PI * t) + Mathf.Cos(angle) * profile.x,
                    .06f * Mathf.Sin(t * 5f) + Mathf.Sin(angle) * profile.y,
                    Mathf.Lerp(.89f, -.89f, t));
                AppendTube(tissue, 9, 12, u => egg + Vector3.forward * Mathf.Lerp(-radius, radius, u),
                    u => Vector2.one * Mathf.Max(.001f, radius * Mathf.Sin(Mathf.PI * u)));
            }
            AddPart(name + " ovary", tissue, center + Vector3.up * .24f, ovaStart, ovary, ova);

            Vector3 inlet = new Vector3(side * 1.36f, 3.67f, 8.46f);
            Vector3 start = new Vector3(side * 1.50f, 3.57f, 8.17f);
            Vector3 junction = new Vector3(side * .68f, 3.78f, 3.69f);
            Vector3 exit = new Vector3(side * .12f, 4.22f, 2.78f);
            var funnel = new Surface();
            // Trace the outer lip and inner wall so the anterior opening reads as an inlet.
            Vector3 lip = inlet + new Vector3(side * -.06f, .08f, .20f);
            Vector3[] funnelPath = { start, Vector3.Lerp(start, lip, .55f), lip, lip, Vector3.Lerp(start, lip, .55f), Vector3.Lerp(start, lip, .35f) };
            float[] funnelRadii = { .071f, .103f, .176f, .125f, .069f, .012f };
            AppendTube(funnel, funnelPath.Length - 1, 18, t => funnelPath[Mathf.RoundToInt(t * 5)],
                t => Vector2.one * funnelRadii[Mathf.RoundToInt(t * 5)], (lip - start).normalized);
            AddPart(name + " oviduct funnel", funnel, lip + Vector3.up * .14f, 0, duct);

            Vector3 DuctPath(float t)
            {
                float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 9f)) *
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) * 6f));
                float lateral = Mathf.Lerp(1.50f, 1.71f, Mathf.SmoothStep(0f, 1f, t * 4f));
                lateral = Mathf.Lerp(lateral, .68f, Mathf.SmoothStep(0f, 1f, (t - .76f) / .24f));
                float folds = t * Mathf.PI * 17f + .34f * Mathf.Sin(t * 13f);
                return new Vector3(side * (lateral + envelope * .24f * Mathf.Sin(folds)),
                    Mathf.Lerp(start.y, junction.y, t) - .19f * Mathf.Sin(Mathf.PI * t) + envelope * .08f * Mathf.Cos(folds),
                    Mathf.Lerp(start.z, junction.z, t));
            }
            var oviduct = new Surface();
            AppendTube(oviduct, 160, 14, DuctPath, t => Vector2.one * Mathf.Lerp(.071f, .092f, t));
            AddPart(name + " oviduct", oviduct, DuctPath(.72f) + Vector3.up * .08f, 0, duct);

            Vector3 SacPath(float t) => Vector3.Lerp(junction, exit, t) +
                new Vector3(side * .14f, .07f, 0f) * Mathf.Sin(Mathf.PI * t);
            var ovisac = new Surface();
            AppendTube(ovisac, 26, 18, SacPath, t =>
            {
                float bulge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 1.4f);
                float neck = Mathf.Lerp(.092f, .052f, t);
                return new Vector2(neck + .17f * bulge, neck + .10f * bulge);
            });
            AddPart(name + " ovisac", ovisac, SacPath(.48f) + Vector3.up * .17f, 0, sac);
        }

        private static Vector2 OvaryProfile(float t)
        {
            float taper = Mathf.Max(.01f, Mathf.Sin(Mathf.PI * t));
            float lobes = .89f + .15f * Mathf.Sin(24f * t) + .07f * Mathf.Sin(47f * t);
            return new Vector2(.38f, .22f) * (Mathf.Pow(taper, .65f) * lobes);
        }

        private sealed class Surface
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
        }

        private static void AppendTube(Surface surface, int segments, int sides, Func<float, Vector3> path,
            Func<float, Vector2> radius, Vector3? fixedAxis = null)
        {
            int offset = surface.Vertices.Count;
            for (int ring = 0; ring <= segments; ring++)
            {
                float t = ring / (float)segments;
                Vector3 point = path(t);
                Vector3 tangent = fixedAxis ?? (path(Mathf.Min(1f, t + 1f / segments)) - path(Mathf.Max(0f, t - 1f / segments))).normalized;
                Vector3 right = Vector3.Cross(tangent, Vector3.up).normalized;
                Vector3 up = Vector3.Cross(right, tangent);
                Vector2 size = radius(t);
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * Mathf.PI * 2f / sides;
                    surface.Vertices.Add(point + right * (size.x * Mathf.Cos(angle)) + up * (size.y * Mathf.Sin(angle)));
                    if (ring == segments) continue;
                    int a = offset + ring * sides + j;
                    int b = offset + ring * sides + (j + 1) % sides;
                    surface.Triangles.Add(a); surface.Triangles.Add(a + sides); surface.Triangles.Add(b);
                    surface.Triangles.Add(b); surface.Triangles.Add(a + sides); surface.Triangles.Add(b + sides);
                }
            }
        }

        private Material MakeMaterial(Shader shader, string name, Color color)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .27f);
            materials.Add(material);
            return material;
        }

        private void AddPart(string name, Surface surface, Vector3 landmark, int ovaStart, params Material[] original)
        {
            var mesh = new Mesh { name = name + " (Schematic)" };
            mesh.SetVertices(surface.Vertices);
            mesh.subMeshCount = original.Length;
            if (ovaStart > 0)
            {
                mesh.SetTriangles(surface.Triangles.GetRange(0, ovaStart), 0);
                mesh.SetTriangles(surface.Triangles.GetRange(ovaStart, surface.Triangles.Count - ovaStart), 1);
            }
            else mesh.SetTriangles(surface.Triangles, 0);
            mesh.RecalculateNormals();
            meshes.Add(mesh);

            var part = new GameObject(name);
            part.transform.SetParent(female.transform, false);
            part.layer = 31;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = original;
            var ghost = Array.ConvertAll(original, LayerMaterials.MakeGhost);
            materials.AddRange(ghost);
            appearances.Add(renderer, (original, ghost));

            var anchor = new GameObject("Surface Landmark");
            anchor.transform.SetParent(part.transform, false);
            Vector3 nearest = surface.Vertices[0];
            foreach (Vector3 vertex in surface.Vertices)
                if ((vertex - landmark).sqrMagnitude < (nearest - landmark).sqrMagnitude) nearest = vertex;
            anchor.transform.localPosition = nearest;
        }

        public void SetFemale(bool value)
        {
            female.SetActive(value);
            foreach (GameObject part in maleParts) part.SetActive(!value);
        }

        public void SetGhost(bool value)
        {
            foreach (var pair in appearances)
            {
                pair.Key.enabled = true;
                pair.Key.sharedMaterials = value ? pair.Value.Ghost : pair.Value.Original;
            }
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(female);
            foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            foreach (Material material in materials) UnityEngine.Object.Destroy(material);
        }
    }
}
