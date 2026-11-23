using System;
using System.Collections.Generic;
using System.Globalization;

public static class PracticalTrialStatistics
{
    public static bool Reading(string text,out float value)
    {return float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=0&&value<=100;}
    public static bool Mean(IList<PracticalTrial> trials,int required,out float mean)
    {
        mean=0;if(required<1||trials==null||trials.Count!=required)return false;
        var numbers=new HashSet<int>();
        foreach(var trial in trials)
        {
            float reading;
            if(!trial.completed||trial.number<1||trial.number>required||!numbers.Add(trial.number)||!Reading(trial.enteredOnset,out reading))return false;
            mean+=reading;
        }
        mean/=required;return true;
    }
}
