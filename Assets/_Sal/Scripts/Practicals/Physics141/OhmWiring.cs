using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


namespace VirtualLab.Physics141
{
    public sealed class OhmWiring : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler,
        IInitializePotentialDragHandler, IPointerExitHandler
    {
        public static readonly Lead[] Reference =
        {
            new(Terminal.SupplyPlus, Terminal.KeyIn), new(Terminal.KeyOut, Terminal.RheostatFixed),
            new(Terminal.RheostatWiper, Terminal.CurrentAmps), new(Terminal.CurrentCom, Terminal.WireLeft),
            new(Terminal.WireRight, Terminal.SupplyMinus), new(Terminal.WireLeftSense, Terminal.VoltageVolts),
            new(Terminal.WireRightSense, Terminal.VoltageCom)
        };

        readonly List<Lead> leads = new();
        readonly List<LineRenderer> cables = new();
        OhmBindingPosts posts;
        Lead? pendingLead;
        LineRenderer pendingCable, sampleWire;
        Material sampleMetal;
        Vector3[] looseTips;
        int securedSampleEnds;
        bool sampleWrapped, sampleGesture, grabbingTip;
        public IReadOnlyList<Lead> Leads => leads;
        public event Action Changed;
        public bool Guided { get; set; } = true;
        public bool Editable { get; set; } = true;
        public MeterRange CurrentRange { get; private set; }
        public MeterRange VoltageRange { get; private set; }
        public bool Busy => pointer.HasValue;
        public bool Prepared { get; private set; }
        public bool SampleMounted => securedSampleEnds == 2;
        internal bool MountingSample { get; set; }
        internal int SecuredSampleEnds => securedSampleEnds;
        internal Terminal SamplePost => securedSampleEnds == 0 ? Terminal.WireLeft : Terminal.WireRight;
        internal bool SampleWrapped => sampleWrapped;
        public Vector3 LooseSampleTip => looseTips[securedSampleEnds == 0 ? 0 : 1];
        internal Terminal? PendingTighten => pendingLead is Lead lead ? OpenPost(lead) : null;
        internal bool HasDraft => pendingLead.HasValue;
        internal Terminal? NeedsOpening(Lead lead) => posts.NeedsOpening(lead.from) ? lead.from
            : posts.NeedsOpening(lead.to) ? lead.to : null;
        internal Vector3 ContactSpot(Terminal terminal) => posts.CapSpot(terminal);
        internal bool IsOpen(Terminal terminal) => posts.IsOpen(terminal);
        OhmBench bench;
        AudioClip snap, click, grind;
        AudioSource audioSource;
        internal string Feedback { get; private set; }
        internal bool AcceptLinks { get; set; }
        internal Lead? Expected { get; set; }
        internal bool HasSelection => selected >= 0;
        internal Terminal? PendingSource => source.HasValue ? (Terminal)source.Value : null;
        internal Func<Vector2, bool> BeginDirect;
        internal Action<Vector2> DragDirect, EndDirect;
        internal Action CancelDirect;
        bool directGesture;
        int? source, pointer, dragStart;
        int selected = -1;
        bool dragged, movingWiper;
        internal Action<float> WiperMoved;
        internal Action WiperReleased;
        internal Func<bool> CanMoveWiper;
        Vector2 down;
        LineRenderer preview;


        public void Configure(OhmBench owner, AudioSource audio)
        {
            bench = owner;
            posts = new OhmBindingPosts(bench);
            var sample = bench.Overview.parent.Find("Nichrome board/Nichrome sample");
            var model = sample.GetComponent<Renderer>();
            model.enabled = false;
            sampleWire = new GameObject("Nichrome sample wire").AddComponent<LineRenderer>();
            sampleWire.transform.SetParent(sample, false);
            sampleMetal = new Material(model.sharedMaterial) { color = new Color(.77f, .81f, .83f) };
            sampleWire.sharedMaterial = sampleMetal;
            sampleWire.widthMultiplier = .0034f;
            sampleWire.numCapVertices = 4;
            sampleWire.positionCount = 8;
            var left = bench.Sockets[(int)Terminal.WireLeft];
            var right = bench.Sockets[(int)Terminal.WireRight];
            looseTips = new[] { left.TransformPoint(new Vector3(.035f, .009f, .022f)),
                right.TransformPoint(new Vector3(-.035f, .009f, .022f)) };
            DrawSample();

            audioSource = audio;
            snap = Resources.Load<AudioClip>("Physics81Audio/Snap");
            click = Resources.Load<AudioClip>("Practicals/ChemistryAudio/Click");
            grind = Resources.Load<AudioClip>("Practicals/ChemistryAudio/Grind");
            Refresh();
        }

        public void SetPreparation(bool value)
        {
            Prepared = value;
            if (value) Cue(grind);
            Refresh();
            Changed?.Invoke();
        }

        public void Restore(List<Lead> saved, MeterRange current, MeterRange voltage, bool prepared, bool sampleMounted)
        {
            Release();
            DiscardDraft();
            foreach (var cable in cables) Destroy(cable.gameObject);
            foreach (Transform wrap in sampleWire.transform) Destroy(wrap.gameObject);
            posts.Reset();
            securedSampleEnds = sampleMounted ? 2 : 0;
            sampleWrapped = false;
            if (SampleMounted)
            {
                WrapSampleEnd(Terminal.WireLeft, false);
                WrapSampleEnd(Terminal.WireRight, false);
            }
            DrawSample();
            cables.Clear();
            leads.Clear();
            foreach (var lead in saved)
            {
                leads.Add(lead);
                cables.Add(Seat(lead));
            }
            CurrentRange = current;
            VoltageRange = voltage;
            Prepared = prepared;
            selected = -1;
            source = null;
            Feedback = "";
            Refresh();
        }

        void Update()
        {
            if (posts == null) return;
            posts.Animate();
            if (pendingLead is Lead lead && !OpenPost(lead).HasValue && !posts.Moving(lead.from) && !posts.Moving(lead.to))
            {
                var cable = pendingCable;
                pendingLead = null;
                pendingCable = null;
                Commit(lead, cable);
            }
            if (sampleWrapped && !posts.IsOpen(SamplePost) && !posts.Moving(SamplePost))
            {
                sampleWrapped = false;
                securedSampleEnds++;
                Feedback = "";
                DrawSample();
                Changed?.Invoke();
            }
        }

        public void Refresh()
        {
            bench.CurrentSelector.localRotation = Quaternion.Euler(0, 0, Angle(CurrentRange));
            bench.VoltageSelector.localRotation = Quaternion.Euler(0, 0, Angle(VoltageRange));
            for (int i = 0; i < leads.Count; i++)
            {
                cables[i].widthMultiplier = i == selected ? .009f : .0045f;
            }
        }

        public static float Angle(MeterRange range) => range switch
        {
            MeterRange.Dc20V => -48, MeterRange.Dc10A => -105, MeterRange.Dc200mA => -170,
            MeterRange.Ac200V => 55, _ => 0
        };

        public void StepRange(bool current, int direction)
        {
            if (!Editable) return;
            int count = Enum.GetValues(typeof(MeterRange)).Length;
            if (current) CurrentRange = (MeterRange)(((int)CurrentRange + direction + count) % count);
            else VoltageRange = (MeterRange)(((int)VoltageRange + direction + count) % count);
            Cue(click);
            Refresh();

            Changed?.Invoke();
        }

        internal static string RangeName(MeterRange range) => range switch
        {
            MeterRange.Dc20V => "20 V DC", MeterRange.Dc10A => "10 A DC",
            MeterRange.Dc200mA => "200 mA DC", MeterRange.Ac200V => "200 V AC", _ => "Off"
        };

        internal void SetRange(bool current, MeterRange range) => StepRange(current, (int)range - (int)(current ? CurrentRange : VoltageRange));

        public void Undo() => RemoveAt(leads.Count - 1);
        public void RemoveSelected() => RemoveAt(selected);

        void RemoveAt(int index)
        {
            if (!Editable || index < 0 || index >= leads.Count) return;
            Destroy(cables[index].gameObject);
            cables.RemoveAt(index);
            leads.RemoveAt(index);
            selected = -1;
            source = null;
            Feedback = "";
            Refresh();
            Changed?.Invoke();
        }

        void Connect(int from, int to)
        {
            if (Expected is Lead route && !route.Joins((Terminal)from, (Terminal)to))
            {
                Feedback = "Try again. Connect the two circled sockets.";
                return;
            }
            if (from == to || leads.Count >= Reference.Length || Occupied(from) || Occupied(to))
            {
                Feedback = "Choose two free sockets.";
                return;
            }
            var lead = new Lead((Terminal)from, (Terminal)to);
            if (NeedsOpening(lead).HasValue || posts.Moving(lead.from) || posts.Moving(lead.to))
            {
                Feedback = "Loosen the screw caps before bringing the lead to the posts.";
                return;
            }
            Feedback = "";
            var cable = Seat(lead, true);
            if (OpenPost(lead).HasValue)
            {
                pendingLead = lead;
                pendingCable = cable;
                Changed?.Invoke();
            }
            else Commit(lead, cable);
        }

        Terminal? OpenPost(Lead lead) => posts.IsOpen(lead.from) ? lead.from : posts.IsOpen(lead.to) ? lead.to : null;

        void Commit(Lead lead, LineRenderer cable)
        {
            leads.Add(lead);
            cables.Add(cable);
            selected = -1;
            Cue(snap);
            Refresh();
            Changed?.Invoke();
        }

        void DiscardDraft()
        {
            if (pendingCable != null) Destroy(pendingCable.gameObject);
            pendingCable = null;
            pendingLead = null;
        }

        bool Occupied(int socket) => leads.Exists(lead => lead.Uses((Terminal)socket));

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer.HasValue || bench.Rig.IsTransitioning || !bench.Rig.CanZoom) return;
            var camera = bench.Rig.ViewCamera;
            if (MountingSample && Editable && !SampleMounted)
            {
                BeginSample(e);
                return;
            }
            if (CanMoveWiper?.Invoke() == true && bench.Rig.IsViewing(bench.RheostatView)
                && Vector2.Distance(e.position, camera.WorldToScreenPoint(bench.Wiper.position)) < Screen.height * .09f)
            {
                pointer = e.pointerId;
                movingWiper = true;
                bench.Rig.LockMovement(true);
                return;
            }
            if (BeginDirect?.Invoke(e.position) == true)
            {
                pointer = e.pointerId;
                directGesture = true;
                bench.Rig.LockMovement(true);
                return;
            }
            if (!Editable || !AcceptLinks) return;
            Feedback = "";
            int? hit = Hit(e.position);
            if (!hit.HasValue) { source = null; selected = -1; Refresh(); return; }
            pointer = e.pointerId;
            dragStart = hit;
            down = e.position;
            dragged = false;
            bench.Rig.LockMovement(true);
        }

        public void OnDrag(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            if (sampleGesture) { DragSample(e.position); return; }
            if (directGesture) { DragDirect?.Invoke(e.position); return; }
            if (movingWiper)
            {
                var wiper = bench.Wiper;
                var camera = bench.Rig.ViewCamera;
                var start = camera.WorldToScreenPoint(wiper.parent.TransformPoint(new Vector3(-.107f, wiper.localPosition.y, wiper.localPosition.z)));
                var end = camera.WorldToScreenPoint(wiper.parent.TransformPoint(new Vector3(.107f, wiper.localPosition.y, wiper.localPosition.z)));
                Vector2 rail = end - start;
                float fraction = Mathf.Clamp01(Vector2.Dot(e.position - (Vector2)start, rail) / rail.sqrMagnitude);
                WiperMoved?.Invoke(fraction);
                return;
            }
            if (!dragStart.HasValue || Occupied(dragStart.Value)) return;
            if (!dragged && Vector2.Distance(e.position, down) < Screen.height * .012f) return;
            dragged = true;
            if (HasDraft) return;
            var point = posts.WireSpot((Terminal)dragStart.Value);
            var ray = bench.Rig.ViewCamera.ScreenPointToRay(e.position);
            var plane = new Plane(Vector3.up, point);
            if (!plane.Raycast(ray, out float distance)) return;
            if (preview == null) preview = BuildCable((Terminal)dragStart.Value);
            Draw(preview, point, ray.GetPoint(distance) + Vector3.up * .028f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            if (sampleGesture) { EndSample(e.position); return; }
            if (directGesture)
            {
                directGesture = false;
                Release();
                EndDirect?.Invoke(e.position);
                return;
            }
            if (movingWiper)
            {
                Release();
                WiperReleased?.Invoke();
                return;
            }
            var hit = Hit(e.position, droppingWire: dragged && !HasDraft);
            int? from = dragStart;
            bool wasDrag = dragged;
            Release();
            if (!Editable || !hit.HasValue || !from.HasValue) return;
            var terminal = (Terminal)hit.Value;
            if (pendingLead is Lead draft)
            {
                if (!wasDrag && draft.Uses(terminal) && posts.IsOpen(terminal))
                {
                    posts.SetOpen(terminal, false);
                    Cue(click);
                    Changed?.Invoke();
                }
                return;
            }
            if (wasDrag)
            {
                source = null;
                selected = -1;
                Refresh();
                if (from != hit) Connect(from.Value, hit.Value);
                return;
            }
            if (Occupied(hit.Value))
            {
                selected = leads.FindIndex(lead => lead.Uses((Terminal)hit.Value));
                source = null;
                Refresh();
                return;
            }
            selected = -1;
            Refresh();
            if (OhmBindingPosts.IsScrewPost(terminal))
            {
                if (Expected is Lead expected && !expected.Uses(terminal))
                {
                    Feedback = "Start with one of the highlighted connections.";
                    return;
                }
                if (source is int socket && posts.IsOpen(terminal))
                {
                    source = null;
                    Connect(socket, hit.Value);
                    return;
                }
                source = null;
                posts.SetOpen(terminal, !posts.IsOpen(terminal));
                Cue(click);
                Changed?.Invoke();
                return;
            }
            if (source.HasValue && source != hit)
            {
                int previous = source.Value;
                source = null;
                Connect(previous, hit.Value);
                return;
            }
            if (Expected is Lead route && !route.Uses((Terminal)hit.Value))
            {
                Feedback = "Start with one of the two circled sockets.";
                return;
            }
            source = source == hit ? null : hit;
        }

        public void OnPointerExit(PointerEventData e) { if (pointer == e.pointerId && !dragged && !movingWiper && !directGesture && !sampleGesture) Release(); }
        void OnEnable() { if (bench != null) Refresh(); }
        void OnDisable() { if (bench != null) { Release(); Refresh(); } }
        void OnDestroy() { if (sampleMetal != null) Destroy(sampleMetal); }
        void OnApplicationFocus(bool focused) { if (!focused && bench != null) Release(); }

        void Release()
        {
            if (sampleGesture) DrawSample();
            sampleGesture = grabbingTip = false;
            if (directGesture) CancelDirect?.Invoke();
            directGesture = false;
            if (pointer.HasValue) bench.Rig.LockMovement(false);
            pointer = dragStart = null;
            movingWiper = false;
            if (preview != null) Destroy(preview.gameObject);
            preview = null;
        }

        void BeginSample(PointerEventData e)
        {
            if (posts.Moving(SamplePost)) return;
            Vector2 cap = bench.Rig.ViewCamera.WorldToScreenPoint(posts.CapSpot(SamplePost));
            Vector2 tip = bench.Rig.ViewCamera.WorldToScreenPoint(LooseSampleTip);
            float distanceToCap = Vector2.Distance(cap, e.position);
            float distanceToTip = Vector2.Distance(tip, e.position);
            grabbingTip = !sampleWrapped && distanceToTip < distanceToCap;
            if ((grabbingTip ? distanceToTip : distanceToCap) > Screen.height * .05f) return;
            Feedback = "";
            down = e.position;
            dragged = false;
            pointer = e.pointerId;
            sampleGesture = true;
            bench.Rig.LockMovement(true);
        }

        void DragSample(Vector2 screen)
        {
            dragged |= Vector2.Distance(screen, down) > Screen.height * .012f;
            if (!dragged || !grabbingTip || !posts.IsOpen(SamplePost)) return;
            var ray = bench.Rig.ViewCamera.ScreenPointToRay(screen);
            if (new Plane(Vector3.up, LooseSampleTip).Raycast(ray, out float distance))
                DrawSample(ray.GetPoint(distance));
        }

        void EndSample(Vector2 screen)
        {
            bool tip = grabbingTip, moved = dragged;
            var post = SamplePost;
            var camera = bench.Rig.ViewCamera;
            float distanceToCap = Vector2.Distance(screen, camera.WorldToScreenPoint(posts.CapSpot(post)));
            float distanceToStem = Vector2.Distance(screen, camera.WorldToScreenPoint(posts.WireSpot(post, true)));
            Release();
            if (tip && moved)
            {
                if (!posts.IsOpen(post)) Feedback = "Tap the screw cap to expose the metal first.";
                else if (Mathf.Min(distanceToCap, distanceToStem) >= Screen.height * .057f)
                    Feedback = "Bring the wire end to the exposed post.";
                else
                {
                    sampleWrapped = true;
                    WrapSampleEnd(post, true);
                    DrawSample();
                    Cue(snap);
                }
            }
            else if (!tip && !moved && distanceToCap < Screen.height * .05f)
            {
                posts.SetOpen(post, !posts.IsOpen(post));
                Cue(click);
            }
            Changed?.Invoke();
        }

        void WrapSampleEnd(Terminal terminal, bool animate) =>
            posts.AddWrap(sampleWire.transform, terminal, sampleWire.sharedMaterial, true, animate);

        void DrawSample(Vector3? heldTip = null)
        {
            bool leftHeld = securedSampleEnds == 0;
            var left = (leftHeld ? heldTip : null) ?? (securedSampleEnds > 0 || leftHeld && sampleWrapped
                ? posts.WireSpot(Terminal.WireLeft, true) : looseTips[0]);
            var right = (!leftHeld ? heldTip : null) ?? (SampleMounted || !leftHeld && sampleWrapped
                ? posts.WireSpot(Terminal.WireRight, true) : looseTips[1]);
            Draw(sampleWire, left, right, SampleMounted ? 0 : .004f);
        }

        int? Hit(Vector2 screen, bool droppingWire = false)
        {
            var camera = bench.Rig.ViewCamera;
            int? best = null;
            float max = camera.pixelHeight * (droppingWire ? .044f : .029f);
            for (int i = 0; i < bench.Sockets.Length; i++)
            {
                var point = camera.WorldToScreenPoint(bench.Sockets[i].position);
                if (point.z <= 0) continue;
                float distance = Mathf.Min(Vector2.Distance(screen, point),
                    Vector2.Distance(screen, camera.WorldToScreenPoint(posts.CapSpot((Terminal)i))));
                if (distance >= max) continue;
                best = i;
                max = distance;
            }
            return best;
        }

        LineRenderer Seat(Lead lead, bool animate = false)
        {
            var cable = BuildCable(lead.from, lead.to);
            Draw(cable, posts.WireSpot(lead.from), posts.WireSpot(lead.to));
            SeatConnector(cable.transform, lead.from, cable.sharedMaterial, animate);
            SeatConnector(cable.transform, lead.to, cable.sharedMaterial, animate);
            return cable;
        }

        void SeatConnector(Transform parent, Terminal end, Material gripMaterial, bool animate)
        {
            if (posts.AddWrap(parent, end, bench.Brass, false, animate)) return;
            var terminal = bench.Sockets[(int)end];
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Seated plug";
            body.transform.SetParent(parent);
            body.transform.SetPositionAndRotation(terminal.position + terminal.up * .019f, terminal.rotation);
            body.transform.localScale = new Vector3(.013f, .014f, .013f);
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = gripMaterial;
        }

        LineRenderer BuildCable(Terminal from, Terminal? to = null)
        {
            var cable = new GameObject("Connecting lead").AddComponent<LineRenderer>();
            cable.transform.SetParent(bench.transform, false);
            cable.sharedMaterial = IsRed(from) || (to.HasValue && IsRed(to.Value)) ? bench.Red : bench.Rubber;
            cable.widthMultiplier = .0045f;
            cable.numCapVertices = 4;
            cable.positionCount = 16;
            return cable;
        }

        static bool IsRed(Terminal terminal) =>
            terminal is Terminal.SupplyPlus or Terminal.CurrentAmps or Terminal.VoltageVolts or Terminal.WireLeftSense;

        static void Draw(LineRenderer cable, Vector3 first, Vector3 last, float lift = .025f)
        {
            for (int i = 0; i < cable.positionCount; i++)
            {
                float t = i / (float)(cable.positionCount - 1);
                var point = Vector3.Lerp(first, last, t);
                point += Vector3.up * (lift * Mathf.Sin(Mathf.PI * t));
                cable.SetPosition(i, point);
            }
        }

        void Cue(AudioClip clip) { if (clip) audioSource.PlayOneShot(clip, .45f); }
    }
}
