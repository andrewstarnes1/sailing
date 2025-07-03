using System.Collections.Generic;
using UnityEngine;
using KWS;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(Rigidbody))]
public class BoatBuoyancy : MonoBehaviour
{
    private const float WATER_DENSITY = 1000f; // kg/m3
    private const float DAMPER = 0.05f;

    Vector3 _lastKeelTorque;
    [SerializeField] float KeelGizmoScale = 0.2f;
    [Header("Center-Line Buoyancy Nodes")]
    [Tooltip("Local points along the centre-line of the hull.")]
    public Vector3[] BuoyancyNodes = new Vector3[0];

    [Header("Port-Side Buoyancy Nodes")]
    [Tooltip("Local points on the port side; starboard will mirror automatically.")]
    public Vector3[] PortLineNodes = new Vector3[0];

    [Header("Physics Settings")]
    [Tooltip("Material density for buoyancy calculation (kg/m3).")]
    public float MaterialDensity = 500f;

    [Tooltip("Linear drag when submerged.")]
    public float WaterDrag = 1f;
    [Tooltip("Linear drag when out of water.")]
    public float AirDrag = 0.1f;

    [Tooltip("Strength of velocity-based damping (higher = more wave resistance).")]
    public float VelocityForce = 10f;

    private Rigidbody _rb;
    private WaterSurfaceRequestArray _waterRequest;
    private Vector3[] _worldNodePositions;

    [Header("Keel (Roll Stabilization)")]
    [Tooltip("Nm per radian of roll angle")]
    public float KeelStiffness = 500f;
    [Tooltip("Nm per radian/sec of roll rate")]
    public float KeelDamping = 50f;

    // New fields on your BoatBuoyancy (or RealisticSailPhysics) script:
    [Header("Yaw Stabilisation")]
    public float YawStiffness = 50f;   // Nm per radian of heading error
    public float YawDamping = 200f;   // Nm per rad/sec of yaw rate

