using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualLab.Physics81
{
    public sealed partial class LabSetupSession
    {
        Transform cupReadingView, bathReadingView, depthRuler, depthPointer;
        Renderer cupTick, bathTick;
        float cylinderTip;
        static readonly Vector3 CylinderSpout = new(.0285f, .187f, 0);
        static readonly Color TickInk = new(.10f, .32f, .36f), GuideInk = new(.16f, 1f, .68f);
        MaterialPropertyBlock tickColour;

        void BuildVesselVisuals()
        {
            tickColour = new MaterialPropertyBlock();
            var cylinderModel = lab.Cylinder.Body.Find("Graduated_Cylinder");
            if (cylinderModel != null)
            {
                Vector3 s = cylinderModel.localScale;
                cylinderModel.localScale = new Vector3(s.x * 1.73f, s.y, s.z * 1.73f);
            }
            var paint = lab.Balance.transform.Find("Balance plinth").GetComponent<Renderer>().sharedMaterial;
            lab.Cylinder.Fit(.009f, .176f, Vector2.zero, .0186f, .0186f);
            lab.Cup.Fit(.007f, .095f, Vector2.zero, .028f, .044f);
            lab.Bath.Fit(.009f, .097f, new Vector2(-.0025f, 0), .035f, .038f);

            cupTick = Graduations(lab.Cylinder, paint, .0216f, 10, 50, 30);
            bathTick = Graduations(lab.Bath, paint, .044f, 25, 50, 150);
            ThermometerMarks(lab.CupProbe, paint);
            ThermometerMarks(lab.BathProbe, paint);

            Transform slab = lab.CupRest.parent;
            cupReadingView = Child(slab, "Cup thermometer reading view", Vector3.zero);
            bathReadingView = Child(slab, "Bath thermometer reading view", Vector3.zero);
            depthRuler = Child(lab.CupRest, "Cup bottom clearance scale", new Vector3(.052f, lab.Cup.Bottom, -.015f));
            for (int mm = 0; mm <= 30; mm += 5)
            {
                float height = mm / 1000f;
                MarkBar(depthRuler, "Depth graduation", Vector3.up * height, mm % 10 == 0 ? .008f : .004f, .001f,
                    paint, mm == 10 ? GuideInk : Color.white);
                if (mm % 10 == 0) MarkLabel(depthRuler, new Vector3(.006f, height, -.001f), mm.ToString(), .0018f, Color.white);
            }
            MarkLabel(depthRuler, new Vector3(.002f, .04f, -.001f), "mm", .0016f, Color.white);
            depthPointer = MarkBar(depthRuler, "Bulb clearance pointer", Vector3.zero, .012f, .0022f, paint, new Color(1f, .67f, .20f));
        }

        Renderer Graduations(SetupVessel vessel, Material paint, float front, int spacing, int major, int target)
        {
            foreach (var name in new[] { "30 mL line", "150 mL line" })
                if (vessel.Body.Find(name) is Transform oldMark) oldMark.gameObject.SetActive(false);
            Transform scale = Child(vessel.Body, "Graduations in mL", new Vector3(vessel.Centre.x, 0, -front));
            Renderer guide = null;
            for (int amount = spacing; amount <= vessel.Capacity; amount += spacing)
            {
                float height = vessel.Surface(amount);
                bool longTick = amount % major == 0 || amount == target;
                float width = longTick ? .011f : .006f;
                var tick = MarkBar(scale, amount + " mL graduation", new Vector3(-width * .5f, height, 0), width, longTick ? .0014f : .0009f, paint, TickInk);
                if (longTick) MarkLabel(scale, new Vector3(.003f, height, -.001f), amount.ToString(), .0024f, Color.white);
                if (amount == target) guide = tick.GetComponent<Renderer>();
            }
            MarkLabel(scale, new Vector3(.003f, vessel.Surface(vessel.Capacity) + .006f, -.001f), "mL", .0022f, Color.white);
            return guide;
        }

        void ThermometerMarks(Transform probe, Material paint)
        {
            var model = probe.Find("ThermometerVisual");
            var glass = model.Find("Thermomter001").GetComponent<MeshFilter>();
            Bounds shell = Physics81InstrumentRig.GlassBounds(probe, glass);
            // The donor mesh runs along its own Z axis, which points up after import.
            model.localScale = Vector3.Scale(model.localScale, new Vector3(1, 1, .277f / shell.size.y));
            shell = Physics81InstrumentRig.GlassBounds(probe, glass);
            model.localPosition -= new Vector3(shell.center.x, shell.min.y, shell.center.z);
            foreach (var part in model.GetComponentsInChildren<Transform>())
                if (part.name is "Text001" or "MovableMercurry" or "MercurryBottom") part.gameObject.SetActive(false);

            float width = shell.size.x;
            var scale = Child(probe, "Printed thermometer scale", new Vector3(0, 0, -shell.extents.z - .0005f));
            MarkBar(scale, "Scale backing", new Vector3(0, .189f, 0), width * .94f, .162f,
                paint, new Color(.055f, .08f, .10f));
            Color ink = new(.98f, 1f, .98f);
            for (int degree = 0; degree <= 110; degree++)
            {
                float y = Physics81InstrumentRig.ScaleY(degree);
                bool major = degree % 5 == 0;
                float tick = width * (major ? .25f : .12f);
                MarkBar(scale, "Thermometer graduation", new Vector3(-width * .14f + tick * .5f, y, -.0009f),
                    tick, major ? .00043f : .00028f, paint, ink);
                if (major) MarkLabel(scale, new Vector3(width * .145f, y, -.00145f), degree.ToString(), width * .15f, ink, true);
            }
            MarkLabel(scale, new Vector3(width * .145f, .267f, -.00145f), "°C", width * .15f, ink, true);
        }

        public bool InspectingReading => Rig.IsViewing(cupReadingView) || Rig.IsViewing(bathReadingView);
        public Vector3 ReadingPoint => ProbeReadingPoint(ShowsBathScale);
        Vector3 ProbeReadingPoint(bool bath)
        {
            double reading = bath ? Data.Thermal.BathProbe : Data.Thermal.CupProbe;
            return instruments.ReadingPoint(bath, reading);
        }

        Transform ThermometerView(bool bath)
        {
            Vector3 focus = ProbeReadingPoint(bath);
            float distance = .13f;
            if (Step == SetupStep.WatchBoil)
            {
                // Hold an eye-level view of the whole rise and the bath surface.
                float top = instruments.ReadingPoint(true, 110).y;
                float surface = lab.Bath.SurfaceCentre(Data.VisibleBathMl).y;
                focus.y = (top + surface) * .5f;
                distance = .36f;
            }
            Vector3 eye = focus - lab.CupRest.parent.forward * distance;
            Transform marker = bath ? bathReadingView : cupReadingView;
            marker.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye));
            return marker;
        }

        void BuildCupLid()
        {
            Transform slab = lab.CupRest.parent;
            lab.Lid.localPosition = new Vector3(.163f, .003f, -.17f);
            Material foam = lab.Lid.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            foreach (var part in lab.Lid.GetComponentsInChildren<MeshRenderer>()) part.enabled = false;
            var lid = Child(lab.Lid, "Lid with thermometer opening", Vector3.zero);
            lid.gameObject.AddComponent<MeshFilter>().sharedMesh = PerforatedLid();
            lid.gameObject.AddComponent<MeshRenderer>().sharedMaterial = foam;
        }

        static Mesh PerforatedLid()
        {
            const float radius = .054f, hole = .008f, thickness = .007f, centre = -.019f;
            const int segments = 64;
            var vertices = new List<Vector3>(segments * 4);
            var indices = new List<int>(segments * 24);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float dx = Mathf.Cos(angle), dz = Mathf.Sin(angle);
                float edge = -centre * dx + Mathf.Sqrt(radius * radius - centre * centre * dz * dz);
                Vector3 inner = new(centre + hole * dx, 0, hole * dz), outer = new(centre + edge * dx, 0, edge * dz);
                vertices.Add(inner); vertices.Add(outer); vertices.Add(inner + Vector3.up * thickness); vertices.Add(outer + Vector3.up * thickness);
                int a = i * 4, b = (i + 1) % segments * 4;
                Quad(a, a + 1, b + 1, b);
                Quad(a + 2, b + 2, b + 3, a + 3);
                Quad(a, b, b + 2, a + 2);
                Quad(a + 1, a + 3, b + 3, b + 1);
            }
            var mesh = new Mesh { name = "Polystyrene lid with probe hole" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals();
            return mesh;

            void Quad(int a, int b, int c, int d) { indices.AddRange(new[] { a, b, c, a, c, d }); }
        }

        void UpdatePourPose(float dt)
        {
            bool pouring = Step == SetupStep.PourCup;
            cylinderTip = Mathf.MoveTowards(cylinderTip, pouring ? 1f : 0f, dt / .36f);
            if (!pouring || lab.Cylinder.Body.parent != lab.CylinderRest) return;
            var rest = lab.CylinderRest;
            Quaternion endRotation = rest.rotation * Quaternion.Euler(0, 0, -65f);
            Vector3 end = lab.Cup.Body.TransformPoint(Vector3.up * .135f) - endRotation * CylinderSpout;
            float blend = Mathf.SmoothStep(0, 1, cylinderTip);
            lab.Cylinder.Body.SetPositionAndRotation(Vector3.Lerp(rest.position, end, blend) + Vector3.up * (Mathf.Sin(Mathf.PI * blend) * .045f),
                Quaternion.Slerp(rest.rotation, endRotation, blend));
        }

        void DrawVessels()
        {
            SetGuide(cupTick, Data.Guided && Step == SetupStep.MeasureCup);
            SetGuide(bathTick, Data.Guided && Step == SetupStep.MeasureBath);
            bool showDepth = Step == SetupStep.SetCupDepth;
            depthRuler.gameObject.SetActive(showDepth);
            if (showDepth)
            {
                float clearance = instruments.ProbeDepth(false, Data.VisibleCupMl).clearance;
                depthPointer.localPosition = new Vector3(-.011f, clearance, -.002f);
            }
            lab.Cylinder.Draw(Data.CylinderMl);
            lab.Cup.Draw(Data.VisibleCupMl);
            lab.Bath.Draw(Data.VisibleBathMl);
            if (Data.TapOn && !Paused && Step == SetupStep.MeasureCup)
                Stream(lab.Tap.position, lab.Cylinder.SurfaceCentre(Data.CylinderMl));
            else if (Data.TapOn && !Paused && Step == SetupStep.MeasureBath)
                Stream(lab.Tap.position, lab.Bath.SurfaceCentre(Data.BathMl));
            else if (Step == SetupStep.PourCup && cylinderTip >= .99f && !Paused)
                Stream(lab.Cylinder.Body.TransformPoint(CylinderSpout), lab.Cup.SurfaceCentre(Data.CupMl));
            else lab.Stream.gameObject.SetActive(false);
        }

        void SetGuide(Renderer mark, bool highlight)
        {
            tickColour.SetColor("_BaseColor", highlight ? GuideInk : TickInk);
            mark.SetPropertyBlock(tickColour);
        }

        static void Frame(Transform marker, Transform slab, Vector3 focus, float distance, float height)
        {
            Vector3 point = slab.TransformPoint(focus);
            Vector3 eye = point - slab.forward * distance + Vector3.up * height;
            marker.SetPositionAndRotation(eye, Quaternion.LookRotation(point - eye));
        }

        static Transform Child(Transform parent, string name, Vector3 local)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.localPosition = local;
            return child;
        }

        static Transform MarkBar(Transform parent, string name, Vector3 centre, float width, float height, Material mat, Color tint)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var child = obj.transform;
            child.SetParent(parent, false); child.localPosition = centre; child.localScale = new Vector3(width, height, .0008f);
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat; renderer.shadowCastingMode = ShadowCastingMode.Off;
            var props = new MaterialPropertyBlock(); props.SetColor("_BaseColor", tint); renderer.SetPropertyBlock(props);
            return child;
        }

        static void MarkLabel(Transform parent, Vector3 point, string content, float size, Color colour, bool bold = false)
        {
            var child = Child(parent, content, point);
            var text = child.gameObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content; text.fontSize = 42; text.characterSize = size;
            if (bold) text.fontStyle = FontStyle.Bold;
            text.color = colour; text.anchor = TextAnchor.MiddleLeft;
            child.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        }
    }
}
