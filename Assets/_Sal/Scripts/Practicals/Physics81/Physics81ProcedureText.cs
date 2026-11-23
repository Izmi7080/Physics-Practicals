using System.Text;

namespace VirtualLab.Physics81
{
    internal static class Physics81ProcedureText
    {
        static readonly (SetupStep Start, string Title, string Method)[] Steps =
        {
            (SetupStep.PowerBalance, "Weigh the brass bob", "Switch on the digital balance. Check the empty display, place the bob on the platform and record its settled mass."),
            (SetupStep.EmptyCupOnPan, "Weigh the empty cup", "Place the dry polystyrene cup on the scale and record its settled mass."),
            (SetupStep.RemoveEmptyCup, "Move the cup to the mixing station", "Drag the empty cup onto its place on the bench."),
            (SetupStep.TakeCylinder, "Measure 30 mL of water", "Carry the cylinder to the main tap. Stop near the 30 mL line. Return to the table and drag the cylinder over the cup to pour."),
            (SetupStep.FilledCupOnPan, "Weigh the cup with water", "Record the settled mass of the cup and water. Return the cup to the mixing station."),
            (SetupStep.PlaceBathStand, "Assemble the heating station", "Place the clamp stand, tripod, gauze and burner in order."),
            (SetupStep.TakeBeaker, "Fill the heating beaker", "Carry the beaker to the main tap. Read the meniscus at eye level and stop near 150 mL."),
            (SetupStep.BeakerOnTripod, "Set the beaker on the gauze", "Carry the filled beaker back and place it on the tripod setup."),
            (SetupStep.FitBathProbe, "Fit the bath thermometer", "Suspend the thermometer from the clamp with its bulb in the water and clear of the glass."),
            (SetupStep.LightLamp, "Start heating", "Light the burner on low while you set up the cup."),
            (SetupStep.PlaceCupStand, "Set up the cup station", "Place the clamp stand and fit the cup thermometer above the uncovered cup."),
            (SetupStep.SetCupDepth, "Set the cup thermometer depth", "Keep the bulb submerged and about 10 mm above the cup bottom."),
            (SetupStep.RecordInitial, "Read the initial temperature", "Wait for the liquid column to settle. Read the scale and enter the cup-water temperature."),
            (SetupStep.RaiseCupProbe, "Raise the cup thermometer", "Lift the probe clear of the opening before transferring the bob."),
            (SetupStep.WatchBoil, "Watch the bath boil", "The heat increases automatically. Watch the red column climb, then look for continuous bubbling."),
            (SetupStep.LowerBob, "Lower the bob into the bath", "Move the bob by its suspension into the boiling water. Keep it clear of the glass."),
            (SetupStep.SoakBob, "Soak the bob for ten lab minutes", "Check that the bath reading stays steady while the submerged bob warms to the bath temperature."),
            (SetupStep.RecordBath, "Read the hot bath temperature", "Read and enter the steady bath temperature as the estimate for the hot bob."),
            (SetupStep.TransferBob, "Transfer the hot bob", "Use the thread to lift the bob, move it clear of both rims and lower it into the cup quickly and gently."),
            (SetupStep.LowerCupProbe, "Lower the cup thermometer", "Return the thermometer to its earlier depth."),
            (SetupStep.FitLid, "Cover the cup", "Drag the lid onto the rim and align it around the thermometer."),
            (SetupStep.MixCup, "Mix the cup gently", "Move the cup gently from side to side to spread heat through the water."),
            (SetupStep.RecordMixture, "Read the mixture temperature", "Watch the cup thermometer reach its highest reading and start to cool. Enter the highest reading you saw.")
        };

        public const string Overview = "Aim\nFind the specific heat capacity of a brass bob using the method of mixtures.\n\n" +
            "Principle\nThe hot bob transfers energy to the cooler water. Compare the water’s temperature rise with the bob’s temperature drop using Q = mcΔT.\n\n" +
            "Apparatus\nDigital balance, brass bob and thread, polystyrene cup and lid, two thermometers, graduated cylinder, beaker, tripod, gauze, two clamps and burner.\n\n" +
            "Method source\nPunjab Grade IX Physics Practical Notebook, experiment 8.1, and the demonstrated 23-step virtual practical.";

        public const string Controls = "Getting around\nUse the joystick to walk. Swipe to look around. On desktop use WASD or arrows and drag with the right mouse button to look.\n\n" +
            "Drag an object to its destination. Tap the cylinder or beaker to pick it up when instructed. Guided marks the active object and the matching destination.\n\n" +
            "If you forgot to tare, tap Tare & reweigh while the item is still on the scale. Put it back on the zeroed scale and record again.\n\n" +
            "Use Inspect and + or − to see closer. Back to station returns to where you stood. Read the scales at eye level and enter temperatures beside the apparatus.\n\n" +
            "Drag the hot bob up, left, and down by its thread. Move the cup gently side to side to mix. Opening a page pauses the experiment.";

