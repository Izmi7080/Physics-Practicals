using UnityEngine;

namespace VirtualLab.Physics81
{
    // Local bench poses, independent of the room's position in a host scene.
    public static class Physics81BenchLayout
    {
        const float RackTop = .011f;

        public static void Arrange(SetupFixtures lab)
        {
            Transform slab = lab.CupRest.parent;
            lab.Balance.transform.localPosition = new Vector3(-.56f, 0f, .015f);
            lab.JugRest.localPosition = new Vector3(-.30f, 0f, .29f);
            lab.CylinderRest.localPosition = new Vector3(-.115f, 0f, .29f);
            lab.JugPour.localPosition = lab.CylinderRest.localPosition + new Vector3(-.185f, .165f, 0f);
            lab.CupRest.localPosition = new Vector3(.045f, 0f, -.040f);
            lab.Lid.localPosition = new Vector3(.181f, 0f, -.042f);
            lab.BobRest.localPosition = new Vector3(-.43f, RackTop, -.19f);
            lab.BeakerRest.localPosition = new Vector3(.56f, 0f, -.081f);

            // Stop at fixed anchors while setup starts; fitting a tool still resets
            // its local rotation and places it at the existing functional fixture.
            Rest(lab.CupProbe, new Vector3(-.122f, RackTop, -.297f));
            Rest(lab.Stirrer, new Vector3(.146f, RackTop, -.297f));
            Rest(lab.BathProbe, new Vector3(.375f, RackTop, -.297f));

            View(slab, lab.BalanceView, new Vector3(-.56f, .15f, .015f), .46f, .19f);
            View(slab, lab.MeasureView, new Vector3(-.163f, .122f, .29f), .46f, .12f);
            View(slab, lab.CupView, new Vector3(.052f, .097f, -.04f), .39f, .14f);
            View(slab, lab.BenchView, new Vector3(.016f, .115f, .007f), 1.12f, .66f);

            var plinth = lab.Balance.transform.Find("Balance plinth");
            if (slab.Find("Apparatus rests") != null || plinth == null || !plinth.TryGetComponent<Renderer>(out var renderer)) return;
            var rests = new GameObject("Apparatus rests").transform;
            rests.SetParent(slab, false);
            Pad(rests, "Low tool rack", new Vector3(.235f, RackTop * .5f, -.297f), new Vector3(.76f, RackTop, .054f), renderer.sharedMaterial);
            Pad(rests, "Bob cradle", new Vector3(-.43f, RackTop * .5f, -.19f), new Vector3(.063f, RackTop, .053f), renderer.sharedMaterial);
        }

        static void Rest(Transform tool, Vector3 pose)
        {
            tool.SetLocalPositionAndRotation(pose, Quaternion.Euler(0f, 0f, -90f));
            // Fit the visible model to the top of the rack rather than assuming
            // the bottom of an imported vertical model is its sideways contact.
            float bottom = float.PositiveInfinity;
            foreach (var part in tool.GetComponentsInChildren<Renderer>())
                if (part.enabled) bottom = Mathf.Min(bottom, part.bounds.min.y);
            if (!float.IsPositiveInfinity(bottom))
                tool.position += Vector3.up * (tool.parent.TransformPoint(pose).y - bottom);
        }

        static void View(Transform slab, Transform camera, Vector3 localFocus, float distance, float elevation)
        {
            Vector3 focus = slab.TransformPoint(localFocus);
            Vector3 eye = focus - slab.forward * distance + Vector3.up * elevation;
            camera.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye));
        }

        static void Pad(Transform owner, string name, Vector3 centre, Vector3 size, Material material)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = name;
            var collider = pad.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            pad.transform.SetParent(owner, false);
            pad.transform.localPosition = centre;
            pad.transform.localScale = size;
            pad.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
