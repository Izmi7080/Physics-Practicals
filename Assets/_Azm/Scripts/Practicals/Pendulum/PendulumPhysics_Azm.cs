using UnityEngine;

namespace VirtualLab.Azm.Pendulum
{
    /*
     * Layer: Logic (pure maths - no MonoBehaviour, no input, camera or UI).
     * Purpose: The pendulum's motion, scripted from the equation (no Rigidbody/PhysX, so nothing
     * damps or jitters the measured times).
     *
     * Small-angle simple harmonic motion:   theta(t) = theta0 * cos( sqrt(g / L) * t )
     * with L = l + r in METRES (effective length: thread length l plus the bob's radius r).
     * Why the closed form is fine: the release angle is capped at 15 degrees (syllabus), where the
     * small-angle approximation sin(theta) ~ theta holds to well under 1 % in the period; solving
     * theta'' = -(g/L) sin(theta) numerically would change the times by less than a student can
     * tap. No damping: the bob keeps swinging for both timing runs at one length.
     * Units: angles in degrees, lengths in metres, time in seconds, g in m/s^2.
     * Talks to: called by PendulumController_Azm.
     */
    public static class PendulumPhysics_Azm
    {
        // Swing angle in degrees, t seconds after release from rest at amplitudeDeg.
        public static float AngleDeg(float amplitudeDeg, float effectiveLengthM, float g, float t)
        {
            float omega = Mathf.Sqrt(g / effectiveLengthM);
            return amplitudeDeg * Mathf.Cos(omega * t);
        }

        // T = 2 * pi * sqrt(L / g). Used only to judge the student's tapping rhythm; it is never
        // shown to the student, so the simulation never hands them the answer.
        public static float Period(float effectiveLengthM, float g)
        {
            return 2f * Mathf.PI * Mathf.Sqrt(effectiveLengthM / g);
        }
    }
}
