using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualLab.Physics81
{
    internal sealed class Physics81InstrumentRig
    {
        const float ScaleStart = .112f, ScaleHeight = .145f;
        internal const float BulbHeight = .011f;
        readonly SetupFixtures lab;
        readonly Transform flame;
        readonly ParticleSystem flameParticles;
        readonly float flameLifetime, flameHeight;
        readonly Transform[] bubbles = new Transform[3];
        readonly Transform[] drops = new Transform[4];
        readonly ProbeColumn cupColumn, bathColumn;
        readonly LineRenderer thread;
        readonly Vector3 bathBob, cupBob;
        Vector3 coldBob;
        float immersion, shake, animationTime, splashAge = 1f;
        public const float CupContactProgress = .90f;
        bool bobMounted, fullFlame = true;
        public Vector3 ThreadGrip => lab.Bob.position + Vector3.up * .107f;
        public bool BobImmersed => immersion >= 1f;
        public Transform TransferView { get; }
        public Transform LampView { get; }
        public Transform FlameAnchor => flame;

        public Physics81InstrumentRig(SetupFixtures fixtures)
        {
            lab = fixtures;
            Transform slab = lab.CupRest.parent;
            Transform lamp = slab.GetComponentInParent<LabSetupSession>().Burner;
            var lampParts = lamp.GetComponentsInChildren<MeshRenderer>();
            Bounds lampBounds = lampParts[0].bounds;
            foreach (var part in lampParts) lampBounds.Encapsulate(part.bounds);
            var material = lab.Bob.GetComponentInChildren<Renderer>().sharedMaterial;
            flame = lamp.Find("Reference burner flame");
            if (flame == null)
            {
                flame = Prop("Burner flame", PrimitiveType.Sphere, lamp, material, new Color(.19f, .48f, .94f), new Vector3(.014f, .031f, .014f));
                flame.position = new Vector3(lampBounds.center.x, lampBounds.max.y + .010f, lampBounds.center.z);
                var core = Prop("Flame core", PrimitiveType.Sphere, flame, material, new Color(.66f, .91f, 1f), new Vector3(.48f, .56f, .48f));
                core.localPosition = Vector3.down * .12f;
            }
            flameParticles = flame.GetComponent<ParticleSystem>();
            if (flameParticles != null)
            {
                flameLifetime = flameParticles.main.startLifetimeMultiplier;
                flameHeight = flameParticles.main.startSizeYMultiplier;
            }
            flame.gameObject.SetActive(false);

            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i] = Prop("Boiling bubble", PrimitiveType.Sphere, lab.Bath.Body, material, new Color(.60f, .84f, .91f), Vector3.one * .007f);
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = Prop("Water splash", PrimitiveType.Sphere, lab.CupRest, material, new Color(.38f, .75f, .88f), Vector3.one * .006f);
                drops[i].gameObject.SetActive(false);
            }
            cupColumn = new ProbeColumn(lab.CupProbe, material);
            bathColumn = new ProbeColumn(lab.BathProbe, material);

            thread = new GameObject("Brass bob thread").AddComponent<LineRenderer>();
            thread.transform.SetParent(slab, false);
            thread.sharedMaterial = lab.Bob.GetChild(1).GetComponent<Renderer>().sharedMaterial;
            thread.widthMultiplier = .0016f;
            thread.positionCount = 2;
            thread.shadowCastingMode = ShadowCastingMode.Off;
            thread.enabled = false;

            bathBob = lab.BeakerTripod.TransformPoint(new Vector3(.018f, .013f, .010f));
            cupBob = lab.CupRest.TransformPoint(new Vector3(.005f, .003f, .022f));
            TransferView = View(slab, "Transfer inspection", (cupBob + bathBob) * .5f + Vector3.up * .095f, .72f, .32f);
            Vector3 burnerCentre = lampBounds.center + (slab.Find("Heating arrangement").position - lamp.position);
            LampView = View(slab, "Burner inspection", burnerCentre + Vector3.up * .08f, .43f, .13f);
        }

        public void LowerBob()
        {
            coldBob = lab.Bob.position;
            lab.Bob.SetParent(lab.CupRest.parent, true);
            bobMounted = true;
        }

        public ThermalInputs HeatInputs(SetupLedger data, bool stirring) => new(data.LampOn,
            BobFraction(lab.Bath, data.VisibleBathMl), BobFraction(lab.Cup, data.VisibleCupMl),
            lab.Lid.parent == lab.CupLid ? 0f : 1f, stirring,
            ProbeDepth(false, data.VisibleCupMl).wet, ProbeDepth(true, data.VisibleBathMl).wet, data.BurnerOutput);

        public (float clearance, float cover, float wet) ProbeDepth(bool bath, float ml)
        {
            SetupVessel vessel = bath ? lab.Bath : lab.Cup;
            Transform probe = bath ? lab.BathProbe : lab.CupProbe;
            Vector3 tip = vessel.Body.InverseTransformPoint(probe.position);
            float clearance = tip.y - vessel.Bottom;
            float waterAboveTip = vessel.Surface(ml) - tip.y;
            Vector2 lateral = new Vector2(tip.x, tip.z) - vessel.Centre;
            bool inside = probe.gameObject.activeInHierarchy && ml > 0 && clearance >= .001f && lateral.sqrMagnitude < .029f * .029f;
            return (clearance, waterAboveTip - BulbHeight, inside ? Mathf.Clamp01(waterAboveTip / BulbHeight) : 0);
        }

        public Vector3 ReadingPoint(bool bath, double temperature) => (bath ? bathColumn : cupColumn).Meniscus(temperature);

        internal static float ScaleY(double temperature) => ScaleStart + Mathf.Clamp01((float)temperature / 110f) * ScaleHeight;

        internal static Bounds GlassBounds(Transform probe, MeshFilter glass)
        {
            Bounds mesh = glass.sharedMesh.bounds;
            Matrix4x4 local = probe.worldToLocalMatrix * glass.transform.localToWorldMatrix;
            Bounds bounds = new(local.MultiplyPoint3x4(mesh.min), Vector3.zero);
            for (int corner = 1; corner < 8; corner++)
                bounds.Encapsulate(local.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                    (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                    (corner & 4) == 0 ? mesh.min.z : mesh.max.z)));
            return bounds;
        }

        public void Splash() => splashAge = 0f;

        public void Reset()
        {
            bobMounted = false;
            immersion = shake = animationTime = 0f;
            splashAge = 1f;
            thread.enabled = false;
        }

        public void DrawInstruments(SetupLedger data, float dt)
        {
            animationTime += dt;
            bool heating = data.BurnerOutput >= 1;
            if (flameParticles != null && heating != fullFlame)
            {
                var main = flameParticles.main;
                main.startLifetimeMultiplier = flameLifetime * (heating ? 1f : .55f);
                main.startSizeYMultiplier = flameHeight * (heating ? 1f : .60f);
                fullFlame = heating;
            }
            flame.gameObject.SetActive(data.LampOn);
            if (data.LampOn && flameParticles == null)
                flame.localScale = new Vector3(.014f, (heating ? .031f : .017f) + .002f * Mathf.Sin(animationTime * 20f), .014f);
            float bubbling = data.LampOn && data.Thermal != null
                ? Mathf.InverseLerp((float)data.Thermal.BoilingPoint - 8f, (float)data.Thermal.BoilingPoint - .05f, (float)data.Thermal.Bath)
                : 0f;
            for (int i = 0; i < bubbles.Length; i++)
            {
                float lift = Mathf.Repeat(animationTime * (.78f + i * .13f) + i * .31f, 1f);
                bool visible = bubbling > i * .26f && lift < Mathf.Lerp(.40f, 1f, bubbling);
                bubbles[i].gameObject.SetActive(visible);
                if (visible)
                {
                    bubbles[i].localPosition = new Vector3((i - 1) * .013f + .002f * Mathf.Sin(lift * 9f + i),
                        Mathf.Lerp(lab.Bath.Bottom, lab.Bath.Surface(data.VisibleBathMl), lift), .011f);
                    bubbles[i].localScale = Vector3.one * Mathf.Lerp(.003f, .007f, lift);
                }
            }
            splashAge += dt;
            for (int i = 0; i < drops.Length; i++)
            {
                bool splashing = splashAge < .38f;
                drops[i].gameObject.SetActive(splashing);
                if (splashing)
                    drops[i].localPosition = new Vector3((i - 1.5f) * (.015f + .17f * splashAge),
                        .105f + splashAge * (.51f - 1.25f * splashAge), -.034f - .18f * splashAge);
            }
            cupColumn.Draw(data.Thermal?.CupProbe ?? 25);
            bathColumn.Draw(data.Thermal?.BathProbe ?? 25);
        }

        public void MoveApparatus(SetupLedger data, bool stirring, float dt)
        {
            if (!bobMounted) return;
            immersion = Mathf.MoveTowards(immersion, 1f, dt * 1.15f);
            Vector3 hotPoint = bathBob + Vector3.up * .19f;
            if (!BobImmersed)
                lab.Bob.position = immersion < .62f ? Vector3.Lerp(coldBob, hotPoint, Mathf.SmoothStep(0f, 1f, immersion / .62f)) :
                    Vector3.Lerp(hotPoint, bathBob, Mathf.SmoothStep(0f, 1f, (immersion - .62f) / .38f));
            else
                lab.Bob.position = TransferPosition(data.TransferProgress);
            thread.enabled = data.Step < SetupStep.Review;
            if (thread.enabled)
            {
                thread.SetPosition(0, lab.Bob.position + Vector3.up * .029f);
                thread.SetPosition(1, data.TransferProgress <= 0f && BobImmersed ? hotPoint : lab.Bob.position + Vector3.up * .14f);
            }

            if (data.Step < SetupStep.MixCup) return;
            shake = Mathf.MoveTowards(shake, stirring ? 1 : 0, dt * 7f);
            float side = Mathf.Sin(animationTime * 14f) * shake;
            Vector3 offset = new(side * .004f, 0, 0);
            lab.Cup.Body.localPosition = offset;
            lab.Lid.localPosition = offset;
            if (data.TransferProgress >= 1f) lab.Bob.position += lab.CupRest.TransformVector(offset);
        }

        float BobFraction(SetupVessel vessel, float ml)
        {
            if (!bobMounted || ml <= 0) return 0;
            const float radius = .0072f;
            Vector3 centre = vessel.Body.InverseTransformPoint(lab.Bob.TransformPoint(Vector3.up * .012f));
            if (centre.x * centre.x + centre.z * centre.z > .036f * .036f) return 0;
            float height = Mathf.Clamp((vessel.Surface(ml) - centre.y + radius) / radius, 0, 2);
            return height * height * (3 - height) * .25f;
        }

        Vector3 TransferPosition(float progress)
        {
            const float lift = .26f, lower = .74f;
            float top = bathBob.y + .19f;
            Vector3 hotTop = new(bathBob.x, top, bathBob.z), cupTop = new(cupBob.x, top, cupBob.z);
            Vector3 cupSurface = cupBob + Vector3.up * .020f;
            if (progress < lift) return Vector3.Lerp(bathBob, hotTop, progress / lift);
            if (progress < lower) return Vector3.Lerp(hotTop, cupTop, (progress - lift) / (lower - lift));
            if (progress < CupContactProgress)
                return Vector3.Lerp(cupTop, cupSurface, (progress - lower) / (CupContactProgress - lower));
            return Vector3.Lerp(cupSurface, cupBob, (progress - CupContactProgress) / (1f - CupContactProgress));
        }

        static Transform View(Transform slab, string name, Vector3 focus, float distance, float height)
        {
            var view = new GameObject(name).transform;
            view.SetParent(slab, false);
            Vector3 eye = focus - slab.forward * distance + Vector3.up * height;
            view.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye));
            return view;
        }

        static Transform Prop(string name, PrimitiveType shape, Transform parent, Material material, Color colour, Vector3 size)
        {
            var prop = GameObject.CreatePrimitive(shape);
            prop.name = name;
            prop.transform.SetParent(parent, false);
            prop.transform.localScale = size;
            var collider = prop.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            var renderer = prop.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", colour);
            renderer.SetPropertyBlock(properties);
            return prop.transform;
        }

        sealed class ProbeColumn
        {
            const float ColumnBase = .0055f;
            readonly Transform column, probe;
            readonly Vector2 axis;
            readonly float diameter;

            public ProbeColumn(Transform probe, Material material)
            {
                this.probe = probe;
                var glass = probe.Find("ThermometerVisual/Thermomter001").GetComponent<MeshFilter>();
                Bounds shell = GlassBounds(probe, glass);
                // Render the column in the plane of the scale so the backing cannot obscure it.
                axis = new Vector2(-shell.size.x * .30f, -shell.extents.z - .0016f);
                diameter = Mathf.Clamp(shell.size.x * .16f, .0011f, .0018f);
                Color red = new(.92f, .06f, .11f);
                column = Prop("Thermometer liquid column", PrimitiveType.Cylinder, probe, material, red, Vector3.one);
                var bulb = Prop("Liquid in sensing bulb", PrimitiveType.Sphere, probe, material, red,
                    new Vector3(diameter * 1.45f, .007f, diameter * 1.45f));
                bulb.localPosition = new Vector3(axis.x, ColumnBase, -shell.extents.z * .43f);
            }

            public void Draw(double temperature)
            {
                float height = ScaleY(temperature) - ColumnBase;
                column.localScale = new Vector3(diameter, height * .5f, diameter);
                column.localPosition = new Vector3(axis.x, ColumnBase + height * .5f, axis.y);
            }

            public Vector3 Meniscus(double temperature) => probe.TransformPoint(new Vector3(axis.x, ScaleY(temperature), axis.y));
        }
    }
}
