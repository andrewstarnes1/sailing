#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System;
using System.Collections.Generic;
using static UnityEngine.Rendering.DebugUI.Table;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class CustomBuoyancyMesh : MonoBehaviour
{
    [Header("Sampling Lines (front→back)")]
    public Transform[] centerLine;      // N points along mid-body
    public Transform[] portBootLine;    // N points at the port boot-sole
    public Transform[] portDeckLine;    // N points at the port sheer-line

    [Header("Snapping")]
    [Tooltip("Hull collider used to snap sample points in edit mode")]
    public MeshCollider hullCollider;

    Mesh _mesh;
    MeshFilter _mf;

    void Reset()
    {
        // auto-assign collider if you drop this component on your hull
        hullCollider = GetComponent<MeshCollider>()
                    ?? GetComponentInChildren<MeshCollider>();
    }

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mesh = new Mesh { name = "BuoyancyHelperMesh" };
        _mf.sharedMesh = _mesh;
        GenerateMesh();
    }

    void OnValidate()
    {
        // keep all three arrays sorted front→back
        SortAlongZ(centerLine);
        SortAlongZ(portBootLine);
        SortAlongZ(portDeckLine);
        SnapAllLines();
        GenerateMesh();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    void SortAlongZ(Transform[] line)
    {
        if (line == null || line.Length < 2) return;
        Array.Sort(line, (a, b) =>
        {
            float za = transform.InverseTransformPoint(a.position).z;
            float zb = transform.InverseTransformPoint(b.position).z;
            return za.CompareTo(zb);
        });
    }

    [ContextMenu("Snap All Lines To Hull")]
    void SnapAllLines()
    {
#if UNITY_EDITOR
        if (hullCollider == null)
        {
            Debug.LogWarning("Assign a MeshCollider for snapping", this);
            return;
        }

        var all = new[] { centerLine, portBootLine, portDeckLine };
        float topY = hullCollider.bounds.min.y - 0.1f;

        foreach (var line in all)
            if (line != null)
                for (int i = 0; i < line.Length; i++)
                {
                    var t = line[i];
                    if (t == null) continue;
                    var ray = new Ray(
                        new Vector3(t.position.x, topY, t.position.z),
                        Vector3.up
                    );
                    if (hullCollider.Raycast(ray, out var hit, Mathf.Infinity))
                    {
                        Undo.RecordObject(t, "Snap Buoy");
                        t.position = new Vector3(
                            t.position.x,
                            hit.point.y,
                            t.position.z
                        );
                    }
                }
#endif
    }

    Vector3 SampleCurve(Transform[] line, float t)
    {
        if (line == null || line.Length == 0)
            throw new InvalidOperationException("Empty curve");
        if (line.Length == 1)
            return line[0].position;

        float idxf = Mathf.Clamp01(t) * (line.Length - 1);
        int idx = Mathf.FloorToInt(idxf);
        int nxt = Mathf.Min(idx + 1, line.Length - 1);
        float frac = idxf - idx;
        return Vector3.Lerp(
            line[idx].position,
            line[nxt].position,
            frac
        );
    }
    void GenerateMesh()
    {
        int rows = centerLine.Length;
        int N = centerLine?.Length ?? 0;
        if (N < 2 ||
            portBootLine == null || portBootLine.Length < 2 ||
            portDeckLine == null || portDeckLine.Length < 2)
        {
            _mesh.Clear();
            return;
        }

        // 1) Sample exactly N points front-to-back on each port curve
        Vector3[] C = new Vector3[N];
        Vector3[] B = new Vector3[N];
        Vector3[] D = new Vector3[N];
        for (int i = 0; i < N; i++)
        {
            float t = (float)i / (N - 1);
            C[i] = centerLine[i].position;
            B[i] = SampleCurve(portBootLine, t);
            D[i] = SampleCurve(portDeckLine, t);
        }

        // new — mirror around that row’s centerline C[i]
        Vector3[] SB = new Vector3[N];
        Vector3[] SD = new Vector3[N];
        for (int i = 0; i < N; i++)
        {
            // grab the world-space center point of this station:
            Vector3 c = C[i];
            // port boot-point:
            Vector3 bp = B[i];
            // port deck-point:
            Vector3 dp = D[i];

            // reflect X about c.x
            SB[i] = new Vector3(2f * c.x - bp.x, bp.y, bp.z);
            SD[i] = new Vector3(2f * c.x - dp.x, dp.y, dp.z);
        }
        // 3) Build 5×N vertex array: [C | B | D | SD | SB]
        const int cols = 5;
        Vector3[] verts = new Vector3[cols * N];
        for (int x = 0; x < cols; x++)
        {
            var line = x == 0 ? C
                     : x == 1 ? B
                     : x == 2 ? D
                     : x == 3 ? SD
                               : SB;
            for (int y = 0; y < N; y++)
                verts[x * N + y] = line[y];
        }

        // 4) build the 4 “strip” quads between adjacent columns
        var tris = new List<int>((cols - 1) * (rows - 1) * 2 * 3);
        for (int x = 0; x < cols - 1; x++)
            for (int y = 0; y < rows - 1; y++)
            {
                int a = x * rows + y;
                int b = x * rows + y + 1;
                int c = (x + 1) * rows + y;
                int d = (x + 1) * rows + y + 1;

                // for port & deck you might do normal winding,
                // for the mirrored strips you may need to reverse,
                // but the exact winding doesn’t affect connectivity:
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }

        // 5) CLOSE THE LOOP back from last column to column 0
        int lastCol = cols - 1;
        int firstCol = 0;
        for (int y = 0; y < rows - 1; y++)
        {
            int a = lastCol * rows + y;
            int b = lastCol * rows + (y + 1);
            int c = firstCol * rows + y;
            int d = firstCol * rows + (y + 1);

            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(b); tris.Add(d); tris.Add(c);
        }

        // 5) Upload and recalc
        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.triangles = tris.ToArray();
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
    }

    /// <summary>
    /// Mirror a world-space point through our local X=0 plane
    /// </summary>
    Vector3 MirrorLocalX(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.x = -local.x;
        return transform.TransformPoint(local);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        const float s = 0.05f;

        // Boot line (red)
        if (portBootLine != null)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < portBootLine.Length; i++)
            {
                var t0 = portBootLine[i];
                if (t0 == null) continue;
                Gizmos.DrawSphere(t0.position, s);
                if (i + 1 < portBootLine.Length
                 && portBootLine[i + 1] != null)
                {
                    Gizmos.DrawLine(
                      t0.position,
                      portBootLine[i + 1].position
                    );
                }
            }
        }

        // Deck line (green)
        if (portDeckLine != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < portDeckLine.Length; i++)
            {
                var t0 = portDeckLine[i];
                if (t0 == null) continue;
                Gizmos.DrawSphere(t0.position, s);
                if (i + 1 < portDeckLine.Length
                 && portDeckLine[i + 1] != null)
                {
                    Gizmos.DrawLine(
                      t0.position,
                      portDeckLine[i + 1].position
                    );
                }
            }
        }

        // Center line (yellow)
        if (centerLine != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < centerLine.Length; i++)
            {
                var t0 = centerLine[i];
                if (t0 == null) continue;
                Gizmos.DrawSphere(t0.position, s);
                if (i + 1 < centerLine.Length
                 && centerLine[i + 1] != null)
                {
                    Gizmos.DrawLine(
                      t0.position,
                      centerLine[i + 1].position
                    );
                }
            }
        }
    }
#endif
}
