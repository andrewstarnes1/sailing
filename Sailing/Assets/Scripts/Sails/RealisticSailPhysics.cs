using System.Collections.Generic;
using Unity.Android.Gradle.Manifest;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class RealisticSailPhysics : MonoBehaviour
{    // A tiny struct to hold each luff anchor
    private struct LuffAnchor
    {
        public SailNode Node;
        public Vector3 Home;

        public LuffAnchor(SailNode node, Vector3 home)
        {
            Node = node;
            Home = home;
        }
    }
    [Header("Boat")]
    public Rigidbody boatBody;
    // class‐level:
    private Vector3 _netAerodynamicForce;
    private Vector3 _centerOfPressure;

    [Header("Aero Settings")]
    [Range(0, 90)] public float stallAngle = 45f;
    [Range(0.1f, 10f)] public float windScale = 2f;
    public Transform head, tack, clew;
    [Header("Sheet Trim")]
    [Tooltip("0 = fully slack, 1 = fully trimmed in")]
    [Range(0f, 1f)]
    public float portSheetTrim = 0f;
    [Range(0f, 1f)]
    public float starboardSheetTrim = 0f;

    // internally store the _initial_ rest lengths
    private float _portInitialRest, _starInitialRest;
    [Header("Sheets")]
    public Transform portSheetAttachment, starboardSheetAttachment;


    [Tooltip("Spring stiffness for each sheet (higher = tighter)")]
    [Range(0f, 1000f)]
    public float portSheetStiffness = 20f;

    [Tooltip("Spring stiffness for each sheet (higher = tighter)")]
    [Range(0f, 1000f)]
    public float starboardSheetStiffness = 20f;

    [Tooltip("Damping for sheet motion")]
    public float sheetDamping = 2f;

    // (internal)
    private float _portRestLength, _starRestLength;
    private int _clewNodeIndex;

    [Range(5, 20)] public int horizontalSegments = 12;
    [Range(5, 20)] public int verticalSegments = 12;
    [Header("Spring Stiffness")]
    public float structuralStiffness = 0.2f;
    public float shearStiffness = 0.2f;
    public float luffStiffness = 1.0f;   // very stiff on the luff


    [Header("Leech Settings")]
    [Range(0f, 5f)]
    public float leechStiffness = 1f;


    [Header("Sheet Preload")]
    [Tooltip("What fraction of original sheet length is taken up under full trim")]
    [Range(0f, 0.5f)]
    public float preloadFraction = 0.1f;
    // --- new: minimum billow factor when sheeted ---
    [Range(0f, 1f)]
    public float minBillow = 0.2f;

    // internals
    private List<int> rowStart = new();
    private List<int> rowCount = new();
    private List<SailNode> nodes = new();
    private List<SailSpring> springs = new();
    private List<LuffAnchor> luffAnchors = new();
    private Mesh sailMesh;
    private List<float> uCoords = new();
    private float totalArea;
    private Vector3[] meshVertices;
    private Vector3 headL, tackL, clewL, portAttachL, starAttachL;
    private float[] gScales;
    private Transform tr;
    public Vector3 AerodynamicForce => _netAerodynamicForce;
public Vector3 AerodynamicForcePosition => _centerOfPressure;
    void Start()
    {

        headL = transform.InverseTransformPoint(head.position);
        tackL = transform.InverseTransformPoint(tack.position);
        clewL = transform.InverseTransformPoint(clew.position);
        portAttachL = transform.InverseTransformPoint(portSheetAttachment.position);
        starAttachL = transform.InverseTransformPoint(starboardSheetAttachment.position);
        tr = transform;

        GenerateNodes();
        GenerateSprings();
        GenerateMesh();  // setup mesh triangles once only

        // bottom row runs from rowStart[0] to rowStart[0]+rowCount[0]-1
        _clewNodeIndex = rowStart[0] + (rowCount[0] - 1);

        // initial distance to each sheet ring
        _portRestLength = Vector3.Distance(nodes[_clewNodeIndex].position,
                                                portSheetAttachment.position);
        _starRestLength = Vector3.Distance(nodes[_clewNodeIndex].position,
                                                starboardSheetAttachment.position);

        _portInitialRest = Vector3.Distance(nodes[_clewNodeIndex].position, portAttachL);
        _starInitialRest = Vector3.Distance(nodes[_clewNodeIndex].position, starAttachL);

        // ① compute the flat triangle area
        totalArea = ComputeTriangleArea(head.position,
                                        tack.position,
                                        clew.position);

        // build gravity‐scale lookup
        gScales = new float[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
            gScales[i] = Mathf.Lerp(1f, 0.2f, uCoords[i]);

    }
    /// <summary>
    /// Area of triangle ABC = ½ |(B–A)×(C–A)|
    /// </summary>
    private float ComputeTriangleArea(Vector3 A, Vector3 B, Vector3 C)
    {
        return Vector3.Cross(B - A, C - A).magnitude * 0.5f;
    }


    private List<SailSpring> leechSprings = new List<SailSpring>();
    void FixedUpdate()
    {

        // 0) recompute all your parametric stiffnesses
        var activeTrim = Mathf.Max(portSheetTrim, starboardSheetTrim);
        var railStiff = Mathf.Clamp01(leechStiffness * activeTrim);
        foreach (var ls in leechSprings)
            ls.stiffness = railStiff;

        // 1) accumulate external forces (wind + gravity + sheet) & Verlet integrate
        UpdatePhysics();

        // 2) now relax _all_ your springs N times
        float fPasses = Mathf.Lerp(5f, 20f, activeTrim);
        int basePass = Mathf.FloorToInt(fPasses);
        float extraFrac = fPasses - basePass;

        // do the “base” number of passes
        for (int i = 0; i < basePass; i++)
            foreach (var s in springs)
                s.ApplySpringConstraint();

        // maybe do one more pass
        if (Random.value < extraFrac)
            foreach (var s in springs)
                s.ApplySpringConstraint();

        // 3) finally push those new node positions into your mesh
        UpdateMeshVertices();




    }
    [Header("Physics")]
    public float gravityStrength = 9.81f;


    void GenerateNodes()
    {
        nodes.Clear();
        luffAnchors.Clear();
        rowStart.Clear();
        rowCount.Clear();
        // in GenerateNodes(), before your double‐loop:
        uCoords.Clear();
        int bottomCount = horizontalSegments + 1;

        // build each row, from y=0 (tack) to y=verticalSegments (head)
        for (int y = 0; y <= verticalSegments; y++)
        {
            rowStart.Add(nodes.Count);
            int cols = Mathf.Max(1, bottomCount - y);     // 13,12,11...1
            rowCount.Add(cols);

            float tY = y / (float)verticalSegments;
            Vector3 front = Vector3.Lerp(tack.position, head.position, tY);
            Vector3 back = Vector3.Lerp(clew.position, head.position, tY);

            for (int x = 0; x < cols; x++)
            {
                float tX = cols == 1 ? 0f : x / (float)(cols - 1);
                Vector3 pos = Vector3.Lerp(front, back, tX);

                // record it
                uCoords.Add(tX);
                // fix the entire luff edge (x==0)
                bool isFixed = (x == 0);

                Vector3 frontL = Vector3.Lerp(tackL, headL, tY);
                Vector3 backL = Vector3.Lerp(clewL, headL, tY);
                Vector3 posL = Vector3.Lerp(frontL, backL, tX);
                SailNode sn = new SailNode(posL, isFixed);
                nodes.Add(sn);


                if (x == 0)
                    luffAnchors.Add(new LuffAnchor(sn, posL));
            }
        }
    }

    void GenerateSprings()
    {
        springs.Clear();
        leechSprings.Clear();
        // for each row pair, connect:
        for (int y = 0; y < verticalSegments; y++)
        {
            int startA = rowStart[y], countA = rowCount[y];
            int startB = rowStart[y + 1], countB = rowCount[y + 1];

            // horizontal springs in row A
            for (int x = 0; x < countA - 1; x++)
                springs.Add(new SailSpring(nodes[startA + x],
                                           nodes[startA + x + 1]));

            // between rows (vertical + diagonal)
            for (int x = 0; x < countA; x++)
            {
                // map horizontal index into the smaller row above
                float normX = x / (float)(countA - 1);
                int xB = Mathf.RoundToInt(normX * (countB - 1));

                // vertical spring
                springs.Add(new SailSpring(nodes[startA + x],
                                           nodes[startB + xB]));

                // diagonal toward the next B node
                if (x < countA - 1)
                {
                    int xB2 = Mathf.RoundToInt((x + 1) / (float)(countA - 1) * (countB - 1));
                    springs.Add(new SailSpring(nodes[startA + x],
                                               nodes[startB + xB2]));
                    springs.Add(new SailSpring(nodes[startA + x + 1],
                                               nodes[startB + xB]));
                }
            }
        }
        // now build the leech rail
        for (int y = 0; y <= verticalSegments; y++)
        {
            int start = rowStart[y], count = rowCount[y];
            if (count < 2) continue;

            var A = nodes[start + count - 1];
            var B = (y < verticalSegments)
                    ? nodes[rowStart[y + 1] + rowCount[y + 1] - 1]
                    : default;

            // use scaled stiffness based on how much starboard is trimmed
            float railStiff = leechStiffness * starboardSheetTrim;

            SailSpring S1 = new SailSpring(A, nodes[start + count - 2], railStiff);
            leechSprings.Add(S1);
            // horizontal neighbor along the leech
            springs.Add(S1);
           
            // vertical link up the leech
            if (y < verticalSegments)
            {
                SailSpring S2 = new SailSpring(A, B, railStiff);
                leechSprings.Add(S2);
                springs.Add(S2);

            }
        }

        int cols = horizontalSegments + 1;
        for (int y = 0; y <= verticalSegments; y++)
        {
            for (int x = 0; x <= rowCount[y] - 1; x++)
            {
                int i = rowStart[y] + x;
                // horizontal bending: connect i to i+2
                if (x + 2 < rowCount[y])
                    springs.Add(new SailSpring(nodes[i], nodes[i + 2])
                    {
                        stiffness = luffStiffness * 0.2f  // a gentle bend-stiffness
                    });
                // vertical bending: connect i to node two rows up, same x-ratio
                if (y + 2 <= verticalSegments)
                {
                    int countA = rowCount[y], countB = rowCount[y + 2];
                    float normX = (countA == 1 ? 0f : x / (float)(countA - 1));
                    int j = rowStart[y + 2] + Mathf.RoundToInt(normX * (countB - 1));
                    springs.Add(new SailSpring(nodes[i], nodes[j])
                    {
                        stiffness = luffStiffness * 0.2f
                    });
                }
            }
        }

    }
    void OnDrawGizmos()
    {

        // (1) existing sail gizmos…
        if (nodes != null && nodes.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (var node in nodes)
                Gizmos.DrawSphere(this.transform.TransformPoint(node.position), 0.05f);

            Gizmos.color = Color.cyan;
            foreach (var s in springs)
                Gizmos.DrawLine(this.transform.TransformPoint(s.nodeA.position), this.transform.TransformPoint(s.nodeB.position));

            Gizmos.color = Color.red;
            foreach (var node in nodes)
                if (node.isFixed)
                    Gizmos.DrawSphere(this.transform.TransformPoint(node.position), 0.1f);
        }

        // (2) now draw your sheets:
        if (UnityEngine.Application.isPlaying)
        {
            // find your clew's world‐space position
            Vector3 clewPos = this.transform.TransformPoint(nodes[_clewNodeIndex].position);

            // port sheet
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(portSheetAttachment.position, 0.05f);
            Gizmos.DrawLine(clewPos, portSheetAttachment.position);

            // starboard sheet
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(starboardSheetAttachment.position, 0.05f);
            Gizmos.DrawLine(clewPos, starboardSheetAttachment.position);
        }

#if UNITY_EDITOR
        // (3) optional: draw little arrowheads using Handles
        if (UnityEngine.Application.isPlaying)
        {
            Handles.color = Color.green;
            DrawArrow(this.clew.position, portSheetAttachment.position, 0.1f);
            Handles.color = Color.magenta;
            DrawArrow(this.clew.position, starboardSheetAttachment.position, 0.1f);
        }
#endif

        // draw the center of pressure
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(_centerOfPressure, 0.05f);

        // draw the force vector
        float arrowScale = 0.1f; // tweak so it’s visible
        Gizmos.DrawLine(_centerOfPressure,
                        _centerOfPressure + _netAerodynamicForce * arrowScale);

#if UNITY_EDITOR
        // optional: label the magnitude
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(
            _centerOfPressure + Vector3.up * 0.1f,
            $"{_netAerodynamicForce.magnitude:F1} N"
        );
#endif

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(boatBody.worldCenterOfMass, 0.2f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(_centerOfPressure, 0.1f);

    }

#if UNITY_EDITOR
    // helper to draw an arrow from A→B
    void DrawArrow(Vector3 from, Vector3 to, float headSize)
    {
        Vector3 dir = (to - from).normalized;
        Handles.DrawAAPolyLine(from, to);
        Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        Handles.DrawAAPolyLine(to, to + right * headSize);
        Handles.DrawAAPolyLine(to, to + left * headSize);
    }
#endif
    void GenerateMesh()
    {
        sailMesh = new Mesh { name = "Triangular Sail Mesh" };
        GetComponent<MeshFilter>().mesh = sailMesh;

        // vertices
        meshVertices = new Vector3[nodes.Count];
        sailMesh.vertices = meshVertices;

        // triangles
        var tris = new List<int>();
        for (int y = 0; y < verticalSegments; y++)
        {
            int startA = rowStart[y], countA = rowCount[y];
            int startB = rowStart[y + 1], countB = rowCount[y + 1];

            for (int x = 0; x < countA - 1; x++)
            {
                int a = startA + x;
                int a2 = a + 1;

                float normX1 = x / (float)(countA - 1);
                float normX2 = (x + 1) / (float)(countA - 1);
                int b = startB + Mathf.RoundToInt(normX1 * (countB - 1));
                int b2 = startB + Mathf.RoundToInt(normX2 * (countB - 1));

                // build two triangles connecting rowA→rowB
                tris.Add(a); tris.Add(b); tris.Add(b2);
                tris.Add(a); tris.Add(b2); tris.Add(a2);
            }
        }

        sailMesh.triangles = tris.ToArray();
        sailMesh.RecalculateNormals();
        sailMesh.RecalculateBounds();
    }
    void UpdatePhysics()
    {
        // 0) reset
        _netAerodynamicForce = Vector3.zero;
        Vector3 weightedWorldPosSum = Vector3.zero;
        float totalForceMag = 0f;
        // 1) get wind in world, then into sail-local
        Vector3 windWorld = GlobalWind.Instance.GetWindForceAtPosition(tr.position);
        float windSpeed = windWorld.magnitude * windScale;
        Vector3 W_worldNorm = windWorld.normalized;
        float activeTrim = Mathf.Max(portSheetTrim, starboardSheetTrim);
        // convert wind direction *into* the sail’s local space:
        Vector3 W = tr.InverseTransformDirection(W_worldNorm);
        float areaPerNode = totalArea / nodes.Count;
        float halfV2 = 0.5f * windSpeed * windSpeed;
        float liftConst = halfV2 * areaPerNode;
        float dragConst = halfV2 * 0.1f * areaPerNode;

        var normals = sailMesh.normals;
        var upLocal = Vector3.up; // sail’s “up” in its own space

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var localNorm = normals[i];             // this is already in local space
            var chord = Vector3.Cross(upLocal, localNorm).normalized;

            // AoA via dot+acos in local space
            float dotCW = Mathf.Clamp(Vector3.Dot(chord, W), -1f, 1f);
            float aoaRad = Mathf.Acos(Mathf.Abs(dotCW));
            float aoaDeg = aoaRad * Mathf.Rad2Deg;
            float Cl = aoaDeg < stallAngle
                              ? Mathf.Sin(2f * aoaRad)
                              : 0f;

            // lift/drag/pressure computed in *local* coordinates
            float windSign = -Mathf.Sign(Vector3.Dot(W, localNorm));
            var liftDirLocal = Vector3.Cross(chord, W).normalized;
            Vector3 LiftLocal = windSign * liftConst * Cl * liftDirLocal;
            Vector3 DragLocal = dragConst * W;
            Vector3 PressureLocal = halfV2 * areaPerNode * Vector3.Dot(localNorm, W) * localNorm;

            Vector3 F_local = LiftLocal + DragLocal + PressureLocal;

            // apply in local as intended
            node.ApplyExternalForce(F_local);

            // to accumulate on hull we need world-space F & node position:
            Vector3 F_world = tr.TransformDirection(F_local);
            Vector3 worldPos = tr.TransformPoint(node.position);

            _netAerodynamicForce += F_world;

            float m = F_world.magnitude;
            weightedWorldPosSum += worldPos * m;
            totalForceMag += m;

            // flutter (local randomness is fine)
            if (Cl == 0f)
            {
                float noiseAmp = windSpeed * 0.02f * (1f - activeTrim);
                node.ApplyExternalForce(Random.onUnitSphere * noiseAmp);
            }

            // gravity still local
            node.ApplyExternalForce(Vector3.down * gravityStrength * gScales[i]);
        }

        // 4) sheet forces (all in *local* space now)
        float portPreload = _portInitialRest * preloadFraction;
        float starPreload = _starInitialRest * preloadFraction;
        float portRest = Mathf.Lerp(_portInitialRest + portPreload, -portPreload, portSheetTrim);
        float starRest = Mathf.Lerp(_starInitialRest + starPreload, -starPreload, starboardSheetTrim);

        ApplySheetForce(nodes[_clewNodeIndex], portAttachL, portRest, portSheetStiffness);
        ApplySheetForce(nodes[_clewNodeIndex], starAttachL, starRest, starboardSheetStiffness);

        // 5) Verlet integrate
        foreach (var n in nodes)
            n.UpdatePosition(Time.fixedDeltaTime);

        // 6) compute center-of-pressure in world space
        if (totalForceMag > 1e-4f)
            _centerOfPressure = weightedWorldPosSum / totalForceMag;
        else
            _centerOfPressure = tr.TransformPoint(clewL);

        // 7) Decompose the aerodynamic force into forward and lateral components
        Vector3 F_aero = _netAerodynamicForce;
        Vector3 forwardDir = boatBody.transform.forward;
        Vector3 F_forward = forwardDir * Vector3.Dot(F_aero, forwardDir);
        Vector3 F_lateral = F_aero - F_forward;

        // 8) Simulate hydrodynamic resistance to fight leeway (sideways drift)
        // This is a simplified model of the keel's function.
        float lateralDragCoefficient = 5f; // Tune this to control drift
        Vector3 hullVelocity = boatBody.linearVelocity;
        Vector3 velocityLateral = hullVelocity - (forwardDir * Vector3.Dot(hullVelocity, forwardDir));
        Vector3 F_hydro_lateral_resistance = -velocityLateral.normalized * velocityLateral.sqrMagnitude * lateralDragCoefficient;

        // 9) Calculate and Apply an ISOLATED Heeling Torque
        // The lateral force from the sail (F_lateral) creates a torque that can heel, pitch, and yaw the boat.
        // To prevent the unwanted pitching and yawing, we will isolate ONLY the heeling component of this torque.

        // First, calculate the full torque vector as before.
        Vector3 leverArm = _centerOfPressure - boatBody.worldCenterOfMass;
        Vector3 totalAerodynamicTorque = Vector3.Cross(leverArm, F_lateral);

        // Next, get the boat's forward direction. This is the axis we want to roll around.
        Vector3 boatForwardAxis = boatBody.transform.forward;

        // Now, project the total torque onto the boat's forward axis. This effectively filters out
        // any torque that would cause pitching or yawing, leaving only the heeling motion.
        Vector3 heelingTorque = Vector3.Project(totalAerodynamicTorque, boatForwardAxis);

        // Apply the isolated heeling torque.
     //   boatBody.AddTorque(heelingTorque, ForceMode.Force);


        // 10) Apply Forces for Propulsion and Leeway at the Center of Mass (This part remains the same)
        // Applying forces at the center of mass produces linear movement without adding extra torque.
   //     boatBody.AddForce(F_forward + F_lateral + F_hydro_lateral_resistance, ForceMode.Force);



        sailMesh.RecalculateBounds();
    }
    private void ApplySheetForce(
    SailNode node,
    Vector3 attachPos,
    float restLength,
    float stiffness
)
    {
        if (node.isFixed) return;

        // spring
        Vector3 delta = attachPos - node.position;
        float dist = delta.magnitude;
        Vector3 dir = dist > 1e-5f ? delta / dist : Vector3.zero;
        float extension = dist - restLength;
        Vector3 Fspring = dir * (extension * stiffness);

        // damping
        Vector3 velocity = (node.position - node.previousPosition) / Time.fixedDeltaTime;
        Vector3 Fdamper = -velocity * sheetDamping;
        node.ApplyExternalForce(Fspring + Fdamper);
    }

    void UpdateMeshVertices()
    {
        // The sole purpose of this function should be to update vertex positions.
        for (int i = 0; i < nodes.Count; i++)
            meshVertices[i] = nodes[i].position;

        sailMesh.vertices = meshVertices;
        sailMesh.RecalculateNormals(); // Keep this to update normals for the next physics frame
    }


}