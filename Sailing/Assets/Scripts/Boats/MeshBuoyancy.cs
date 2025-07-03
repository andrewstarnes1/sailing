using KWS;                    // for WaterSystem.TryGetWaterSurfaceData
using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Pool;       // for ListPool

[RequireComponent(typeof(Rigidbody), typeof(MeshFilter))]
public class MeshBuoyancy : MonoBehaviour
{
    [Header("Buoyancy Settings")]
    public float waterDensity = 1025f;  // kg/m³
    public float angularDamping = 1f;
    public float smoothForceTime = 0.1f;

    public float rightingStiffness = 10f;
    public float rightingDamping = 2f;

    MeshFilter mf;
    Rigidbody rb;
    WaterSurfaceRequestArray request;
    Vector3[] submittedVerts;  // world‐space hull verts we send
    SurfaceData[] sampleData;      // copy of request.Result
    Vector3 smoothedForce;
    float totalVol;        // store for gizmos
    Vector3 centerOfBuoyancy;// store for gizmos

    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        rb = GetComponent<Rigidbody>();
        request = new WaterSurfaceRequestArray();

        // center of mass down low so boat floats right
        rb.centerOfMass = new Vector3(0f, -1f, 0f);
    }
    void FixedUpdate()
    {
        // 1) Grab helper-mesh verts & tris
        var mesh = mf.sharedMesh;
        var localVerts = mesh.vertices;
        var tris = mesh.triangles;
        int V = localVerts.Length;
        if (V < 4) return;               // need at least a few verts

        // 2) World-space positions
        if (submittedVerts == null || submittedVerts.Length != V)
            submittedVerts = new Vector3[V];
        for (int i = 0; i < V; i++)
            submittedVerts[i] = transform.TransformPoint(localVerts[i]);

        // 3) Ask KWS for water heights & velocities
        request.SetNewPositions(submittedVerts);
        WaterSystem.TryGetWaterSurfaceData(request);
        if (!request.IsDataReady)
        {
            rb.isKinematic = true;   // freeze until data arrives
            return;
        }
        rb.isKinematic = false;

        // 4) Copy back into sampleData[]
        if (sampleData == null || sampleData.Length != V)
            sampleData = new SurfaceData[V];
        request.Result.CopyTo(sampleData, 0);

        // 5) Compute volume, COB & simple drag
        totalVol = 0f;
        Vector3 weightedSum = Vector3.zero;
        Vector3 dragSum = Vector3.zero;
        float g = Physics.gravity.magnitude;
        float dragC = 1f;       // tweak to taste

        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t + 0], i1 = tris[t + 1], i2 = tris[t + 2];
            Vector3 H0 = submittedVerts[i0],
                    H1 = submittedVerts[i1],
                    H2 = submittedVerts[i2];

            // only pull Y from the water sample
            float w0 = sampleData[i0].Position.y;
            float w1 = sampleData[i1].Position.y;
            float w2 = sampleData[i2].Position.y;
            Vector3 W0 = new Vector3(H0.x, w0, H0.z),
                    W1 = new Vector3(H1.x, w1, H1.z),
                    W2 = new Vector3(H2.x, w2, H2.z);

            float d0 = W0.y - H0.y,
                  d1 = W1.y - H1.y,
                  d2 = W2.y - H2.y;

            // build alternating hull / water loop
            var edgeH = ListPool<Vector3>.Get();
            var edgeW = ListPool<Vector3>.Get();
            ClipEdge(H0, W0, d0, H1, W1, d1, edgeH, edgeW);
            ClipEdge(H1, W1, d1, H2, W2, d2, edgeH, edgeW);
            ClipEdge(H2, W2, d2, H0, W0, d0, edgeH, edgeW);

            int M = edgeH.Count;

            if (M == 3)
            {
                // fully-submerged triangle → prismatic approx
                float triArea = Vector3.Cross(H1 - H0, H2 - H0).magnitude * 0.5f;
                float hAvg = (d0 + d1 + d2) / 3f;
                float triVol = triArea * hAvg;

                totalVol += triVol;

                var cenH = (H0 + H1 + H2) / 3f;
                var cenW = (W0 + W1 + W2) / 3f;
                var cen = (cenH + cenW) * 0.5f;
                weightedSum += cen * triVol;

                // optional drag on fully submerged tris:
                float perVert = triArea / 3f;
                foreach (int vi in new[] { i0, i1, i2 })
                {
                    var rel = rb.GetPointVelocity(submittedVerts[vi])
                              - sampleData[vi].Velocity;
                    dragSum += -rel * dragC * perVert;
                }
            }
            else if (M >= 4)
            {
                // your two-tets-per-quad approach
                for (int m = 0; m < M; m += 2)
                {
                    int n = (m + 2) % M;
                    var Hm = edgeH[m];
                    var Hn = edgeH[n];
                    var Wm = edgeW[m];
                    var Wn = edgeW[n];

                    float v1 = Mathf.Abs(SignedTetraVolume(Hm, Hn, Wm, Vector3.zero));
                    float v2 = Mathf.Abs(SignedTetraVolume(Wm, Hn, Wn, Vector3.zero));

                    totalVol += v1 + v2;

                    var c1 = (Hm + Hn + Wm) / 3f;
                    var c2 = (Wm + Hn + Wn) / 3f;
                    weightedSum += c1 * v1 + c2 * v2;
                }

                // simple tri-based drag
                float triArea = Vector3.Cross(H1 - H0, H2 - H0).magnitude * 0.5f;
                float perVert = triArea / 3f;
                foreach (int vi in new[] { i0, i1, i2 })
                {
                    var rel = rb.GetPointVelocity(submittedVerts[vi])
                              - sampleData[vi].Velocity;
                    dragSum += -rel * dragC * perVert;
                }
            }

            ListPool<Vector3>.Release(edgeH);
            ListPool<Vector3>.Release(edgeW);
        }

        // 6) apply viscous drag so you don’t “slide” through the water
        rb.AddForce(dragSum, ForceMode.Force);

        // nothing submerged → bail
        if (totalVol <= Mathf.Epsilon) return;

        // 7) finalize COB
        centerOfBuoyancy = weightedSum / totalVol;

        // 8) buoyant force (smoothed)
        Vector3 rawF = Vector3.up * (waterDensity * totalVol * g);
        float alpha = Mathf.Clamp01(Time.fixedDeltaTime / smoothForceTime);
        smoothedForce = Vector3.Lerp(smoothedForce, rawF, alpha);
        rb.AddForceAtPosition(smoothedForce, centerOfBuoyancy, ForceMode.Force);

        // 9) (optional) angular‐damping and righting torque…
        rb.AddTorque(-rb.angularVelocity * angularDamping, ForceMode.Acceleration);
    }

    // ---- helpers ----
    void ClipEdge(Vector3 Hi, Vector3 Wi, float di,
                  Vector3 Hj, Vector3 Wj, float dj,
                  List<Vector3> edgeH, List<Vector3> edgeW)
    {
        if (di > 0f) { edgeH.Add(Hi); edgeW.Add(Wi); }
        if ((di > 0f) != (dj > 0f))
        {
            float f = di / (di - dj);
            edgeH.Add(Vector3.Lerp(Hi, Hj, f));
            edgeW.Add(Vector3.Lerp(Wi, Wj, f));
        }
    }

    float SignedTetraVolume(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        return Vector3.Dot(a - d, Vector3.Cross(b - d, c - d)) / 6f;
    }

    void OnDrawGizmosSelected()
    {
        // nothing to draw until we have a synced sample
        if (submittedVerts == null || sampleData == null) return;
        if (submittedVerts.Length != sampleData.Length) return;

        // 1) per‐vertex depth lines
        for (int i = 0; i < submittedVerts.Length; i++)
        {
            Vector3 H = submittedVerts[i];
            float wy = sampleData[i].Position.y;
            Vector3 W = new Vector3(H.x, wy, H.z);
            float depth = wy - H.y;

            Gizmos.color = depth > 0f ? Color.green : Color.magenta;
            Gizmos.DrawLine(H, W);
            Gizmos.DrawSphere(H, 0.02f);

#if UNITY_EDITOR
            Handles.Label(H + Vector3.up * 0.05f, depth.ToString("F2"));
#endif
        }

        // 2) draw COM and COB
        Vector3 comWS = transform.TransformPoint(rb.centerOfMass);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(comWS, 0.05f);
#if UNITY_EDITOR
        Handles.Label(comWS + Vector3.up * 0.1f, "COM");
#endif

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(centerOfBuoyancy, 0.05f);
#if UNITY_EDITOR
        Handles.Label(centerOfBuoyancy + Vector3.up * 0.1f,
                      $"COB\nVol={totalVol:F2}");
#endif

        // 3) draw buoyant force
        Vector3 F = smoothedForce;
        if (F.sqrMagnitude > 1e-4f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(centerOfBuoyancy,
                            centerOfBuoyancy + F.normalized * 0.5f);
#if UNITY_EDITOR
            Handles.ArrowHandleCap(
              0,
              centerOfBuoyancy + F.normalized * 0.5f,
              Quaternion.LookRotation(F),
              0.2f,
              EventType.Repaint
            );
            Handles.Label(
              centerOfBuoyancy + F.normalized * 0.6f,
              $"{F.magnitude:F0} N"
            );
#endif
        }
    }

}
