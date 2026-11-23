public static class PracticalTaskRules
{
    public static bool Done(string id,PreparationState p,PracticalDefinition definition,ThermalSimulation simulation)
    {
        switch(id)
        {
            case "setup":return p.tripod&&p.gauze&&p.beaker;
            case "water":return p.water;
            case "seal":return p.capillarySealed;
            case "sample":return p.sample;
            case "grind":return p.grind>=definition.sample.grindingRevolutions;
            case "pack":return p.sampleAccepted;
            case "invert":return p.capillaryInverted;
            case "attach":return p.band;
            case "immerse":return p.immersed;
            case "ignite":return p.burnerStarted;
            case "heat":return simulation.HasRequiredObservations;
            case "shutdown":return simulation.HasRequiredObservations&&!simulation.lit&&p.shutdownConfirmed;
            default:return false;
        }
    }
    public static bool Available(string id,PreparationState p,PracticalDefinition definition,ThermalSimulation simulation)
    {
        switch(id)
        {
            case "setup":case "water":case "seal":return true;
            case "sample":return definition.workflow==PracticalWorkflow.Melting||p.capillarySealed;
            case "grind":return p.sample;
            case "pack":return p.capillarySealed&&p.grind>=definition.sample.grindingRevolutions;
            case "invert":return p.capillarySealed&&p.sample;
            case "attach":return definition.workflow==PracticalWorkflow.Melting?p.sampleAccepted:p.capillaryInverted&&p.sample;
            case "immerse":return p.beaker&&p.water&&p.band;
            case "ignite":return p.capillarySealed&&p.IsPrepared(definition);
            case "heat":return p.burnerStarted;
            case "shutdown":return simulation.HasRequiredObservations;
            case "notebook":return Done("shutdown",p,definition,simulation);
            default:return false;
        }
    }
}