    // you'll need new public floats:
    public float PitchStiffness = 300f;   // Nm/rad
    public float PitchDamping = 30f;    // Nm/(rad/s)


    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _waterRequest = new WaterSurfaceRequestArray();
        _rb.centerOfMass = new Vector3(0f, -1.2f, -0.2f);

    }

    void FixedUpdate()
    {



        // 1) combine centre, port, and mirrored starboard
        var localNodes = new List<Vector3>();
        localNodes.AddRange(BuoyancyNodes);
        localNodes.AddRange(PortLineNodes);
        foreach (var p in PortLineNodes)
            localNodes.Add(new Vector3(-p.x, p.y, p.z));

        int n = localNodes.Count;
        if (n == 0)
            return;

        // 2) world-space positions
        if (_worldNodePositions == null || _worldNodePositions.Length != n)
            _worldNodePositions = new Vector3[n];
        for (int i = 0; i < n; i++)
            _worldNodePositions[i] = transform.TransformPoint(localNodes[i]);

        // 3) query water
        _waterRequest.SetNewPositions(_worldNodePositions);
        WaterSystem.TryGetWaterSurfaceData(_waterRequest);
        if (!_waterRequest.IsDataReady)
        {
            _rb.Sleep();
            return;
        }
        _rb.WakeUp();

        // 4) apply forces
        int submergedCount = 0;
        float totalArchForce = WATER_DENSITY * Physics.gravity.magnitude * (_rb.mass / MaterialDensity);
        Vector3 archPerNode = Vector3.up * (totalArchForce / n);

        for (int i = 0; i < n; i++)
        {
            Vector3 wp = _worldNodePositions[i];
            var surface = _waterRequest.Result[i];
            float depth = surface.Position.y - wp.y;
            if (depth <= 0f)
                continue;

            submergedCount++;
            float k = Mathf.Clamp01(depth / 1f);
            Vector3 buoyancy = archPerNode * Mathf.Sqrt(k);

            Vector3 localVel = _rb.GetPointVelocity(wp);
            Vector3 waterVel = surface.Velocity;
            // wave-relative damping
            Vector3 velDiff = localVel - waterVel * (VelocityForce / 10f);
            Vector3 damping = -velDiff * DAMPER * _rb.mass;

            _rb.AddForceAtPosition(buoyancy + damping, wp);
        }

        // 5) blend drag by submersion fraction
        float subFrac = submergedCount / (float)n;
        _rb.linearDamping = Mathf.Lerp(AirDrag, WaterDrag, subFrac);


        // ——— KEEL STABILISATION ———
        // 1) how far is the boat tilted around its forward axis?
        //    (signed so port vs starboard list gives opposite sign)
        float rollDeg = Vector3.SignedAngle(transform.up, Vector3.up, transform.forward);
        float rollRad = rollDeg * Mathf.Deg2Rad;

        // 2) how fast is it rolling?
        float rollRate = Vector3.Dot(_rb.angularVelocity, transform.forward);

        // 3) torque = –(stiffness*angle + damping*rate) about forward axis
        Vector3 keelTorque = transform.forward * (-KeelStiffness * rollRad
                                                  - KeelDamping * rollRate);


        _lastKeelTorque = keelTorque;
        _rb.AddTorque(keelTorque);


        // ——— PITCH STABILISATION ———
        // signed angle between hull's forward and the horizontal plane
        float pitchDeg = Vector3.SignedAngle(transform.forward,
                                             Vector3.ProjectOnPlane(transform.forward, Vector3.up),
                                             transform.right);
        float pitchRad = pitchDeg * Mathf.Deg2Rad;
        // rate of pitch (positive nose-up)
        float pitchRate = Vector3.Dot(_rb.angularVelocity, transform.right);

        Vector3 pitchTorque = transform.right
                            * (-PitchStiffness * pitchRad
                                - PitchDamping * pitchRate);

        _rb.AddTorque(pitchTorque);

        // --- YAW STABILISATION ---
        // This simulates the keel's natural resistance to turning, making the boat track straighter.
        float yawRate = Vector3.Dot(_rb.angularVelocity, transform.up); // How fast the boat is turning
        Vector3 yawDampingTorque = transform.up * (-YawDamping * yawRate);
        _rb.AddTorque(yawDampingTorque, ForceMode.Force);

        // --- HYDRODYNAMIC YAW RESTORING TORQUE ---
        // Aligns the boat's forward direction with its velocity direction (in the horizontal plane)
        Vector3 velocity = _rb.linearVelocity;
        velocity.y = 0f; // Ignore vertical component
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (velocity.sqrMagnitude > 0.1f) {
            float angle = Vector3.SignedAngle(forward, velocity, Vector3.up);
            float yawStiffness = 2000f; // Tune this value as needed
            Vector3 restoringTorque = Vector3.up * (-angle * Mathf.Deg2Rad * yawStiffness);
            _rb.AddTorque(restoringTorque, ForceMode.Force);
        }
    }

    void OnDrawGizmosSelected()
    {
        // draw centre nodes
        if (BuoyancyNodes != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var p in BuoyancyNodes)
                Gizmos.DrawSphere(transform.TransformPoint(p), 0.1f);
        }

        // draw port nodes
        if (PortLineNodes != null)
        {
            Gizmos.color = Color.green;
            foreach (var p in PortLineNodes)
                Gizmos.DrawCube(transform.TransformPoint(p), Vector3.one * 0.1f);
        }

        // draw starboard (mirrored)
        if (PortLineNodes != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var p in PortLineNodes)
            {
                var mp = new Vector3(-p.x, p.y, p.z);
                Gizmos.DrawCube(transform.TransformPoint(mp), Vector3.one * 0.1f);
            }
        }
        Gizmos.color = Color.magenta;
        Vector3 com = transform.position;
        float lever = 1.0f;                // 1m half-beam for visualization
        float moment = Vector3.Dot(_lastKeelTorque, transform.forward);

        // compute the two forces F such that F * (2*lever) = moment
        float F = moment / (2f * lever);

        Vector3 portPoint = com + transform.right * lever;
        Vector3 starPoint = com - transform.right * lever;
        Vector3 upF = transform.up * (F * KeelGizmoScale);
        Vector3 downF = -upF;

        // upward force on port, downward on starboard
        Gizmos.DrawLine(portPoint, portPoint + upF);
        Gizmos.DrawLine(starPoint, starPoint + downF);

    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BoatBuoyancy))]
