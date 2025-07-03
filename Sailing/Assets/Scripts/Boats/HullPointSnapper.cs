#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshCollider))]
public class HullPointSnapper : MonoBehaviour
{
    [Tooltip("Assign the MeshCollider of your hull. That collider will be ray-cast against.")]
    public MeshCollider hullCollider;

    [Tooltip("All of the Transforms whose Y you want locked to the hull’s surface.")]
    public Transform[] samplePoints;

    void Reset()
    {
        // If you add this component in the Inspector, auto-assign the hull’s collider if there is one.
        hullCollider = GetComponent<MeshCollider>();
    }

    void OnValidate()
    {
        // Every time you tweak anything in the Inspector (or move a point in Scene),
        // we re-snap.
        SnapAllPoints();
    }

    /// <summary>
    /// Context menu so you can also right-click the component and run it manually.
    /// </summary>
    [ContextMenu("Snap Points To Hull")]
    public void SnapAllPoints()
    {
#if UNITY_EDITOR
        if (hullCollider == null)
        {
            Debug.LogWarning("HullPointSnapper: please assign a MeshCollider to use for snapping.");
            return;
        }

        // We cast from slightly above the top of the collider’s bounds,
        // straight down in world-space, and position the Transform at the hit Y.
        float topY = hullCollider.bounds.max.y + 0.1f;
        foreach (var t in samplePoints)
        {
            if (t == null) continue;

            var worldXZ = new Vector3(t.position.x, topY, t.position.z);
            var ray = new Ray(worldXZ, Vector3.down);

            if (hullCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                // Record for Undo so you can Cmd-Z in the Editor
#if UNITY_EDITOR
                Undo.RecordObject(t, "Snap Point Y to Hull Mesh");
#endif
                t.position = new Vector3(t.position.x, hit.point.y, t.position.z);
            }
        }
#endif
    }
}
