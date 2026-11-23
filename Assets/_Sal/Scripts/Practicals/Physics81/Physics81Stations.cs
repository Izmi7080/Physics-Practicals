using UnityEngine;

namespace VirtualLab.Physics81
{
    public sealed partial class LabSetupSession
    {
        Transform cylinderDock, beakerDock, cupPourSeat, cupProbeRaisedPose, cupProbeWetPose, cupSetupView, cupPlacementView, bathAssemblyView, travellingSinkView;
        Transform bobBathRest, bobCradle, bobLoadingView, cupDepthView;
        Transform bathStand, tripod, gauze, burner, cupStand;
        Transform bathStandSeat, tripodSeat, gauzeSeat, burnerSeat, cupStandSeat;
        Transform bathStandRest, tripodRest, gauzeRest, burnerRest, cupStandRest;
        internal Transform Burner => burner;

        void BuildProcedureFixtures()
        {
            Transform slab = lab.CupRest.parent;
            Transform heater = slab.Find("Heating arrangement");
            bathStand = slab.Find("Bath clamp stand");
            cupStand = slab.Find("Cup clamp stand");
            tripod = heater.Find("Tripod");
            gauze = heater.Find("Gauze");
            burner = heater.Find("Bunsen burner") ?? heater.Find("Spirit lamp (off)");
            bathStandSeat = Seat(bathStand);
            cupStandSeat = Seat(cupStand);
            tripodSeat = Seat(tripod);
            gauzeSeat = Seat(gauze);
            burnerSeat = Seat(burner);
            bathStandRest = Child(slab, "Bath stand rest", new Vector3(.29f, 0, -.24f));
            tripodRest = Child(slab, "Tripod rest", new Vector3(.22f, 0, -.16f));
            gauzeRest = Child(slab, "Gauze rest", new Vector3(.27f, .007f, -.19f));
            burnerRest = Child(slab, "Burner rest", new Vector3(.24f, 0, -.18f));
            cupStandRest = Child(slab, "Cup stand rest", new Vector3(-.20f, 0, -.18f));
            lab.Jug.Body.gameObject.SetActive(false);
            lab.Bob.localScale *= .62f;

            Vector3 outlet = lab.Tap.position;
            cylinderDock = Child(transform, "Cylinder under main tap", Vector3.zero);
            cylinderDock.SetPositionAndRotation(new Vector3(outlet.x, Mathf.Min(lab.JugSink.position.y, outlet.y - .202f), outlet.z), slab.rotation);
            beakerDock = Child(transform, "Beaker under main tap", Vector3.zero);
            beakerDock.SetPositionAndRotation(new Vector3(outlet.x + .0025f, lab.JugSink.position.y, outlet.z), slab.rotation);
            travellingSinkView = Child(transform, "Tap placement view", Vector3.zero);
            cupPourSeat = Child(lab.CupRest, "Cylinder pour position", new Vector3(-.035f, .148f, 0));
            cupProbeRaisedPose = Child(lab.CupProbeSlot, "Thermometer raised", Vector3.up * .14f);
            cupProbeWetPose = Child(lab.CupProbeSlot, "Thermometer measuring position", Vector3.up * .01f);
            cupSetupView = Child(slab, "Open cup setup view", Vector3.zero);
            Frame(cupSetupView, slab, new Vector3(-.009f, .123f, -.105f), .64f, .12f);
            cupPlacementView = Child(slab, "Cup placement view", Vector3.zero);
            Frame(cupPlacementView, slab, new Vector3(-.245f, .068f, -.025f), .83f, .24f);
            bathAssemblyView = Child(slab, "Bath assembly view", Vector3.zero);
            Frame(bathAssemblyView, slab, new Vector3(.455f, .16f, .016f), .77f, .24f);
            Vector3 bath = slab.InverseTransformPoint(lab.BeakerTripod.position);
            bobBathRest = Child(slab, "Cold bob beside bath", new Vector3(bath.x - .22f, lab.BobRest.localPosition.y, bath.z - .12f));
            bobCradle = slab.Find("Apparatus rests/Bob cradle");
            bobLoadingView = Child(slab, "Bob loading view", Vector3.zero);
            Frame(bobLoadingView, slab, bath + new Vector3(-.095f, .018f, -.05f), .56f, .17f);
            Frame(lab.BalanceView, slab, new Vector3(-.53f, .077f, -.058f), .57f, .125f);
            Frame(lab.CupView, slab, new Vector3(.049f, .135f, -.039f), .39f, 0f);
            cupDepthView = Child(slab, "Look into polystyrene cup", Vector3.zero);
            Frame(cupDepthView, slab, new Vector3(.049f, .092f, -.039f), .24f, .34f);
        }

