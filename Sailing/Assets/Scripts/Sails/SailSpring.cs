// Add this class to your project
using UnityEngine;

public class SailSpring
{
    public SailNode nodeA;
    public SailNode nodeB;
    public float restLength;
    public float stiffness = 0.2f;
    public SailSpring(SailNode a, SailNode b, float aStiffness = 0.2f)
    {
        nodeA = a;
        nodeB = b;
        restLength = Vector3.Distance(a.position, b.position);
        stiffness = aStiffness;
    }

    public void ApplySpringConstraint()
    {
        Vector3 delta = nodeB.position - nodeA.position;
        float currentLength = delta.magnitude;

        // ---- guard against zero-length springs ----
        if (currentLength < 1e-5f) return;
        // --------------------------------------------

        float difference = (currentLength - restLength) / currentLength;
        Vector3 offset = delta * (difference * stiffness * 0.5f);

        if (!nodeA.isFixed)
            nodeA.position += offset;
        if (!nodeB.isFixed)
            nodeB.position -= offset;
    }
}