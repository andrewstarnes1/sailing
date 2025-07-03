using KWS;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoatKeel : MonoBehaviour
{
    public Transform[] samplePoints;  // positions along your keel foil
    public float keelArea = 2f;
    public float liftSlope = 2f * Mathf.PI;
    public float dragCoeff = 0.1f;

    Rigidbody rb;
    WaterSurfaceRequestArray req;
    Vector3[] worldPts;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        req = new WaterSurfaceRequestArray();
        worldPts = new Vector3[samplePoints.Length];
    }

    void FixedUpdate()
    {
        // 1) world-space sample
        for (int i = 0; i < samplePoints.Length; i++)
            worldPts[i] = samplePoints[i].position;

        // 2) sample water (reuse your buoy mesh’s KWS pass)
        req.SetNewPositions(worldPts);
        WaterSystem.TryGetWaterSurfaceData(req);
        if (!req.IsDataReady) return;
        var data = req.Result;

        // 3) heel righting lift + drag
        Vector3 keelDir = transform.forward;
        for (int i = 0; i < data.Length; i++)
        {
            // relative flow
            Vector3 Vrel = rb.GetPointVelocity(worldPts[i]) - data[i].Velocity;
            Vector3 lat = Vector3.ProjectOnPlane(Vrel, keelDir);
            float v2 = lat.sqrMagnitude;
            if (v2 < 0.0001f) continue;

            // angle of attack (small-angle)
            float alpha = Vector3.SignedAngle(lat, keelDir, Vector3.up) * Mathf.Deg2Rad;
            float liftM = 0.5f * FloaterConstants.RhoWater * v2 * keelArea * (liftSlope * alpha);
            Vector3 lift = Vector3.Cross(Vector3.up, lat).normalized * liftM;

            float dragM = 0.5f * FloaterConstants.RhoWater * v2 * keelArea * dragCoeff;
            Vector3 drag = -lat.normalized * dragM;

            rb.AddForceAtPosition(lift + drag, worldPts[i]);
        }
    }
}