        static Transform Seat(Transform part)
        {
            var seat = Child(part.parent, part.name + " seat", part.localPosition);
            seat.localRotation = part.localRotation;
            return seat;
        }

        Transform SinkView(SetupVessel vessel, float mark, bool placement)
        {
            Vector3 front = Flat(lab.Sink.position - lab.Tap.position).normalized;
            Transform dock = vessel == lab.Cylinder ? cylinderDock : beakerDock;
            Vector3 focus = dock.position + Vector3.up * vessel.Surface(mark);
            if (placement) focus = Vector3.Lerp(focus, lab.Tap.position, .35f);
            Vector3 eye = focus + front * (placement ? .59f : .32f);
            Transform view = placement ? travellingSinkView : lab.SinkView;
            view.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye));
            // Keep graduations facing the student even if the sink faces a different wall.
            dock.rotation = Quaternion.LookRotation(-front);
            return view;
        }

        void ResetStations(bool prepared, bool finished)
        {
            Place(bathStand, prepared ? bathStandSeat : bathStandRest);
            Place(tripod, prepared ? tripodSeat : tripodRest);
            Place(gauze, prepared ? gauzeSeat : gauzeRest);
            Place(burner, prepared ? burnerSeat : burnerRest);
            Place(cupStand, finished ? cupStandSeat : cupStandRest);
        }

        void RefreshApparatus()
        {
            lab.Bob.gameObject.SetActive(Started);
            if (Step is SetupStep.WatchBoil or SetupStep.LowerBob)
            {
                if (lab.Bob.parent == lab.BobRest) Place(lab.Bob, bobBathRest);
                MoveBobCradle(bobBathRest);
            }
            else if (Step < SetupStep.WatchBoil) MoveBobCradle(lab.BobRest);
            lab.Cup.Body.gameObject.SetActive(Step >= SetupStep.EmptyCupOnPan);
            lab.Cylinder.Body.gameObject.SetActive(Step is >= SetupStep.TakeCylinder and <= SetupStep.PourCup);
            lab.Bath.Body.gameObject.SetActive(Step >= SetupStep.TakeBeaker);
            lab.CupProbe.gameObject.SetActive(Step >= SetupStep.FitCupProbe);
            lab.BathProbe.gameObject.SetActive(Step >= SetupStep.FitBathProbe);
            lab.Lid.gameObject.SetActive(Step >= SetupStep.FitLid);
            lab.Stirrer.gameObject.SetActive(false);
            bathStand.gameObject.SetActive(Step >= SetupStep.PlaceBathStand);
            tripod.gameObject.SetActive(Step >= SetupStep.PlaceTripod);
            gauze.gameObject.SetActive(Step >= SetupStep.PlaceGauze);
            burner.gameObject.SetActive(Step >= SetupStep.PlaceLamp);
            cupStand.gameObject.SetActive(Step >= SetupStep.PlaceCupStand);
        }

        void MoveBobCradle(Transform rest)
        {
            if (bobCradle != null) bobCradle.position = rest.position - bobCradle.up * (bobCradle.lossyScale.y * .5f);
        }
    }
}
