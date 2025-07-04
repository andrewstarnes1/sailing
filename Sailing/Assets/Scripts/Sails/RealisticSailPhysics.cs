using System.Collections.Generic;

using Unity.Android.Gradle.Manifest;

using UnityEditor;

using UnityEngine;



[RequireComponent(typeof(MeshFilter))]

public class RealisticSailPhysics : MonoBehaviour

{    // A tiny struct to hold each luff anchor

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
    [Header("Aero Settings")]
    [Range(0, 90)] public float stallAngle = 45f;
    [Range(0.1f, 10f)] public float windScale = 2f;
    public Transform head, tack, clew;
    [Header("Sheet Trim")]
    // internally store the _initial_ rest lengths
    private float _portInitialRest, _starInitialRest;
    [Header("Sheets")]
    public Transform portSheetAttachment, starboardSheetAttachment;
    /// <summary>
    /// From 0 to 180, between 0 and 25 no power (in irons), 25 and 60 (close haul go from 1% power to 60% power), from 60 to 120 power go from 60% to 100% to 70% power  (beam reach), from 120 to 180 degrees (broad reach into run) we go from 70% down to 40% power
    /// </summary>
    [SerializeField] AnimationCurve sailPowerAtPointOfSail2 = new AnimationCurve(
    new Keyframe(0f / 180f, 0f),      // 0°
    new Keyframe(25f / 180f, 0.01f),  // 25°
    new Keyframe(60f / 180f, 0.6f),   // 60°
    new Keyframe(90f / 180f, 1.0f),   // 90°
    new Keyframe(120f / 180f, 0.7f),  // 120°
    new Keyframe(180f / 180f, 0.4f)   // 180°
);


    /// <summary>
    /// For Sheet Trim, Sheet Stiffness, we want this to be 100% in close haul, 50% in beam reach, around 20% tight on a run (so it maintains its position on its side and we dont do a crash gybe)
    /// </summary>
    [SerializeField] AnimationCurve sailPowerAtSheetSetting1 = new AnimationCurve(
    new Keyframe(0f / 180f, 1.0f),     // 0° (tight is best in irons/close-haul)
    new Keyframe(60f / 180f, 1.0f),    // 60°
    new Keyframe(90f / 180f, 0.5f),    // 90°
    new Keyframe(150f / 180f, 0.2f),   // 150°
    new Keyframe(180f / 180f, 0.2f)    // 180°
);
    [Tooltip("0 = fully slack, 1 = fully trimmed in")]
    [Range(0f, 1f)]
    public float portSheetTrim = 0f;


    // In Game we wont touch this, always leave it to maximum and only let user adjust portSheetTrim
    [Tooltip("Spring stiffness for each sheet (higher = tighter)")]
    [Range(0f, 2000f)]
    public float portSheetStiffness = 20f;

    [Range(0f, 1f)]
    public float starboardSheetTrim = 0f;
    [Tooltip("Spring stiffness for each sheet (higher = tighter)")]

    // In Game we wont touch this, always leave it to maximum and only let user adjust starboardSheetTrim
    [Range(0f, 2000f)]
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

    public float luffStiffness = 1.0f;   // very stiff on the luff





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
        GenerateMesh();  // setup mesh triangles once only



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





    // -------------- in RealisticSailPhysics.cs --------------
    // you’ll need to store each spring’s restLength when you create it:
    // public class SailSpring { public float restLength, stiffness; ... }



    public float ComputeTotalSpringTension()
    {
        float sum = 0f;
        foreach (var s in springs)
        {
            // skip completely fixed or super-rigid luff springs if you like
            float current = Vector3.Distance(s.nodeA.position, s.nodeB.position);
            float ext = current - s.restLength;
            sum += Mathf.Abs(s.stiffness * ext);
        }
        return sum;
    }





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
            int cols = Mathf.Max(1, bottomCount - y);     // 13,12,11...1
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

