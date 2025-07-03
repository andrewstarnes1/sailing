using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GenoaSail : MonoBehaviour
{
    [Header("Sail Corners")]
    public Transform head;
    public Transform tack;
    public Transform portSheet;
    public Transform starboardSheet;

    [Header("Sheet Tension (0 = loose, 1 = tight)")]
    [Range(0f, 1f)] public float portSheetTension = 1f;
    [Range(0f, 1f)] public float starboardSheetTension = 1f;

    [Header("Mesh Settings")]
    public int verticalSegments = 10;
    public int horizontalSegments = 5;

    private Mesh sailMesh;
    private Vector3 clewPosition;

    void Start()
    {
        sailMesh = new Mesh { name = "GenoaSail" };
        GetComponent<MeshFilter>().mesh = sailMesh;
    }

    void Update()
    {
        UpdateClewPosition();
        GenerateSailMesh();
    }

    private void UpdateClewPosition()
    {
        // Simulate clew position based on sheet tensions
        Vector3 portClew = portSheet.position;
        Vector3 starboardClew = starboardSheet.position;

        // Interpolate clew position based on tensions
        clewPosition = Vector3.Lerp(portClew, starboardClew, starboardSheetTension / (portSheetTension + starboardSheetTension));
    }

    private void GenerateSailMesh()
    {
        Vector3[,] vertices = new Vector3[verticalSegments + 1, horizontalSegments + 1];
        Vector2[] uv = new Vector2[(verticalSegments + 1) * (horizontalSegments + 1)];
        List<int> triangles = new List<int>();

        // Generate vertices grid
        for (int y = 0; y <= verticalSegments; y++)
        {
            float tY = y / (float)verticalSegments;
            Vector3 frontEdge = Vector3.Lerp(tack.position, head.position, tY);
            Vector3 backEdge = Vector3.Lerp(clewPosition, head.position, tY);

            for (int x = 0; x <= horizontalSegments; x++)
            {
                float tX = x / (float)horizontalSegments;
                vertices[y, x] = Vector3.Lerp(frontEdge, backEdge, tX);
                uv[y * (horizontalSegments + 1) + x] = new Vector2(tX, tY);
            }
        }

        // Generate triangles
        for (int y = 0; y < verticalSegments; y++)
        {
            for (int x = 0; x < horizontalSegments; x++)
            {
                int idx = y * (horizontalSegments + 1) + x;

                triangles.Add(idx);
                triangles.Add(idx + horizontalSegments + 1);
                triangles.Add(idx + 1);

                triangles.Add(idx + 1);
                triangles.Add(idx + horizontalSegments + 1);
                triangles.Add(idx + horizontalSegments + 2);
            }
        }

        // Assign mesh data
        sailMesh.Clear();
        sailMesh.vertices = Flatten(vertices);
        sailMesh.uv = uv;
        sailMesh.triangles = triangles.ToArray();
        sailMesh.RecalculateNormals();
    }

    private Vector3[] Flatten(Vector3[,] array2D)
    {
        int width = array2D.GetLength(0);
        int height = array2D.GetLength(1);
        Vector3[] array1D = new Vector3[width * height];
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                array1D[i * height + j] = transform.InverseTransformPoint(array2D[i, j]);
        return array1D;
    }
}
