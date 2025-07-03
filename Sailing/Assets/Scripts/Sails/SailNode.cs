using System;
using UnityEngine;

[System.Serializable]
public class SailNode
{
    public Vector3 position;
    public Vector3 previousPosition;
    public Vector3 externalForce;
    public bool isFixed; // Fixed nodes don't move (head/tack/clew)
    public float damping = 0.95f;
    public SailNode(Vector3 startPosition, bool fixedNode)
    {
        position = startPosition;
        previousPosition = startPosition;
        isFixed = fixedNode;
    }

    public void UpdatePosition(float dt)
    {
        if (isFixed) { externalForce = Vector3.zero; return; }

        // lower this to something like 0.90–0.95 to settle faster
        Vector3 velocity = (position - previousPosition) * damping;
        Vector3 next = position + velocity + externalForce * dt * dt;

        previousPosition = position;
        position = next;
        externalForce = Vector3.zero;
    }

    public void ApplyExternalForce(Vector3 f)
    {
        // just accumulate into the Verlet step:
        externalForce += f;
    }
}
