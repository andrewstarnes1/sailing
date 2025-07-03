using System.Collections.Generic;
using UnityEngine;

public class BoatSailManager : MonoBehaviour
{
    public Rigidbody boatBody;
    public float heelSensitivity = 0.5f; // Tune for more or less heel

    private RealisticSailPhysics[] sails;

    void Start()
    {
        sails = GetComponentsInChildren<RealisticSailPhysics>();
    }

    void FixedUpdate()
    {
        Vector3 totalForce = Vector3.zero;
        Vector3 weightedPosition = Vector3.zero;
        /*
        foreach (var sail in sails)
        {
            Vector3 force = sail.AerodynamicForce;
            Vector3 pos = sail.AerodynamicForcePosition;

            totalForce += force;
            weightedPosition += pos * force.magnitude;
        }

        if (totalForce.magnitude > 0.01f)
        {
            // Determine average point of force application
            Vector3 avgForcePosition = weightedPosition / totalForce.magnitude;

            // Split total aerodynamic force into forward and lateral components
            Vector3 forwardDir = boatBody.transform.forward;
            Vector3 lateralDir = boatBody.transform.right;

            Vector3 forwardForce = Vector3.Project(totalForce, forwardDir);
            Vector3 lateralForce = Vector3.Project(totalForce, lateralDir);

            // Apply forward propulsion at center-of-mass (no torque)
            boatBody.AddForce(forwardForce, ForceMode.Force);

            // Apply lateral force vertically aligned with center-of-mass to avoid yawing
            boatBody.AddForceAtPosition(lateralForce, boatBody.worldCenterOfMass, ForceMode.Force);

            // === REINTRODUCE HEELING ===
            // Explicitly apply roll torque proportional to lateral aerodynamic force magnitude
            Vector3 rollAxis = boatBody.transform.forward;
            float heelTorqueMagnitude = lateralForce.magnitude * heelSensitivity;

            // Direction depends on wind/sail orientation
            float heelDirection = Vector3.Dot(lateralForce, boatBody.transform.right) > 0 ? -1f : 1f;

            Vector3 heelTorque = rollAxis * heelTorqueMagnitude * heelDirection;
            boatBody.AddTorque(heelTorque, ForceMode.Force);
        }*/
    }
}
