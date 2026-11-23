using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace VirtualLab.Physics141
{
    internal sealed class OhmWalkthrough
    {
        enum Task { Identify, Diagram, Clean, Mount, Meters, Wire, Zero, Center, Power, Voltage, CloseKey, Observe, Fix, OpenKey, PowerOff, Workbook }
        static readonly (string Name, string Description, string Use)[] Equipment =
        {
            ("DC power supply", "Provides a voltage between its red + and black − sockets.",
                "Turn its knob to 6.0 V. Keep the supply at that setting for all five readings."),
            ("Ammeter", "Measures how much electric charge flows each second. Its unit is the ampere (A).",
                "Put the left meter in the main loop so the same current flows through it and the sample wire."),
            ("Voltmeter", "Measures the difference in voltage between two points, in volts (V).",
                "Connect the right meter across the two ends of the nichrome wire to measure its voltage drop."),
            ("Plug key", "A switch that opens or closes the circuit.",
                "Keep it open while setting up. Close it to make a complete loop so current can flow."),
            ("Rheostat", "An adjustable resistor. Its slider changes how much of the coil is in the circuit.",
                "Move the handle to change the current through the sample while the supply stays at 6.0 V."),
            ("Nichrome wire", "An alloy (a mix of metals) made mainly of nickel and chromium.",
                "You’ll use voltage and current readings to find this wire’s resistance."),
            ("Lead and sandpaper", "Leads are insulated wires with metal ends. Sandpaper has a rough surface.",
                "Rub the metal tip on the sandpaper to remove buildup so the lead makes good electrical contact.")
        };
        const int AllApparatus = (1 << 7) - 1;
        readonly OhmBench bench;
        readonly OhmWiring wiring;
        readonly OhmMeasurements measurements;
        readonly OhmCircuitPage diagram;
        readonly OhmGuideHud hud;
        readonly Transform probe, probeTip, paper, sample;
        readonly Vector3 probeHome, tipHome;
        OhmProgress progress;
        Task task;
        int identified, zeros, pendingTarget = -1;
        int lastIdentified = -1;
        bool cleaning, cleaningAnimation, dirty = true, ready, directMoved, introductionDone, checkingWires;
        string wiringFeedback = "";
        Terminal? pendingSource;
        bool hasSelectedLead;
        Vector2 dragOrigin;
        float suggested;
        float? supplyPointerAngle;
        float supplyTurningValue;
        Coroutine cleaningRoutine;
        bool? turningCurrent;
        MeterRange dialRange;
        static readonly MeterRange[] DialRanges = (MeterRange[])Enum.GetValues(typeof(MeterRange));
        static readonly string[] ConnectionNotes =
        {
            "Red + is the higher-voltage end of the supply. This first lead takes it to the switch.",
            "The key is a switch. This lead joins its output to the adjustable resistor.",
            "The rheostat limits current. Current enters the left meter through its red 10A input.",
            "Current leaves the left meter through the black COM jack and enters the sample wire.",
            "Black − is the lower-voltage return. This lead completes the path back to the supply.",
            "VΩ is the red voltage input. It connects to the higher-voltage end of the sample.",
            "COM is the black reference input. It connects to the lower-voltage end so the meter reads across the wire."
        };

        internal OhmWalkthrough(OhmBench bench, OhmWiring wiring, OhmMeasurements measurements, OhmCircuitPage diagram,
            OhmGuideHud hud)
        {
            this.bench = bench;
            this.wiring = wiring;
            this.measurements = measurements;
            this.diagram = diagram;
            this.hud = hud;
            var slab = bench.Overview.parent;
            var tray = slab.Find("Lead preparation tray");
            probe = tray.Find("Probe grip");
            probeTip = tray.Find("Probe tip");
            paper = tray.Find("Sandpaper");
            sample = slab.Find("Nichrome board/Nichrome sample");
            probeHome = probe.position;
            tipHome = probeTip.position;
            wiring.BeginDirect = Begin;
            wiring.DragDirect = Drag;
            wiring.EndDirect = End;
            wiring.CancelDirect = Cancel;
            wiring.WiperReleased = () =>
            {
                if (wiring.Guided && Mathf.Abs(measurements.Rheostat - suggested) <= 2)
                    measurements.SetRheostat(suggested);
            };
            diagram.Changed += Invalidate;
            wiring.Changed += Invalidate;
            measurements.Changed += Invalidate;
        }

        internal void Restore(OhmProgress saved)
        {
            if (cleaningRoutine != null) bench.StopCoroutine(cleaningRoutine);
            cleaningRoutine = null;
            cleaning = cleaningAnimation = false;
            ResetProbe();
            progress = saved;
            identified = saved.inspectedApparatus & AllApparatus;
            lastIdentified = -1;
            introductionDone = identified == AllApparatus;
            checkingWires = false;
            turningCurrent = null;
            supplyPointerAngle = null;
            zeros = 0;
            Invalidate();
        }

        internal void Invalidate() => dirty = true;

        internal void Update(bool active)
        {
            if (!active || wiring.Busy) return;
            if (dirty && wiring.Guided && measurements.Closed && measurements.ReadingValid)
                suggested = measurements.Count == 0 && !measurements.Retaking ? 25 : measurements.SuggestedResistance;
            bool canCapture = AtReadingPosition && measurements.ReadyToCapture;
            if (!dirty && ready == canCapture && wiringFeedback == wiring.Feedback
                && pendingSource == wiring.PendingSource && hasSelectedLead == wiring.HasSelection) return;
            dirty = false;
            ready = canCapture;
            wiringFeedback = wiring.Feedback;
            pendingSource = wiring.PendingSource;
            hasSelectedLead = wiring.HasSelection;
            task = Next();
            if (wiring.Guided && CurrentCheck() is OhmCheck check && (progress.reviewedChecks & (1 << (int)check)) == 0)
            {
                bench.OpenCheck(check);
                return;
            }
            var missing = MissingLead();
            wiring.MountingSample = task == Task.Mount;
            wiring.AcceptLinks = task == Task.Wire;
            wiring.Expected = wiring.Guided && task == Task.Wire ? missing : null;
            bench.ShowTools(task == Task.Wire && !wiring.HasDraft && wiring.Leads.Count > 0,
                wiring.Guided && task == Task.Wire && (wiring.HasSelection || wiring.Leads.Count == 7));
            bench.ShowWorkbook(task == Task.Workbook);
            ChooseView();
            Present(missing);
        }

        bool AtReadingPosition => !wiring.Guided || measurements.Rheostat == suggested;

        OhmCheck? CurrentCheck() => task switch
        {
            Task.Diagram when diagram.Placed == 0 => OhmCheck.Apparatus,
            Task.Clean => OhmCheck.Key,
            Task.Meters when turningCurrent == null && wiring.CurrentRange == MeterRange.Off && wiring.VoltageRange == MeterRange.Off
                => OhmCheck.Contacts,
            Task.Meters when turningCurrent == null && (wiring.CurrentRange == MeterRange.Dc10A || wiring.VoltageRange == MeterRange.Dc20V)
                => OhmCheck.MeterMode,
            Task.Zero => OhmCheck.Wiring,
            Task.Center or Task.Power => OhmCheck.Zero,
            Task.Observe when measurements.Count == 1 && !measurements.Retaking => OhmCheck.Readings,
            Task.Observe when measurements.Count == 3 && !measurements.Retaking => OhmCheck.Resistance,
            Task.Workbook when !progress.submitted => OhmCheck.Graph,
            _ => null
        };

        Task Next()
        {
            if (!introductionDone && wiring.Guided) return Task.Identify;
            if (!diagram.IsComplete(wiring.Guided)) return Task.Diagram;
            if (!wiring.Prepared) return Task.Clean;
            if (!wiring.SampleMounted) return Task.Mount;
            if (measurements.Closed)
            {
                if (!measurements.ReadingValid) return Task.Fix;
                return measurements.Count == 5 && !measurements.Retaking ? Task.OpenKey : Task.Observe;
            }
            if (checkingWires && measurements.Powered) return Task.PowerOff;
            if (measurements.Count == 5 && !measurements.Retaking)
                return measurements.Powered ? Task.PowerOff : Task.Workbook;
            if (turningCurrent.HasValue || wiring.CurrentRange != MeterRange.Dc10A || wiring.VoltageRange != MeterRange.Dc20V)
                return Task.Meters;
            if (checkingWires || wiring.Leads.Count < OhmWiring.Reference.Length || wiring.Guided && MissingLead().HasValue)
                return Task.Wire;
            if (!measurements.ZeroChecked) return Task.Zero;
            if (measurements.Count == 0 && measurements.Rheostat != 25) return Task.Center;
            if (!measurements.Powered) return Task.Power;
            return measurements.Supply != OhmCircuit.ObservationSupply ? Task.Voltage : Task.CloseKey;
        }

        Lead? MissingLead()
        {
            foreach (var expected in OhmWiring.Reference)
                if (!wiring.Leads.Any(lead => lead.Joins(expected.from, expected.to))) return expected;
            return null;
        }

        Terminal? BlockingSocket(Lead expected)
        {
            foreach (var lead in wiring.Leads)
            {
                if (lead.Uses(expected.from)) return expected.from;
                if (lead.Uses(expected.to)) return expected.to;
            }
            return null;
        }

        void ChooseView()
        {
            var view = task switch
            {
                Task.Center => bench.RheostatView,
                Task.Mount => bench.SampleView,
                Task.Power or Task.Voltage or Task.PowerOff => bench.SupplyView,
                Task.Meters when turningCurrent == true => bench.CurrentView,
                Task.Meters when turningCurrent == false => bench.VoltageView,
                Task.Observe when ready || AtReadingPosition && measurements.DifferentReading &&
                    (measurements.Count > 0 || measurements.Retaking || measurements.Rheostat == 25) => bench.ReadingView,
                Task.Observe => bench.RheostatView,
                Task.Zero => bench.ReadingView,
                _ => bench.Overview
            };
            bench.Focus(view);
        }

        void Present(Lead? missing)
        {
            switch (task)
            {
                case Task.Identify:
                    bool finished = identified == AllApparatus;
                    var item = lastIdentified >= 0 ? Equipment[lastIdentified] : default;
                    hud.Present(1, "Meet the equipment", lastIdentified < 0 ? "Tap each circled item to see what it does."
                            : item.Name + " · " + item.Description,
                        CountBits(identified), Equipment.Length, "items", finished ? "Continue" : null,
                        () => { introductionDone = true; Invalidate(); }, item.Use ?? "", showContext: true);
                    break;
                case Task.Diagram:
                    hud.Present(2, "Draw the circuit", wiring.Guided ? "Open the diagram and follow the highlighted connections."
                            : "Open the diagram and draw how you’ll connect the equipment.",
                        diagram.LinkCount, 7, "links", "Open diagram", bench.OpenDiagram);
                    break;
                case Task.Clean:
                    hud.Present(3, "Clean the lead end", "Drag the red probe onto the mint circle on the sandpaper.",
                        0, 1, "prepared");
                    break;
                case Task.Mount:
                    string side = wiring.SecuredSampleEnds == 0 ? "left" : "right";
                    string mountCue = wiring.SampleWrapped ? $"Tap the {side} cap to tighten it over the wire."
                        : wiring.IsOpen(wiring.SamplePost) ? $"Drag the loose {side} end onto the exposed metal post."
                        : $"Tap the {side} screw cap to loosen it and expose the metal.";
                    hud.Present(3, "Mount the nichrome wire", string.IsNullOrEmpty(wiringFeedback) ? mountCue : wiringFeedback,
                        wiring.SecuredSampleEnds, 2, "ends secured",
                        context: "The cap holds the wire against the metal post so it makes electrical contact.", showContext: true);
                    break;
                case Task.Meters:
                    bool current = wiring.CurrentRange != MeterRange.Dc10A;
                    string meter = current ? "left current meter" : "right voltage meter";
                    int set = (wiring.CurrentRange == MeterRange.Dc10A ? 1 : 0) + (wiring.VoltageRange == MeterRange.Dc20V ? 1 : 0);
                    if (turningCurrent is bool isCurrent)
                    {
                        var requested = isCurrent ? MeterRange.Dc10A : MeterRange.Dc20V;
                        string display = "Dial now at " + OhmWiring.RangeName(dialRange) + ". ";
                        hud.Present(4, isCurrent ? "Turn the current dial" : "Turn the voltage dial",
                            wiring.Guided ? $"Drag around the dial to {OhmWiring.RangeName(requested)}, then release."
                                : "Drag around the dial, then release at the range you want.",
                            set, 2, "meters set", "Back to bench", () => { turningCurrent = null; Invalidate(); },
                            display + (isCurrent ? "A means current. Use the red 10A jack." : "V means voltage. Use the red VΩ jack."));
                    }
                    else
                        hud.Present(4, "Set the meters", wiring.Guided ? "Tap the circled " + meter + "."
                            : "Tap the meter you want to set.",
                            set, 2, "meters set", context: "With DC, the supply keeps the same positive and negative sides.");
                    break;
                case Task.Wire:
                    hud.Present(5, "Connect the circuit", WireInstruction(missing),
                        wiring.Guided ? OhmWiring.Reference.Count(route => wiring.Leads.Any(l => l.Joins(route.from, route.to))) : wiring.Leads.Count,
                        7, "wires", checkingWires && wiring.Leads.Count == 7 ? "Try circuit" : null,
                        () => { checkingWires = false; Invalidate(); },
                        wiring.Guided && missing is Lead link ? ConnectionNotes[Array.IndexOf(OhmWiring.Reference, link)]
                            : "The current meter goes in the loop. The voltage meter goes across the sample.");
                    break;
                case Task.Zero:
                    hud.Present(6, "Check the meters", "Tap each meter screen after you see 0.00. Keep the key open.",
                        CountBits(zeros), 5, "setup actions");
                    break;
                case Task.Center:
                    hud.Present(6, "Center the rheostat", wiring.Guided ? "Drag the black handle to the mint circle in the middle."
                            : "Drag the black handle to the middle of the coil.", 2, 5, "setup actions",
                        context: "The middle is 25 Ω. Starting here leaves room to raise or lower the current for later readings.",
                        showContext: wiring.Guided);
                    break;
                case Task.Power:
                    hud.Present(6, "Turn on the supply", "Press POWER below the voltage knob.", 2, 5, "setup actions");
                    break;
                case Task.Voltage:
                    hud.Present(6, "Set the supply to 6.0 V", $"Drag around the knob {(measurements.Supply < OhmCircuit.ObservationSupply ? "clockwise" : "counterclockwise")} until the display reads 6.0 V.",
                        3, 5, "setup actions", context: "Keep the key open while you set the supply. Use this same voltage for all five readings.",
                        showContext: wiring.Guided);
                    break;
                case Task.CloseKey:
                    hud.Present(6, "Close the circuit", "Tap the brass switch to close the key. The meter readings will change.",
                        4, 5, "setup actions");
                    break;
                case Task.Fix:
                    hud.Present(7, "Check the circuit", "Tap the brass key to open the circuit.", measurements.Count, 5, "readings",
                        context: measurements.Feedback, showContext: true);
                    break;
                case Task.Observe:
                    bool takingReading = bench.View == bench.ReadingView;
                    bool movingRight = suggested > measurements.Rheostat;
                    hud.Present(7, ready ? "Read both meter screens" : takingReading ? "Wait for the readings"
                            : wiring.Guided ? $"Set the rheostat to {suggested:0} Ω" : "Move the rheostat slider",
                        ready ? "Read A on the left and V on the right, then tap Capture pair."
                            : takingReading ? "Hold the slider still so the readings settle."
                            : wiring.Guided ? $"Drag the black handle {(movingRight ? "right" : "left")} to the mint circle, then let go."
                            : "Drag the black slider to get a different reading.",
                        measurements.Count, 5, "readings", ready ? "Capture pair" : null, Record,
                        wiring.Guided ? RheostatExplanation(takingReading, movingRight) : "",
                        showContext: true);
                    break;
                case Task.OpenKey:
                    hud.Present(8, "Switch off the circuit", "Tap the brass key to open it.", 0, 3, "finished");
                    break;
                case Task.PowerOff:
                    if (checkingWires)
                        hud.Present(7, "Switch off before rewiring", "Press POWER below the voltage knob.", measurements.Count, 5, "readings");
                    else
                        hud.Present(8, "Switch off the supply", "Press POWER below the voltage knob.", 1, 3, "finished");
                    break;
                case Task.Workbook:
                    hud.Present(8, progress.submitted ? "Practical complete" : "Finish your workbook",
                        progress.submitted ? "Your readings, graph and conclusion are saved."
                            : "Copy the saved readings, calculate resistance and plot your graph.",
                        progress.submitted ? 3 : 2, 3, "finished", "Open workbook", bench.OpenWorkbook);
                    break;
            }
        }

        string RheostatExplanation(bool takingReading, bool movingRight)
        {
            if (takingReading)
                return measurements.Count == 0 && !measurements.Retaking
                    ? "25 Ω is the midpoint. It leaves room for both higher and lower currents at the same 6.0 V."
                    : "";

            string effect = movingRight ? "Moving right adds resistance and lowers the current."
                : "Moving left reduces resistance and raises the current.";
            string reason = suggested switch
            {
                25 => "25 Ω is the midpoint of the slider.",
                50 => "50 Ω gives the lowest current in the set.",
                35 => "35 Ω fills the gap between the first two currents.",
                15 => "15 Ω gives a reading above the midpoint current.",
                5 => "5 Ω gives the highest current in the set.",
                _ => "Choose a current clearly different from the ones you’ve saved."
            };
            return effect + " " + reason;
        }

        string WireInstruction(Lead? missing)
        {
            if (wiring.PendingTighten is Terminal tighten)
                return $"Tap the cap at {SocketName(tighten)} to tighten it.";
            if (wiring.HasDraft) return "Wait for the cap to seat over the wire.";
            if (hasSelectedLead) return "Lead selected. Open Wiring tools to remove it.";
            if (!string.IsNullOrEmpty(wiringFeedback)) return wiringFeedback;
            if (wiring.Guided && missing is Lead lead)
            {
                if (BlockingSocket(lead).HasValue) return "Tap the circled connection, then choose Wiring tools → Remove selected.";
                if (wiring.Leads.Count == 7) return "Open Wiring tools and undo a wire to make room.";
                if (wiring.NeedsOpening(lead) is Terminal open) return $"Tap the cap at {SocketName(open)} to expose the metal.";
                if (pendingSource is Terminal start)
                    return $"Tap {SocketName(start == lead.from ? lead.to : lead.from)} to attach the other end.";
                return $"Drag from {SocketName(lead.from)} to {SocketName(lead.to)}.";
            }
            return pendingSource.HasValue ? "Tap the other socket to attach the lead."
                : "Loosen screw caps, drag between terminals, then tighten the wraps. Push plugs into meter and supply jacks.";
        }

        void Record()
        {
            measurements.Capture();
        }

        internal void DrawRings(bool atBench, bool overlayOpen)
        {
            hud.ShowPrompt(!overlayOpen);
            hud.BeginRings();
            if (atBench && !overlayOpen && !bench.Rig.IsTransitioning) MarkTask();
            hud.EndRings(bench.Rig.ViewCamera, atBench, wiring.Busy || bench.Rig.IsTransitioning);
        }

        void MarkTask()
        {
            switch (task)
            {
                case Task.Identify:
                    for (int i = 0; i < Equipment.Length; i++)
                        if ((identified & 1 << i) == 0) Ring(i, Apparatus(i), i switch { 0 => 122, 3 or 6 => 55, _ => 86 });
                    if (lastIdentified >= 0)
                        hud.Ring(0, bench.Rig.ViewCamera, Apparatus(lastIdentified), 0, draw: false);
                    break;
                case Task.Clean:
                    if (!cleaningAnimation)
                    {
                        Ring(0, probe.position, 50);
                        Ring(1, paper.position, 54, true);
                    }
                    break;
                case Task.Mount:
                    if (wiring.IsOpen(wiring.SamplePost) && !wiring.SampleWrapped)
                    {
                        Ring(0, wiring.LooseSampleTip, 46);
                        Ring(1, wiring.ContactSpot(wiring.SamplePost), 51, true);
                    }
                    else Ring(0, wiring.ContactSpot(wiring.SamplePost), 52);
                    break;
                case Task.Meters:
                    bool current = turningCurrent ?? wiring.CurrentRange != MeterRange.Dc10A;
                    var dial = Dial(current);
                    Ring(0, dial.position, turningCurrent.HasValue ? 102 : 72);
                    if (turningCurrent.HasValue)
                    {
                        var range = current ? MeterRange.Dc10A : MeterRange.Dc20V;
                        Vector3 marker = Quaternion.Euler(0, 0, OhmWiring.Angle(range)) * (Vector3.up * .039f);
                        Ring(1, dial.position + dial.parent.TransformVector(marker), 30, true);
                    }
                    break;
                case Task.Wire:
                    if (wiring.Expected is not Lead route) break;
                    if (BlockingSocket(route) is Terminal blocked) Ring(0, Socket(blocked), 47);
                    else if (wiring.PendingTighten is Terminal tighten) Ring(0, wiring.ContactSpot(tighten), 47);
                    else if (wiring.HasDraft) break;
                    else if (wiring.NeedsOpening(route) is Terminal open) Ring(0, wiring.ContactSpot(open), 47);
                    else { Ring(0, Socket(route.from), 41); Ring(1, Socket(route.to), 45, true); }
                    break;
                case Task.Zero:
                    if ((zeros & 1) == 0) Ring(0, bench.CurrentDisplay.transform.position, 90);
                    if ((zeros & 2) == 0) Ring(1, bench.VoltageDisplay.transform.position, 90);
                    break;
                case Task.Center:
                case Task.Observe when bench.View == bench.RheostatView:
                    var destination = WiperPosition(task == Task.Center ? 25 : suggested);
                    if (!wiring.Busy) Ring(0, bench.Wiper.position, 55);
                    Ring(1, destination, 45, true);
                    if (wiring.Guided) hud.DragDirection(bench.Rig.ViewCamera, bench.Wiper.position, destination);
                    break;
                case Task.Observe:
                    Ring(0, bench.CurrentDisplay.transform.position, 90);
                    Ring(1, bench.VoltageDisplay.transform.position, 90, true);
                    break;
                case Task.Power:
                case Task.PowerOff:
                    Ring(0, bench.SupplyPower.position, 46);
                    break;
                case Task.Voltage:
                    Ring(0, bench.SupplyKnob.position, 50);
                    break;
                case Task.CloseKey:
                case Task.OpenKey:
                case Task.Fix:
                    Ring(0, KeyPosition(), 62);
                    break;
            }
        }

        void Ring(int i, Vector3 point, float width, bool target = false) =>
            hud.Ring(i, bench.Rig.ViewCamera, point, width, target, wiring.Guided);
        Vector3 Socket(Terminal terminal) => bench.Sockets[(int)terminal].position;
        Vector3 KeyPosition() => bench.Key.TransformPoint(new Vector3(.045f, .018f, 0));
        Vector3 WiperPosition(float resistance)
        {
            var wiper = bench.Wiper;
            return wiper.parent.TransformPoint(new Vector3(OhmMeasurements.WiperX(resistance), wiper.localPosition.y, wiper.localPosition.z));
        }

        Vector3 Apparatus(int index) => index switch
        {
            0 => bench.SupplyDisplay.transform.position,
            1 => bench.CurrentSelector.position,
            2 => bench.VoltageSelector.position,
            3 => KeyPosition(),
            4 => bench.Wiper.position,
            5 => sample.position,
            _ => paper.position
        };

        bool Begin(Vector2 position)
        {
            dragOrigin = position;
            directMoved = false;
            pendingTarget = -1;
            if (task == Task.Identify)
            {
                float best = Screen.height * .08f;
                for (int i = 0; i < Equipment.Length; i++)
                {
                    if ((identified & 1 << i) != 0) continue;
                    float distance = Distance(position, Apparatus(i));
                    if (distance >= best) continue;
                    best = distance;
                    pendingTarget = i;
                }
                return pendingTarget >= 0;
            }
            if (task == Task.Clean)
            {
                cleaning = !cleaningAnimation && Near(position, probe.position, .058f);
                return cleaning;
            }
            if (task is Task.Meters or Task.Zero)
            {
                if (task == Task.Meters && turningCurrent is bool current)
                    return Near(position, Dial(current).position, .19f);
                Vector3 left = task == Task.Zero ? bench.CurrentDisplay.transform.position : bench.CurrentSelector.position;
                Vector3 right = task == Task.Zero ? bench.VoltageDisplay.transform.position : bench.VoltageSelector.position;
                if (Near(position, left, .075f)) pendingTarget = 0;
                else if (Near(position, right, .075f)) pendingTarget = 1;
                return pendingTarget >= 0;
            }
            if (task == Task.Voltage && Near(position, bench.SupplyKnob.position, .065f))
            {
                supplyPointerAngle = PointerAngle(bench.SupplyKnob, position, .005f);
                supplyTurningValue = measurements.Supply;
                return true;
            }
            return task switch
            {
                Task.Center => Near(position, bench.Wiper.position, .08f),
                Task.Power or Task.PowerOff => Near(position, bench.SupplyPower.position, .047f),
                Task.CloseKey or Task.OpenKey or Task.Fix => Near(position, KeyPosition(), .08f),
                _ => false
            };
        }

        void Drag(Vector2 position)
        {
            directMoved |= Vector2.Distance(dragOrigin, position) > Screen.height * .014f;
            if (task == Task.Voltage)
            {
                RotateSupply(position);
                return;
            }
            if (task == Task.Meters && turningCurrent is bool current)
            {
                RotateDial(current, position);
                return;
            }
            if (task == Task.Center) measurements.SetRheostat(ResistanceAt(position));
            if (!cleaning) return;
            var camera = bench.Rig.ViewCamera;
            var ray = camera.ScreenPointToRay(position);
            var plane = new Plane(Vector3.up, probeHome + Vector3.up * .018f);
            if (!plane.Raycast(ray, out float distance)) return;
            probe.position = ray.GetPoint(distance);
            probeTip.position = probe.position + tipHome - probeHome;
        }

        void End(Vector2 position)
        {
            if (task == Task.Voltage && directMoved) RotateSupply(position);
            bool cleaned = cleaning && directMoved && Near(position, paper.position, .06f);
            if (cleaned)
            {
                cleaning = false;
                cleaningRoutine = bench.StartCoroutine(CleanLead());
                return;
            }
            Cancel();
            if (task == Task.Center)
            {
                if (directMoved && Near(position, WiperPosition(25), .045f)) measurements.SetRheostat(25);
                Invalidate();
                return;
            }
            if (task == Task.Meters && turningCurrent is bool current)
            {
                if (!directMoved) return;
                RotateDial(current, position);
                if (dialRange == (current ? MeterRange.Dc10A : MeterRange.Dc20V)) turningCurrent = null;
                Invalidate();
                return;
            }
            if (directMoved) return;
            switch (task)
            {
                case Task.Identify:
                    identified |= 1 << pendingTarget;
                    lastIdentified = pendingTarget;
                    progress.inspectedApparatus = identified;
                    bench.PlayClick();
                    bench.QueueSave();
                    break;
                case Task.Meters:
                    turningCurrent = pendingTarget == 0;
                    dialRange = turningCurrent == true ? wiring.CurrentRange : wiring.VoltageRange;
                    bench.PlayClick();
                    break;
                case Task.Zero:
                    zeros |= 1 << pendingTarget;
                    bench.PlayClick();
                    if (zeros == 3) measurements.CheckZero();
                    break;
                case Task.Power:
                case Task.PowerOff:
                    measurements.TogglePower();
                    break;
                case Task.CloseKey:
                case Task.OpenKey:
                    measurements.ToggleKey();
                    break;
                case Task.Fix:
                    checkingWires = true;
                    measurements.ToggleKey();
                    break;
            }
            Invalidate();
        }

        Transform Dial(bool current) => current ? bench.CurrentSelector : bench.VoltageSelector;

        float? PointerAngle(Transform knob, Vector2 pointer, float deadZone)
        {
            var ray = bench.Rig.ViewCamera.ScreenPointToRay(pointer);
            if (!new Plane(knob.parent.forward, knob.position).Raycast(ray, out float distance)) return null;
            var delta = ray.GetPoint(distance) - knob.position;
            float x = Vector3.Dot(delta, knob.parent.right);
            float y = Vector3.Dot(delta, knob.parent.up);
            return x * x + y * y < deadZone * deadZone ? null : Mathf.Atan2(-x, y) * Mathf.Rad2Deg;
        }

        void RotateSupply(Vector2 pointer)
        {
            if (PointerAngle(bench.SupplyKnob, pointer, .005f) is not float angle) return;
            if (supplyPointerAngle is float previous)
            {
                float delta = Mathf.DeltaAngle(previous, angle);
                supplyTurningValue = Mathf.Clamp(supplyTurningValue - delta / OhmMeasurements.SupplyDegreesPerVolt,
                    0, (float)OhmCircuit.MaximumSupply);
                measurements.SetSupply(supplyTurningValue);
            }
            supplyPointerAngle = angle;
        }

        void RotateDial(bool current, Vector2 pointer)
        {
            if (PointerAngle(Dial(current), pointer, .011f) is not float angle) return;
            MeterRange nearest = dialRange;
            float best = 180;
            foreach (var range in DialRanges)
            {
                float gap = Mathf.Abs(Mathf.DeltaAngle(angle, OhmWiring.Angle(range)));
                if (gap >= best) continue;
                best = gap;
                nearest = range;
            }
            if (nearest == dialRange) return;
            dialRange = nearest;
            wiring.SetRange(current, nearest);
            Present(MissingLead());
        }

        void Cancel()
        {
            supplyPointerAngle = null;
            if (!cleaning) return;
            cleaning = false;
            ResetProbe();
        }

        void ResetProbe()
        {
            probe.position = probeHome;
            probeTip.position = tipHome;
        }

        IEnumerator CleanLead()
        {
            cleaningAnimation = true;
            var center = paper.position + Vector3.up * .018f;
            const float duration = .28f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                probe.position = center + paper.right * (Mathf.Sin(elapsed / duration * Mathf.PI * 4) * .022f);
                probeTip.position = probe.position + tipHome - probeHome;
                yield return null;
            }
            ResetProbe();
            cleaningRoutine = null;
            cleaningAnimation = false;
            wiring.SetPreparation(true);
        }

        float ResistanceAt(Vector2 screen)
        {
            var camera = bench.Rig.ViewCamera;
            Vector2 start = camera.WorldToScreenPoint(WiperPosition(0));
            Vector2 end = camera.WorldToScreenPoint(WiperPosition(50));
            Vector2 line = end - start;
            return line.sqrMagnitude > 0 ? Mathf.Round(Mathf.Clamp01(Vector2.Dot(screen - start, line) / line.sqrMagnitude) * 50) : 25;
        }

        bool Near(Vector2 point, Vector3 world, float radius) => Distance(point, world) <= Screen.height * radius;
        float Distance(Vector2 point, Vector3 world)
        {
            var screen = bench.Rig.ViewCamera.WorldToScreenPoint(world);
            return screen.z > 0 ? Vector2.Distance(point, screen) : float.PositiveInfinity;
        }

        static int CountBits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask >>= 1) count += mask & 1;
            return count;
        }

        static string SocketName(Terminal terminal) => terminal switch
        {
            Terminal.SupplyPlus => "supply +", Terminal.SupplyMinus => "supply −",
            Terminal.KeyIn => "the left key post", Terminal.KeyOut => "the right key post",
            Terminal.RheostatFixed => "the left rheostat post", Terminal.RheostatWiper => "the right rheostat post",
            Terminal.CurrentCom => "left meter COM", Terminal.CurrentAmps => "left meter 10A",
            Terminal.VoltageCom => "right meter COM", Terminal.VoltageVolts => "right meter VΩ",
            Terminal.WireLeft => "the rear left nichrome post", Terminal.WireLeftSense => "the front left nichrome post",
            Terminal.WireRight => "the rear right nichrome post", Terminal.WireRightSense => "the front right nichrome post",
            _ => terminal.ToString()
        };
    }
}