public class BoatBuoyancyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var script = (BoatBuoyancy)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Node Tools", EditorStyles.boldLabel);

        // Center-line controls
        if (GUILayout.Button("Add Center Node"))
        {
            Undo.RecordObject(script, "Add Center Node");
            var list = new List<Vector3>(script.BuoyancyNodes) { Vector3.zero };
            script.BuoyancyNodes = list.ToArray();
            EditorUtility.SetDirty(script);
        }
        if (GUILayout.Button("Clear Center Nodes"))
        {
            Undo.RecordObject(script, "Clear Center Nodes");
            script.BuoyancyNodes = new Vector3[0];
            EditorUtility.SetDirty(script);
        }

        // Port-side controls
        if (GUILayout.Button("Add Port Node"))
        {
            Undo.RecordObject(script, "Add Port Node");
            var list = new List<Vector3>(script.PortLineNodes) { Vector3.zero };
            script.PortLineNodes = list.ToArray();
            EditorUtility.SetDirty(script);
        }
        if (GUILayout.Button("Clear Port Nodes"))
        {
            Undo.RecordObject(script, "Clear Port Nodes");
            script.PortLineNodes = new Vector3[0];
            EditorUtility.SetDirty(script);
        }
    }

    private void OnSceneGUI()
    {
        var script = (BoatBuoyancy)target;
        var t = script.transform;

        // editable center nodes
        Handles.color = Color.cyan;
        EditNodeArray(ref script.BuoyancyNodes, t, 0.1f, script);

        // editable port nodes
        Handles.color = Color.green;
        EditNodeArray(ref script.PortLineNodes, t, 0.1f, script);

        // starboard: just draw, no handles
        Handles.color = Color.yellow;
        if (script.PortLineNodes != null)
        {
            foreach (var p in script.PortLineNodes)
            {
                Vector3 world = t.TransformPoint(new Vector3(-p.x, p.y, p.z));
                Handles.DrawSolidDisc(world, Vector3.up, HandleUtility.GetHandleSize(world) * 0.05f);
            }
        }

        // shift+click to add to currently highlighted array
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.shift && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject == script.gameObject)
            {
                Undo.RecordObject(script, "Add Buoyancy Node");
                var localPos = t.InverseTransformPoint(hit.point);

                // decide which list to add to by last GUI focus
                // if port list foldout open use that, else center
                script.BuoyancyNodes = AppendToArray(script.BuoyancyNodes, localPos);
                // you can change this logic to add to PortLineNodes if desired

                EditorUtility.SetDirty(script);
                e.Use();
            }
        }
    }

    // helper to handle move-handles for any Vector3[] array
    void EditNodeArray(ref Vector3[] arr, Transform t, float sizeFactor, BoatBuoyancy script)
    {
        if (arr == null) return;
        var list = new List<Vector3>(arr);
        for (int i = 0; i < list.Count; i++)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 world = t.TransformPoint(list[i]);
            float handleSize = HandleUtility.GetHandleSize(world) * sizeFactor;
            var fmh_235_59_638869475215971078 = Quaternion.identity; Vector3 moved = Handles.FreeMoveHandle(world, handleSize, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(script, "Move Buoyancy Node");
                list[i] = t.InverseTransformPoint(moved);
                arr = list.ToArray();
                EditorUtility.SetDirty(script);
            }
        }
    }

    Vector3[] AppendToArray(Vector3[] arr, Vector3 v)
    {
        var l = new List<Vector3>(arr) { v };
        return l.ToArray();
    }
}
#endif
