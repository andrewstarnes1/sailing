using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SailMeshGenerator : MonoBehaviour
{
    public Transform Head;
    public Transform Tack;
    public Transform Clew;

    [Range(2, 20)] public int verticalSegments = 10;
    [Range(2, 20)] public int horizontalSegments = 10;

    private MeshFilter meshFilter;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        GenerateMesh();
    }

    void Update()
    {
        GenerateMesh(); // Realtime update for dynamic deformation
    }

    void GenerateMesh()
    {
        Vector3[] vertices = new Vector3[(verticalSegments + 1) * (horizontalSegments + 1)];
        int[] triangles = new int[verticalSegments * horizontalSegments * 6];

        for (int y = 0; y <= verticalSegments; y++)
        {
            float tY = y / (float)verticalSegments;
            Vector3 frontEdge = Vector3.Lerp(Tack.position, Head.position, tY);
            Vector3 rearEdge = Vector3.Lerp(Clew.position, Head.position, tY);

            for (int x = 0; x <= horizontalSegments; x++)
            {
                float tX = x / (float)horizontalSegments;
                vertices[y * (horizontalSegments + 1) + x] = Vector3.Lerp(frontEdge, rearEdge, tX) - transform.position;
            }
        }

        int ti = 0;
        for (int y = 0; y < verticalSegments; y++)
        {
            for (int x = 0; x < horizontalSegments; x++)
            {
                int i = y * (horizontalSegments + 1) + x;
                triangles[ti++] = i;
                triangles[ti++] = i + horizontalSegments + 1;
                triangles[ti++] = i + 1;

                triangles[ti++] = i + 1;
                triangles[ti++] = i + horizontalSegments + 1;
                triangles[ti++] = i + horizontalSegments + 2;
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }
}
