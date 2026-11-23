using System.Collections.Generic;
using UnityEngine;

namespace VirtualLab.Physics141
{
    internal sealed class OhmBindingPosts
    {
        sealed class Post
        {
            internal Transform Cap, Stem;
            internal float Lift;
            internal bool Open;
        }

        readonly struct Wrap
        {
            internal readonly LineRenderer Line;
            internal readonly Transform Stem;
            internal readonly bool Sample;
            internal readonly float StartAngle;
            internal readonly float Start;

            internal Wrap(LineRenderer line, Transform stem, bool sample, float startAngle)
            {
                Line = line;
                Stem = stem;
                Sample = sample;
                StartAngle = startAngle;
                Start = Time.unscaledTime;
            }
        }

        readonly Transform[] sockets;
        readonly Post[] posts;
        readonly List<Wrap> wraps = new();

        internal OhmBindingPosts(OhmBench bench)
        {
            sockets = bench.Sockets;
            posts = new Post[sockets.Length];
            for (int i = 0; i < sockets.Length; i++)
            {
                if (!IsScrewPost((Terminal)i)) continue;
                var socket = sockets[i];
                foreach (var face in socket.GetComponentsInChildren<Renderer>()) face.enabled = false;
                Part(socket, "Binding post foot", new Vector3(0, -.004f, 0), new Vector3(.027f, .005f, .027f), bench.Brass);
                var stem = Part(socket, "Exposed metal stem", Vector3.zero, new Vector3(.011f, .015f, .011f), bench.Brass);
                var cap = new GameObject("Screw cap").transform;
                cap.SetParent(socket, false);
                Part(cap, "Thumb cap", Vector3.zero, new Vector3(.026f, .007f, .026f), bench.Rubber);
                Part(cap, "Cap turning mark", new Vector3(.006f, .0071f, 0), new Vector3(.005f, .0006f, .005f), bench.Brass);
                posts[i] = new Post { Cap = cap, Stem = stem };
            }
            Reset();
        }

        internal static bool IsScrewPost(Terminal terminal) => terminal is Terminal.KeyIn or Terminal.KeyOut
            or Terminal.RheostatFixed or Terminal.RheostatWiper or Terminal.WireLeft or Terminal.WireRight
            or Terminal.WireLeftSense or Terminal.WireRightSense;

        internal bool IsOpen(Terminal terminal) => posts[(int)terminal]?.Open == true;
        internal bool NeedsOpening(Terminal terminal) => IsScrewPost(terminal) && !IsOpen(terminal);
        internal bool Moving(Terminal terminal) => posts[(int)terminal] is Post post && post.Lift != (post.Open ? 1 : 0);
        internal void SetOpen(Terminal terminal, bool open) => posts[(int)terminal].Open = open;
        internal Vector3 CapSpot(Terminal terminal) => posts[(int)terminal]?.Cap.position ?? sockets[(int)terminal].position;

        internal Vector3 WireSpot(Terminal terminal, bool sample = false)
        {
            var socket = sockets[(int)terminal];
            return IsScrewPost(terminal) ? WrapPoint(socket, sample, StartAngle(terminal, sample)) : socket.position + socket.up * .024f;
        }

        internal bool AddWrap(Transform parent, Terminal terminal, Material metal, bool sample, bool animate)
        {
            if (!IsScrewPost(terminal)) return false;
            var line = new GameObject(sample ? "Wrapped nichrome end" : "Wrapped bare lead").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = metal;
            line.widthMultiplier = sample ? .0027f : .0035f;
            line.numCapVertices = 4;
            line.positionCount = 20;
            var wrap = new Wrap(line, sockets[(int)terminal], sample, StartAngle(terminal, sample));
            DrawWrap(wrap, animate ? .05f : 1);
            if (animate) wraps.Add(wrap);
            return true;
        }

        internal void Reset()
        {
            wraps.Clear();
            foreach (var post in posts)
            {
                if (post == null) continue;
                post.Open = false;
                post.Lift = 0;
                PlaceCap(post);
            }
        }

        internal void Animate()
        {
            foreach (var post in posts)
            {
                if (post == null) continue;
                float target = post.Open ? 1 : 0;
                if (post.Lift == target) continue;
                post.Lift = Mathf.MoveTowards(post.Lift, target, Time.unscaledDeltaTime / .27f);
                PlaceCap(post);
            }
            for (int i = wraps.Count - 1; i >= 0; i--)
            {
                var wrap = wraps[i];
                float fraction = Mathf.Clamp01((Time.unscaledTime - wrap.Start) / .35f);
                if (wrap.Line != null) DrawWrap(wrap, fraction);
                if (fraction >= 1 || wrap.Line == null) wraps.RemoveAt(i);
            }
        }

        static void PlaceCap(Post post)
        {
            float height = Mathf.Lerp(.017f, .039f, post.Lift);
            post.Cap.localPosition = Vector3.up * height;
            post.Cap.localRotation = Quaternion.Euler(0, -280 * post.Lift, 0);
            post.Stem.localPosition = Vector3.up * (height * .5f);
            post.Stem.localScale = new Vector3(.011f, height * .5f, .011f);
        }

        static float StartAngle(Terminal terminal, bool sample) => sample && terminal == Terminal.WireRight ? Mathf.PI : 0;

        static Vector3 WrapPoint(Transform stem, bool sample, float angle) =>
            stem.TransformPoint(new Vector3(.0085f * Mathf.Cos(angle), sample ? .007f : .013f, .0085f * Mathf.Sin(angle)));

        static void DrawWrap(Wrap wrap, float fraction)
        {
            for (int i = 0; i < wrap.Line.positionCount; i++)
            {
                float angle = wrap.StartAngle + fraction * 2 * Mathf.PI * i / (wrap.Line.positionCount - 1);
                wrap.Line.SetPosition(i, WrapPoint(wrap.Stem, wrap.Sample, angle));
            }
        }

        static Transform Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(part.GetComponent<Collider>());
            return part.transform;
        }
    }
}
