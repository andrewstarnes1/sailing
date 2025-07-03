using UnityEngine;

public class SailWind : MonoBehaviour
{
    private MeshFilter meshFilter;
    private Rigidbody clewRigidbody;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        clewRigidbody = GetComponentInChildren<Rigidbody>();
    }

    void FixedUpdate()
    {
        ApplyWindForce();
    }

    void ApplyWindForce()
    {
        if (GlobalWind.Instance == null) return;

        Vector3 windForce = GlobalWind.Instance.GetWindForceAtPosition(transform.position);

        Vector3[] vertices = meshFilter.mesh.vertices;
        Vector3[] normals = meshFilter.mesh.normals;
        Vector3 totalForce = Vector3.zero;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldNormal = transform.TransformDirection(normals[i]);
            float forceMagnitude = Mathf.Max(Vector3.Dot(worldNormal, windForce.normalized), 0f);
            totalForce += worldNormal * forceMagnitude * GlobalWind.Instance.windStrength / vertices.Length;
        }

        if (clewRigidbody)
        {
            clewRigidbody.AddForce(totalForce);
        }
    }
}
