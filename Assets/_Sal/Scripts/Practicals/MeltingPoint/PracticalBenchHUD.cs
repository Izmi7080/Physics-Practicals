using UnityEngine;
using VirtualLab.Physics141;

public partial class PracticalUIController
{
    OhmGuideHud benchGuide;
    (int Step, int Done, int Total, string Instruction, string Why) lastGuide;
    bool guideVisible;

    void BuildBenchGuide()
    {
        var dock = Rect("Instruction dock",run,new Vector2(.012f,.59f),new Vector2(BenchLeft-.012f,.865f));
        benchGuide = new OhmGuideHud(run, System.Array.Empty<Bounds>(), session.steps.steps.Count, dock);
    }

    void RefreshBenchGuide()
    {
        bool visible = !PageVisible && !session.IsPaused && session.steps.Current != null;
        if (visible != guideVisible)
        {
            benchGuide.Show(visible);
            guideVisible = visible;
        }
        if (!visible) return;

        var action = session.GuideAction();
        bool guided = session.steps.ShowHints;
        var state = (session.Step + 1, action.Done, action.Total,
            guided ? session.CurrentInstruction : session.AssessmentControlInstruction, guided ? action.Why : "");
        if (state != lastGuide)
        {
            lastGuide = state;
            benchGuide.Present(state.Item1, session.steps.Current.title, state.Item4, state.Item2, state.Item3,
                action.Units, context: state.Item5, showContext: session.At("water"));
        }

        var camera = session.cameraDirector.cam;
        bool moving = session.cameraDirector.IsMoving || session.drag.IsHolding || session.Busy;
        benchGuide.BeginRings();
        if (!session.cameraDirector.IsMoving)
        {
            if (action.Source is Vector3 source) benchGuide.Ring(0, camera, source, 58, draw: guided && !session.Busy);
            if (action.Target is Vector3 target) benchGuide.Ring(1, camera, target, 52, true, guided && !session.Busy);
        }
        benchGuide.EndRings(camera, true, moving);
    }
}

public partial class NaphthalenePracticalSession
{
    internal (Vector3? Source, Vector3? Target, int Done, int Total, string Units, string Why) GuideAction()
    {
        Vector3 Item(string id, float height = 0) => lookup[id].item.transform.position + Vector3.up * height;
        Vector3 AtZone(string name, float height = 0) => destinations[name].transform.position + Vector3.up * height;

        switch (StepId)
        {
            case "setup":
                if (!preparation.tripod) return (Item("tripod", .09f), AtZone("PlaceTripod"), 0, 3, "placed", "The tripod holds the water bath above the heat source.");
                if (!preparation.gauze) return (Item("gauze"), AtZone("PlaceGauze"), 1, 3, "placed", "Wire gauze supports the beaker and spreads the heat across its base.");
                return (Item("beaker", .05f), AtZone("PlaceBeaker", .04f), preparation.beaker ? 3 : 2, 3, "placed", "The beaker holds the water bath that heats the sample indirectly.");
            case "water":
                const string bathWhy = "The marked water level lets the bath cover the sample and thermometer bulb.";
                if (preparation.water) return (null, null, 3, 3, "actions", "");
                if (beakerBackAtBench) return (Item("beaker", .055f), AtZone("PlaceBeaker", .04f), 2, 3, "actions", bathWhy);
                if (!beakerAtSinkStation) return (Item("beaker", .055f), null, 0, 3, "actions", bathWhy);
                if (!waterFill.atSink && !drag.IsHolding || drag.IsHolding && !pickedBeakerFromSink)
                    return (Item("beaker", .055f), AtZone("FillAtSink", .04f), 0, 3, "actions", bathWhy);
                if (CanReturnWater) return (Item("beaker", .055f), null, 2, 3, "actions", "Carry the beaker back with the tap closed before heating the bath.");
                return (WaterControlPoint, null, 1, 3, "actions", "Compare the water surface with the mark at eye level. The tap handle starts and stops the flow.");
            case "seal":
                return (Item("capillary", .026f), AtZone("SealCapillary"), 0, 0, "", "Sealing one end keeps the sample from falling out and keeps bath water from entering that end.");
            case "sample":
                return IsBoiling ? (Item("fusion", .04f), null, 0, 0, "", "The fusion tube holds the liquid sample for the boiling point test.")
                    : (Item("sample"), mortarCentre.position, 0, 0, "", "The mortar holds the solid while it is ground into a finer powder.");
            case "grind":
                return (!preparation.pestle ? Item("pestle") : mortarCentre.position,
                    preparation.pestle ? null : (Vector3?)mortarCentre.position, preparation.pestle ? 1 : 0, 2, "actions",
                    "The pestle crushes the solid into fine grains so the powder can pack evenly in the tube.");
            case "pack":
                return (preparation.sampleAccepted ? (Vector3?)null : lookup["capillary"].item.transform.TransformPoint(Vector3.up*.022f),
                    preparation.sampleAccepted || HasLoosePowder || preparation.sampleHeightMm > SampleSettings.maximumColumnMm
                        ? null : (Vector3?)mortarCentre.position, 0, 0, "",
                    "A short, tightly packed column heats evenly and makes the first liquid easier to see.");
            case "invert":
                return (Item("capillary", .03f), Item("fusion", .04f), 0, 0, "", "The open end sits in the liquid so a stream of bubbles can be observed.");
            case "attach":
                return (preparation.capillaryPlaced ? (preparation.band ? (Vector3?)null : rubberBand.position) : Item(IsBoiling ? "fusion" : "capillary", .03f),
                    preparation.capillaryPlaced ? null : (Vector3?)AtZone(IsBoiling ? "AttachFusion" : "AttachCapillary"),
                    preparation.capillaryPlaced ? 1 : 0, 2, "actions", "The sample sits beside the thermometer bulb so both are at the same depth in the bath.");
            case "immerse":
                return (preparation.immersed ? (Vector3?)null : ThermometerGripPoint, preparation.clamped ? null : (Vector3?)ThermometerClampPoint,
                    preparation.clamped ? 1 : 0, 2, "actions", "The bath must cover the bulb and sample. Keep the thread above water and the bulb away from the hot glass.");
            case "ignite":
            case "shutdown":
                return (HeatControlPosition, null, 0, 0, "", "The heat source warms the water bath, which transfers heat to the sample.");
            case "heat":
                return (!preparation.stirrer ? Item("stirrer", .06f)
                        : !IsBoiling && cameraDirector.CurrentAnchorName=="Thermometer" ? (Vector3?)null : packedSolid.parent.position,
                    preparation.stirrer ? null : (Vector3?)AtZone("PlaceStirrer"),
                    (Simulation.onset.captured ? 1 : 0) + (Simulation.completion.captured ? 1 : 0), IsBoiling ? 1 : 2, "readings",
                    "Stirring distributes heat through the bath. Gentle heating near the change makes your reading easier to catch.");
            default:
                return (null, null, 0, 0, "", "");
        }
    }
}