        public const string Precautions = "Read the bottom of the meniscus at eye level. Keep thermometer bulbs submerged and clear of the glass.\n\n" +
            "Keep the bob at the boil for the full heating time. Handle the hot bob by its suspension. Transfer promptly and gently to limit cooling and spillage.\n\n" +
            "Cover the cup after returning the thermometer. Mix gently, watch for a small drop, then enter the highest temperature you saw.";

        public const string RepeatTrials =
            "What the Punjab notebook asks for\n" +
            "Experiment 8.1, pages 57–59, gives one observation table and one calculation for a single brass bob. A complete trial uses three mass readings and three temperatures. One temperature alone cannot give the specific heat.\n\n" +
            "What you are finding\n" +
            "Specific heat capacity is the energy needed to warm 1 kg of a material by 1 K. It depends on the material and can vary with temperature. A heavier bob of the same brass has greater total heat capacity. Its specific heat is approximately the same over this experiment’s temperature range.\n\n" +
            "Optional repeat practice\n" +
            "Do three complete trials in total to compare repeatability. Use the same dry brass bob, cup and lid. Aim for the same water amounts and similar starting conditions. Keep the soak, transfer and stirring method consistent. Measure the actual masses and temperatures each time.\n\n" +
            "What changes the result\n" +
            "More cup water gives a smaller temperature rise under the same starting conditions. A heavier bob of the same brass gives a larger rise. Use the actual new mass if either changes. Switching materials changes the property being measured. If the bob cools in air, using the bath temperature usually makes the calculated value too low. If water spills, the weighed mass no longer matches what warms up.\n\n" +
            "Calculate a value for each comparable trial, then report the mean and the spread. Record a reason for any excluded trial. Keep your earlier results before starting a new workbook.";

        public static (string title, string body) LearningNote(SetupStep step) => step switch
        {
            SetupStep.EmptyCupOnPan or SetupStep.WeighEmptyCup => ("Why a foam cup?",
                "Polystyrene slows heat loss. The light cup itself absorbs little heat as the water warms."),
            SetupStep.MeasureCup => ("Why about 30 mL?",
                "Too little may leave the bulb exposed. More water gives a smaller temperature rise that is harder to read."),
            SetupStep.MeasureBath => ("Why about 150 mL?",
                "Too little may boil away and uncover the bob. Too much takes longer to heat."),
            SetupStep.SetCupDepth or SetupStep.SetBathDepth => ("Why keep the bulb clear?",
                "A submerged bulb measures the water. Touching the container can affect the reading."),
            SetupStep.RecordInitial => ("Why let it settle?",
                "Give the bulb time to reach the water’s temperature. Read the top of the red column at eye level."),
            SetupStep.WatchBoil => ("What changes at the boil?",
                "The column levels off while water keeps boiling. Much of the added heat turns water into vapour."),
            SetupStep.SoakBob => ("Why soak the bob?",
                "Check that the bath reading stays steady. The cooler brass takes time to reach the water’s temperature."),
            SetupStep.RecordBath => ("Why read the bath?",
                "After soaking, the bath temperature is our estimate of the bob’s temperature just before transfer."),
            SetupStep.TransferBob => ("Why move it quickly?",
                "The bob cools in air. Move it promptly and gently so the water you weighed stays in the cup."),
            SetupStep.FitLid => ("Why cover the cup?",
                "The lid slows heat loss to the air while the hot bob warms the cup water."),
            SetupStep.MixCup => ("Why mix gently?",
                "Mixing spreads the heat through the water, so the thermometer can sample the whole mixture."),
            SetupStep.RecordMixture => ("Why use the peak?",
                "The mixture warms as the bob gives up heat, then cools to the room. Record the highest reading you saw."),
            _ => default
        };

        public static string Heading(SetupStep step)
        {
            if (step == SetupStep.Review) return "Physics 8.1  ·  Review readings";
            int current = Index(step);
            return $"{current + 1} / {Steps.Length}  ·  {Steps[current].Title}";
        }

        public static string Procedure(bool includeMethod, SetupStep? progress = null)
        {
            var text = new StringBuilder();
            for (int i = 0; i < Steps.Length; i++)
            {
                if (i > 0) text.Append("\n\n");
                if (progress.HasValue)
                {
                    int current = progress == SetupStep.Review ? Steps.Length : Index(progress.Value);
                    text.Append(i < current ? "Done  ·  " : i == current ? "Now  ·  " : "Later  ·  ");
                }
                text.Append(i + 1).Append(". ").Append(Steps[i].Title);
                if (includeMethod) text.Append('\n').Append(Steps[i].Method);
            }
            return text.ToString();
        }

        static int Index(SetupStep step)
        {
            for (int i = Steps.Length - 1; i > 0; i--)
                if (step >= Steps[i].Start) return i;
            return 0;
        }
    }
}
