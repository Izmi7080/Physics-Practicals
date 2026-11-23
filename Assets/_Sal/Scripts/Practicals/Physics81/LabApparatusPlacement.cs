using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualLab.Physics81
{
    public sealed partial class LabSetupSession
    {
        enum Gesture { None, Placement, Depth, Transfer, Mix }
        Gesture gesture;
        int placementPointer;
        Transform heldApparatus, returnApparatus;
        Vector3 placementHome, returnFrom, localGrip;
        Quaternion placementHomeRotation, returnRotation;
        Vector2 pressPoint, lastPoint, gripOffset;
        Plane placementPlane;
        Bounds heldBounds, litBounds;
        float returnTime, depthOnPress, startGripDepth;
        bool placementMoved, validDrop;
        Transform litApparatus;
        LineRenderer[] sourceOutlines, targetOutlines;
        LineRenderer targetMarker;
        Material placementMaterial;
        readonly Bounds[] standSections = new Bounds[3];
        int standSectionCount;
        readonly List<Vector2> projected = new(32), hull = new(32);

        public bool IsPlacement => PlacementFor(Step, out _, out _) || Step == SetupStep.TransferBob;
        public bool PlacementReady => CanInteract && Data.ActionReady && !Rig.IsTransitioning;
        public bool PlacementBusy => gesture != Gesture.None || returnApparatus != null;

        bool PlacementFor(SetupStep step, out Transform item, out Transform seat)
        {
            (item, seat) = step switch
            {
                SetupStep.BobOnPan => (lab.Bob, lab.BobOnPan),
                SetupStep.ReturnBob => (lab.Bob, lab.BobRest),
                SetupStep.EmptyCupOnPan or SetupStep.FilledCupOnPan => (lab.Cup.Body, lab.CupOnPan),
                SetupStep.RemoveEmptyCup or SetupStep.CupOnSlab => (lab.Cup.Body, lab.CupRest),
                SetupStep.TakeCylinder or SetupStep.CarryCylinder => (lab.Cylinder.Body, lab.JugCarry),
                SetupStep.DockCylinder => (lab.Cylinder.Body, cylinderDock),
                SetupStep.ParkCylinder => (lab.Cylinder.Body, lab.CylinderRest),
                SetupStep.TipCup => (lab.Cylinder.Body, cupPourSeat),
                SetupStep.TakeBeaker or SetupStep.CarryBeaker => (lab.Bath.Body, lab.JugCarry),
                SetupStep.DockBeaker => (lab.Bath.Body, beakerDock),
                SetupStep.BeakerOnTripod => (lab.Bath.Body, lab.BeakerTripod),
                SetupStep.PlaceBathStand => (bathStand, bathStandSeat),
                SetupStep.PlaceTripod => (tripod, tripodSeat),
                SetupStep.PlaceGauze => (gauze, gauzeSeat),
                SetupStep.PlaceLamp => (burner, burnerSeat),
                SetupStep.FitBathProbe => (lab.BathProbe, lab.BathProbeSlot),
                SetupStep.PlaceCupStand => (cupStand, cupStandSeat),
                SetupStep.FitCupProbe => (lab.CupProbe, lab.CupProbeSlot),
                SetupStep.RaiseCupProbe => (lab.CupProbe, cupProbeRaisedPose),
                SetupStep.LowerCupProbe => (lab.CupProbe, cupProbeWetPose),
                SetupStep.FitLid => (lab.Lid, lab.CupLid),
                SetupStep.LowerBob => (lab.Bob, lab.Bath.Body),
                _ => (null, null)
            };
            return item != null && seat != null;
        }

        public bool PlacementTargets(out Transform item, out Vector3 destination)
        {
            bool active = PlacementFor(Step, out item, out var seat);
            destination = active ? TargetPose(item, seat, ApparatusBounds(item)).contact : Vector3.zero;
            return active;
        }

        (Vector3 origin, Vector3 contact, Quaternion rotation, Vector3 scale) TargetPose(Transform item, Transform seat, Bounds bounds)
        {
            Vector3 scale = Vector3.Scale(seat.lossyScale, item.localScale);
            Vector3 offset = seat.rotation * Vector3.Scale(ContactPoint(item, bounds), scale);
            if (Step == SetupStep.LowerBob)
            {
                Vector3 contact = seat.TransformPoint(new Vector3(.018f, .141f, .01f));
                return (contact - offset, contact, seat.rotation, scale);
            }
            Vector3 origin = Step is SetupStep.FitBathProbe or SetupStep.FitCupProbe
                ? seat.TransformPoint(Vector3.up * .055f) : seat.position;
            return (origin, origin + offset, seat.rotation, scale);
        }

        Vector3 ContactPoint(Transform item, Bounds bounds)
        {
            if (item == bathStand || item == cupStand)
            {
                var clamp = item.Find("ClampStandVisual/clamp");
                if (clamp != null) return item.InverseTransformPoint(clamp.position);
            }
            if (item == lab.Cup.Body || item == lab.Bath.Body || item == lab.Cylinder.Body || item == tripod || item == burner)
                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return bounds.center;
        }

        public bool BeginPlacement(Vector2 screen, int pointer)
        {
            if (!CanInteract || Rig.IsTransitioning || PlacementBusy) return false;
            if (TryTapScale(screen)) return true;
            if (CanTransfer && NearScreen(screen, instruments.ThreadGrip, .082f))
            {
                downwardTravel = 0;
                return Hold(Gesture.Transfer, screen, pointer);
            }
            if (CanMix && NearScreen(screen, lab.Cup.Body.TransformPoint(Vector3.up * .06f), .065f))
                return Hold(Gesture.Mix, screen, pointer);
            Transform item;
            if (AdjustingDepth) item = Step == SetupStep.SetCupDepth ? lab.CupProbe : lab.BathProbe;
            else if (!PlacementReady || !PlacementFor(Step, out item, out _)) return false;
            var ray = Rig.ViewCamera.ScreenPointToRay(screen);
            var localRay = new Ray(item.InverseTransformPoint(ray.origin), item.InverseTransformVector(ray.direction));
            heldBounds = ApparatusBounds(item);
            Bounds hitBounds = heldBounds;
            hitBounds.Expand(.032f);
            if (!hitBounds.IntersectRay(localRay, out float distance)) return false;

            heldApparatus = item;
            placementHome = item.localPosition;
            placementHomeRotation = item.localRotation;
            var centre = item.TransformPoint(heldBounds.center);
            localGrip = AdjustingDepth ? heldBounds.center : localRay.GetPoint(distance);
            Vector3 grip = Rig.ViewCamera.WorldToScreenPoint(item.TransformPoint(localGrip));
            gripOffset = (Vector2)grip - screen;
            startGripDepth = grip.z;
            Vector3 viewForward = Rig.ViewCamera.transform.forward;
            placementPlane = new Plane(AdjustingDepth ? Flat(viewForward).normalized : viewForward, centre);
            depthOnPress = depth;
            placementMoved = validDrop = false;
            return Hold(AdjustingDepth ? Gesture.Depth : Gesture.Placement, screen, pointer);
        }

        bool TryTapScale(Vector2 screen)
        {
            if (Step == SetupStep.PowerBalance && NearScreen(screen, Balance.PowerButton.position, .016f))
            {
                PlaySound("Click");
                Primary();
                return true;
            }
            if (!CanTare || !NearScreen(screen, Balance.TareButton.position, .030f)) return false;
            PlaySound("Click");
            TareScale();
            return true;
        }

        bool Hold(Gesture kind, Vector2 screen, int pointer)
        {
            gesture = kind;
            placementPointer = pointer;
            pressPoint = lastPoint = screen;
            Rig.LockMovement(true);
            return true;
        }

        public bool OwnsPlacement(int pointer) => gesture != Gesture.None && placementPointer == pointer;

        public void MovePlacement(Vector2 screen, int pointer)
        {
            if (!OwnsPlacement(pointer)) return;
            Vector2 delta = (screen - lastPoint) / Mathf.Max(1, Screen.height);
            lastPoint = screen;
            if (gesture == Gesture.Transfer) { MoveThread(delta); return; }
            if (gesture == Gesture.Mix)
            {
                if (Mathf.Abs(delta.x) >= .0015f) mixMotion = .26f;
                return;
            }
            if (!placementMoved)
            {
                if ((screen - pressPoint).sqrMagnitude < 36f) return;
                placementMoved = true;
                if (gesture == Gesture.Placement) PlaySound("Pickup");
            }
            var camera = Rig.ViewCamera;
            Vector2 grip = screen + gripOffset;
            if (gesture == Gesture.Depth)
            {
                Ray ray = camera.ScreenPointToRay(grip);
                if (!placementPlane.Raycast(ray, out float distance)) return;
                Vector3 start = heldApparatus.parent.TransformPoint(placementHome) + heldApparatus.TransformVector(heldBounds.center);
                SetDepth((depthOnPress + (ray.GetPoint(distance).y - start.y)) * 1000f);
                return;
            }
            if (!PlacementFor(Step, out var item, out var seat)) return;

            var pose = TargetPose(item, seat, heldBounds);
            Vector3 contact = camera.WorldToScreenPoint(pose.contact);
            Vector3 seatGrip = camera.WorldToScreenPoint(pose.origin + pose.rotation * Vector3.Scale(localGrip, pose.scale));
            float radius = DropRadius(contact, pose.contact, item);
            float gap = Mathf.Min(Vector2.Distance(screen, contact), Vector2.Distance(grip, seatGrip));
            validDrop = contact.z > camera.nearClipPlane && gap <= radius;
            if (validDrop)
            {
                item.SetPositionAndRotation(pose.origin, pose.rotation);
                return;
            }

            float startGap = Mathf.Min(Vector2.Distance(pressPoint, contact), Vector2.Distance(pressPoint + gripOffset, seatGrip));
            float approach = contact.z > camera.nearClipPlane && seatGrip.z > camera.nearClipPlane
                ? Mathf.SmoothStep(0, 1, (startGap - gap) / Mathf.Max(startGap - radius, radius)) : 0f;
            float depth = Mathf.Lerp(startGripDepth, seatGrip.z, approach);
            Quaternion rotation = Quaternion.Slerp(item.parent.rotation * placementHomeRotation, pose.rotation, approach);
            Vector3 worldGrip = camera.ScreenToWorldPoint(new Vector3(grip.x, grip.y, depth));
            item.SetPositionAndRotation(worldGrip - rotation * Vector3.Scale(localGrip, item.lossyScale), rotation);
        }

        void MoveThread(Vector2 delta)
        {
            float advance = transferTarget < .26f ? Mathf.Max(0, delta.y) * 3f :
                transferTarget < .74f ? Mathf.Max(0, -delta.x) * 2.4f : Mathf.Max(0, -delta.y) * 3f;
            transferTarget = Mathf.Clamp(transferTarget + advance, Data.TransferProgress, 1f);
            if (transferTarget < Physics81InstrumentRig.CupContactProgress || delta.y >= 0 || Data.Thermal.SpilledKg > 0) return;
            if (Time.unscaledTime - downwardTime > .18f) downwardTravel = 0;
            downwardTime = Time.unscaledTime;
            downwardTravel -= delta.y;
            if (downwardTravel > .13f && Data.Thermal.Spill(.0015) > 0) instruments.Splash();
        }

        public void EndPlacement(Vector2 screen, int pointer)
        {
            if (!OwnsPlacement(pointer)) return;
            MovePlacement(screen, pointer);
            var kind = gesture;
            var item = heldApparatus;
            gesture = Gesture.None;
            heldApparatus = null;
            Rig.LockMovement(false);
            if (kind != Gesture.Placement) return;
            bool pickingUp = Step is SetupStep.TakeCylinder or SetupStep.CarryCylinder or SetupStep.TakeBeaker or SetupStep.CarryBeaker;
            if (!placementMoved && pickingUp)
            {
                PlaySound("Pickup");
                AdvanceAction();
                return;
            }
            if (placementMoved && validDrop)
            {
                if (Step != SetupStep.LowerBob) item.SetLocalPositionAndRotation(placementHome, placementHomeRotation);
                if (!pickingUp) PlaySound("Snap");
                AdvanceAction();
                return;
            }
            if (!placementMoved) return;
            returnApparatus = item;
            returnFrom = item.localPosition;
            returnRotation = item.localRotation;
            returnTime = 0f;
            Rig.LockMovement(true);
        }

        float DropRadius(Vector3 contact, Vector3 world, Transform item)
        {
            var camera = Rig.ViewCamera;
            float metres = item == lab.CupProbe || item == lab.BathProbe ? .033f : .047f;
            float pixels = Vector2.Distance(contact, camera.WorldToScreenPoint(world + camera.transform.right * metres));
            return Mathf.Clamp(pixels, Screen.height * .036f, Screen.height * .075f);
        }

        bool NearScreen(Vector2 position, Vector3 point, float radius)
        {
            var camera = Rig.ViewCamera;
            var target = camera.WorldToScreenPoint(point);
            float pixels = Vector2.Distance(target, camera.WorldToScreenPoint(point + camera.transform.right * radius));
            return target.z > camera.nearClipPlane && Vector2.Distance(position, target) <= Mathf.Max(Screen.height * .029f, pixels);
        }

        public void CancelPlacement()
        {
            if (!PlacementBusy) return;
            if (heldApparatus != null && gesture == Gesture.Placement) heldApparatus.SetLocalPositionAndRotation(placementHome, placementHomeRotation);
            if (returnApparatus != null) returnApparatus.SetLocalPositionAndRotation(placementHome, placementHomeRotation);
            heldApparatus = returnApparatus = null;
            gesture = Gesture.None;
            validDrop = false;
            Rig.LockMovement(false);
        }

        void UpdatePlacement(float dt)
        {
            if (returnApparatus != null)
            {
                returnTime = Mathf.MoveTowards(returnTime, 1f, dt / .24f);
                float progress = Mathf.SmoothStep(0, 1, returnTime);
                returnApparatus.SetLocalPositionAndRotation(Vector3.Lerp(returnFrom, placementHome, progress),
                    Quaternion.Slerp(returnRotation, placementHomeRotation, progress));
                if (returnTime >= 1f) CancelPlacement();
            }

            Transform source = null, target = null;
            if (CanInteract && !Rig.IsTransitioning) PlacementFor(Step, out source, out target);
            if (source == null && targetMarker == null) return;
            if (targetMarker == null)
            {
                placementMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Physics 8.1 placement guides" };
                sourceOutlines = new LineRenderer[standSections.Length];
                targetOutlines = new LineRenderer[standSections.Length];
                for (int i = 0; i < standSections.Length; i++)
                {
                    sourceOutlines[i] = MakeOutline("Placement source outline", new Color(.44f, 1f, .82f, .84f));
                    targetOutlines[i] = MakeOutline("Placement destination outline", new Color(.14f, .92f, .51f, .77f));
                }
                targetMarker = MakeOutline("Placement drop marker", Color.white);
            }
            targetMarker.enabled = source != null;
            for (int i = 0; i < standSections.Length; i++)
                sourceOutlines[i].enabled = targetOutlines[i].enabled = false;
            if (source == null) { litApparatus = null; return; }
            if (litApparatus != source)
            {
                litApparatus = source;
                litBounds = ApparatusBounds(source);
                standSectionCount = 0;
                if (source == bathStand || source == cupStand)
                {
                    var model = source.Find("ClampStandVisual");
                    if (model != null)
                        foreach (Transform section in model)
                            if (section.name is "clamp" or "stand" or "ThermometerHoldingClamp")
                                standSections[standSectionCount++] = BranchBounds(source, section);
                }
            }
            var pose = TargetPose(source, target, litBounds);
            bool preview = gesture == Gesture.Placement && validDrop;
            for (int i = 0; i < Mathf.Max(1, standSectionCount); i++)
            {
                Bounds bounds = standSectionCount == 0 ? litBounds : standSections[i];
                if (!preview)
                    DrawOutline(sourceOutlines[i], source, bounds, source.position, source.rotation, source.lossyScale);
                DrawOutline(targetOutlines[i], source, bounds, pose.origin, pose.rotation, pose.scale);
            }
            DrawDropMarker(source, pose.contact, preview);
        }

        LineRenderer MakeOutline(string label, Color colour)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = placementMaterial;
            line.loop = true;
            line.widthMultiplier = .0022f;
            line.startColor = line.endColor = colour;
            line.shadowCastingMode = ShadowCastingMode.Off;
            return line;
        }

        void DrawDropMarker(Transform item, Vector3 world, bool preview)
        {
            var camera = Rig.ViewCamera;
            Vector3 contact = camera.WorldToScreenPoint(world);
            if (contact.z <= camera.nearClipPlane) { targetMarker.enabled = false; return; }
            float radius = DropRadius(contact, world, item);
            targetMarker.startColor = targetMarker.endColor = preview
                ? new Color(.51f, 1f, .69f) : new Color(1f, .88f, .50f, .93f);
            targetMarker.positionCount = 40;
            float depth = Mathf.Max(camera.nearClipPlane + .01f, contact.z * .75f);
            for (int i = 0; i < targetMarker.positionCount; i++)
            {
                float angle = 2 * Mathf.PI * i / targetMarker.positionCount;
                var point = new Vector3(contact.x + Mathf.Cos(angle) * radius, contact.y + Mathf.Sin(angle) * radius, depth);
                targetMarker.SetPosition(i, camera.ScreenToWorldPoint(point));
            }
        }

        void DrawOutline(LineRenderer line, Transform source, Bounds bounds, Vector3 origin, Quaternion rotation, Vector3 scale)
        {
            var camera = Rig.ViewCamera;
            Vector3 centre = origin + rotation * Vector3.Scale(bounds.center, scale);
            float distance = camera.WorldToScreenPoint(centre).z;
            if (distance <= camera.nearClipPlane) return;
            projected.Clear();
            Vector3 e = Vector3.Scale(bounds.extents, scale) + Vector3.one * .003f;
            bool round = source == lab.Cup.Body || source == lab.Bath.Body || source == lab.Cylinder.Body || source == lab.Lid || source == lab.Bob;
            int corners = round ? 12 : 4;
            for (int level = -1; level <= 1; level += 2)
            for (int i = 0; i < corners; i++)
            {
                float angle = (i + (round ? 0 : .5f)) * Mathf.PI * 2f / corners;
                float taper = source == lab.Cup.Body && level < 0 ? .65f : round ? 1f : 1.4142f;
                projected.Add(camera.WorldToScreenPoint(centre + rotation * new Vector3(
                    Mathf.Cos(angle) * e.x * taper, e.y * level, Mathf.Sin(angle) * e.z * taper)));
            }
            projected.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            hull.Clear();
            foreach (var point in projected) AddPoint(point, 2);
            int lower = hull.Count + 1;
            for (int i = projected.Count - 2; i >= 0; i--) AddPoint(projected[i], lower);
            line.enabled = true;
            line.positionCount = hull.Count - 1;
            float front = Mathf.Max(camera.nearClipPlane + .01f, distance - e.magnitude - .009f);
            for (int i = 0; i < line.positionCount; i++)
                line.SetPosition(i, camera.ScreenToWorldPoint(new Vector3(hull[i].x, hull[i].y, front)));

            void AddPoint(Vector2 point, int floor)
            {
                while (hull.Count >= floor)
                {
                    Vector2 edge = hull[^1] - hull[^2], next = point - hull[^1];
                    if (edge.x * next.y - edge.y * next.x > 0) break;
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(point);
            }
        }

        internal static Bounds ApparatusBounds(Transform item) => BranchBounds(item, item);

        static Bounds BranchBounds(Transform item, Transform branch)
        {
            Bounds result = new(Vector3.zero, Vector3.zero);
            bool found = false;
            foreach (var part in branch.GetComponentsInChildren<MeshFilter>())
            {
                if (part.name == "Measured water" || part.sharedMesh == null || !part.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
                Bounds mesh = part.sharedMesh.bounds;
                Matrix4x4 local = item.worldToLocalMatrix * part.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = local.MultiplyPoint3x4(corner);
                    if (found) result.Encapsulate(point);
                    else { result.center = point; found = true; }
                }
            }
            return result;
        }

        void OnDestroy() { if (placementMaterial != null) Destroy(placementMaterial); }
    }
}