                        stiffness = luffStiffness * 0.2f  // a gentle bend-stiffness

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

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(
            this.head.transform.position + Vector3.up * 1f,
            $"{this._lastSailPower:F3} Sail Power - Sheet Effect {_lastSheetEffect:F3}"
        );


        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(boatBody.worldCenterOfMass, 0.2f);
        Gizmos.color = Color.red;

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
    }// Add this field to your class:
    [Header("Forward-Drive Settings")]
    [Tooltip("Efficiency vs. apparent wind angle (0…1), peaks at beam reach")]
    public AnimationCurve sailPolarCurve = AnimationCurve.EaseInOut(0f, 0f, 0.5f, 1f);
    [Tooltip("Hull drag coefficient for D = C·v²")]
    public float hullDragCoefficient = 0.5f;
    void UpdatePhysics()
    {
        // 1) NODE-LEVEL AERO + GRAVITY + SHEET + VERLET (unchanged)
        Vector3 windWorld = GlobalWind.Instance.GetWindForceAtPosition(tr.position);
        float ws = windWorld.magnitude * windScale;
        Vector3 W = tr.InverseTransformDirection(windWorld.normalized);
        float areaPerNode = totalArea / nodes.Count;
        float halfV2 = 0.5f * ws * ws;
        float liftC = halfV2 * areaPerNode;
        float dragC = halfV2 * 0.1f * areaPerNode;
        // We’ll accumulate forward‐component from each node here
        Vector3 forwardAxis = boatBody.transform.forward;
        float F_sum = 0f;
        var normals = sailMesh.normals;
        for (int i = 0; i < nodes.Count; i++)
        {
            SailNode n = nodes[i];
            Vector3 norm = normals[i];
            Vector3 chord = Vector3.Cross(Vector3.up, norm).normalized;
            float cosA = Mathf.Clamp(Vector3.Dot(chord, W), -1f, 1f);
            float aoa = Mathf.Acos(Mathf.Abs(cosA));
            float Cl = (aoa * Mathf.Rad2Deg < stallAngle)
                          ? Mathf.Sin(2f * aoa)
                          : 0f;
            float sign = -Mathf.Sign(Vector3.Dot(W, norm));
            Vector3 liftLocal = sign * liftC * Cl * Vector3.Cross(chord, W).normalized;
            Vector3 dragLocal = dragC * W;
            Vector3 pressureLocal = halfV2 * areaPerNode * Vector3.Dot(norm, W) * norm;

            Vector3 F_local = liftLocal + dragLocal + pressureLocal;
            n.ApplyExternalForce(F_local);
            n.ApplyExternalForce(Vector3.down * gravityStrength * Mathf.Lerp(1f, 0.2f, uCoords[i]));

            // record forward‐component of *world* force
            Vector3 F_world = tr.TransformDirection(F_local);
            F_sum += Vector3.Dot(F_world, forwardAxis);
        }

        // sheets & Verlet
        float portPreload = _portInitialRest * preloadFraction;
        float starPreload = _starInitialRest * preloadFraction;
        float portRest = Mathf.Lerp(_portInitialRest + portPreload, -portPreload, portSheetTrim);
        float starRest = Mathf.Lerp(_starInitialRest + starPreload, -starPreload, starboardSheetTrim);

        ApplySheetForce(nodes[_clewNodeIndex], portAttachL, portRest, portSheetStiffness);
        ApplySheetForce(nodes[_clewNodeIndex], starAttachL, starRest, starboardSheetStiffness);

        foreach (var n in nodes)
            n.UpdatePosition(Time.fixedDeltaTime);

        // 2) POINT-OF-SAIL EFFICIENCY
        Vector3 trueWind = GlobalWind.Instance.GetWindForceAtPosition(tr.position);
        Vector3 appWind = trueWind - boatBody.linearVelocity;

        _lastSailPower = CalculateSailForwardPower(appWind);

        boatBody.AddForce(boatBody.transform.forward * _lastSailPower*maximumSailPower*GlobalWind.Instance.windStrength);
        sailMesh.RecalculateBounds();
    }
    [SerializeField] float maximumSailPower = 10f; 
    /// <summary>
    /// Returns the forward-driving force multiplier [0, 1].
    /// </summary> 
    float CalculateSailForwardPower(Vector3 apparentWind)
    {
        // 1. Calculate apparent wind angle relative to boat forward
        Vector3 boatForward = boatBody.transform.forward;
        float awa = Vector3.SignedAngle(boatForward, -apparentWind, Vector3.up); // CORRECT
        float absAWA = Mathf.Abs(awa);

        // 2. Point of sail power (curve: 0–180 deg)
        float pointOfSailNorm = Mathf.InverseLerp(0f, 180f, absAWA);
        float basePower = sailPowerAtPointOfSail2.Evaluate(pointOfSailNorm);

        // 3. Decide which sheet is controlling the sail
        bool windFromPort = awa > 0f; // Wind is coming over port side
        float sheetTrim = windFromPort ? portSheetTrim : starboardSheetTrim;
        float sheetStiffness = windFromPort ? portSheetStiffness : starboardSheetStiffness;

        float blendZoneWidth = 20f; // degrees from 180 where we blend
        float awaAbs = Mathf.Abs(awa);
        if (awaAbs > 180f - blendZoneWidth)
        {
            // Calculate blend factor: 0 at 180°, 1 at 160° or -160°
            float blend = Mathf.InverseLerp(180f, 180f - blendZoneWidth, awaAbs);
            // Blend both trims (could also use Mathf.LerpUnclamped for values >180)
            sheetTrim = Mathf.Lerp(
                Mathf.Max(portSheetTrim, starboardSheetTrim), // Both active at dead run
                (awa > 0f) ? portSheetTrim : starboardSheetTrim,
                blend
            );
            sheetStiffness = Mathf.Lerp(
                Mathf.Max(portSheetStiffness, starboardSheetStiffness),
                (awa > 0f) ? portSheetStiffness : starboardSheetStiffness,
                blend
            );
        }
        else
        {
            // Standard case: port/starboard logic
            sheetTrim = (awa > 0f) ? portSheetTrim : starboardSheetTrim;
            sheetStiffness = (awa > 0f) ? portSheetStiffness : starboardSheetStiffness;
        }


        // This gives us where we want our sheet to be currently trimmed to, can depower our sail by up to 50% if set incorrectly. 
        // For example if sheetEffect = 0.2 but our current sheetTrim = 1, then output power will be 50% what it should be, similarily if sheetEffect = 1 and our sheetEffect = 0, then output Power will be 50% of max


        // sheetEffect is the *optimal* sheet trim for this point of sail, 0=let out, 1=close hauled
        float sheetEffect = sailPowerAtSheetSetting1.Evaluate(pointOfSailNorm);

        // Penalize mismatch: if current trim is far from optimal, lose power
        float trimPenalty = 1f - Mathf.Abs(sheetTrim - sheetEffect); // 1 when perfect, 0 when completely wrong

        // Now compute final power (could multiply with your other terms as needed)
        float power = basePower * Mathf.Clamp01(trimPenalty);

        _lastSheetEffect = trimPenalty;
        // 5. Combine: base power × (sheet trim × sheet effect)
        // Optionally modulate with sheet stiffness if you want
        // power *= Mathf.InverseLerp(minStiffness, maxStiffness, sheetStiffness);

        return power;
    }

    private float _lastSheetEffect = 0f;

    private float _lastSailPower = 0f;

    [SerializeField] float maxSailPower = 1000f;
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