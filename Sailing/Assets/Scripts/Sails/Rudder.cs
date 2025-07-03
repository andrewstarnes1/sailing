using UnityEngine;

public class Rudder : MonoBehaviour
{
    [Header("References")]
    public Transform bladeVisual;
    public Rigidbody boatRb;

    [Header("Geometry")]
    public float area = 0.5f;
    public float liftSlope = 2 * Mathf.PI;
    public float maxAngle = 30f;

    [Header("Input")]
    [Range(-1, 1)] public float steerInput;

    void Reset()
    {
        if (boatRb == null)
            boatRb = GetComponentInParent<Rigidbody>();
        if (bladeVisual == null)
            bladeVisual = transform.GetChild(0);
    }

    void FixedUpdate()
    {
        if (boatRb == null)
            boatRb = GetComponentInParent<Rigidbody>();

        float rudderAngleDeg = steerInput * maxAngle;
        float rudderAngleRad = rudderAngleDeg * Mathf.Deg2Rad;

        Vector3 wp = transform.position;
        Vector3 hullVel = boatRb.GetPointVelocity(wp);
        float speed = hullVel.magnitude;

        if (speed < 0.1f) return;

        Vector3 localFlow = transform.InverseTransformDirection(hullVel.normalized);
        float aoa = Mathf.Atan2(localFlow.x, localFlow.z) - rudderAngleRad;

        float CL = Mathf.Clamp(liftSlope * aoa, -0.8f, 0.8f);

        float waterDensity = 1000f;
        float liftMagnitude = 0.5f * waterDensity * speed * speed * area * CL;

        Vector3 flowDirection = hullVel.normalized;
        Vector3 liftDirection = Vector3.Cross(flowDirection, transform.up).normalized;

        boatRb.AddForceAtPosition(liftDirection * liftMagnitude, wp, ForceMode.Force);

        float yawDampingTorque = -boatRb.angularVelocity.y * 500f;
        boatRb.AddTorque(Vector3.up * yawDampingTorque, ForceMode.Force);
    }

    void LateUpdate()
    {
        float angleDeg = steerInput * maxAngle;
        bladeVisual.localRotation = Quaternion.Euler(0f, -angleDeg, 0f);
    }
}
