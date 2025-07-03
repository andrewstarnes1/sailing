using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GlobalWind : MonoBehaviour
{
    public static GlobalWind Instance { get; private set; }

    [Header("Wind Settings")]

    [Tooltip("Wind direction as an angle around Y (0° = +X).")]
    [Range(0f, 360f)]
    public float windAngle = 0f;

    [Tooltip("Wind strength (units of force).")]
    public float windStrength = 10f;

    // Computed automatically from windAngle
    private Vector3 windDirection => new Vector3(
        Mathf.Cos(windAngle * Mathf.Deg2Rad),
        0f,
        Mathf.Sin(windAngle * Mathf.Deg2Rad)
    ).normalized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Returns the wind force vector at the given world position.
    /// (Uniform for now, but you could sample per-position if you want gusts.)
    /// </summary>
    public Vector3 GetWindForceAtPosition(Vector3 position)
    {
        return windDirection * windStrength;
    }

    /// <summary>
    /// Update wind parameters at runtime if desired.
    /// </summary>
    public void SetWind(float angleDegrees, float strength)
    {
        windAngle = Mathf.Repeat(angleDegrees, 360f);
        windStrength = strength;
    }

#if UNITY_EDITOR
    // Draw an arrow gizmo in the Scene view so you can see the wind.
    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;
        Vector3 dir = windDirection;
        float arrowLength = 2f;
        Vector3 tip = origin + dir * arrowLength;

        Gizmos.color = Color.cyan;
        // Main line
        Gizmos.DrawLine(origin, tip);

        // Arrowhead
        float headSize = 0.2f;
        Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        Gizmos.DrawLine(tip, tip + right * headSize);
        Gizmos.DrawLine(tip, tip + left * headSize);
    }
#endif
}
