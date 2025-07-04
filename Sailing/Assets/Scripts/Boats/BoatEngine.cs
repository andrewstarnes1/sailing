using UnityEngine;

public class BoatEngine : MonoBehaviour
{
    [Header("Power Settings")]
    [SerializeField] float maxEnginePower = 250f;
    [Range(-0.5f, 1f)] public float engineThrottle = 0f;

    [Header("Fuel Settings")]
    public float maxFuel = 100f;      // Litres or arbitrary units
    public float fuelConsumptionRate = 0.1f; // Fuel per second at full throttle
    private float currentFuel;

    [Header("Reliability Settings")]
    [Range(0f, 1f)] public float failureChancePerMinute = 0.01f; // 1% per minute at full throttle
    public float repairTime = 10f; // Seconds to repair engine
    private float failureTimer = 0f;
    private float repairTimer = 0f;
    private bool isBroken = false;

    // Runtime fields
    private Rigidbody _boatRigidbody;
    public bool isRunning { get; private set; } = false;

    // --- INITIALIZE ---
    public void InitBoat(BoatController aBoat)
    {
        _boatRigidbody = aBoat.physicsBody;
        currentFuel = maxFuel;
        isBroken = false;
        repairTimer = 0f;
    }

    // --- ENGINE LOGIC ---
    void FixedUpdate()
    {
        if (_boatRigidbody == null) return;

        // Burn fuel if engine is running and not broken
        if (!isBroken && engineThrottle > 0.01f && currentFuel > 0f)
        {
            float burn = fuelConsumptionRate * engineThrottle * Time.fixedDeltaTime;
            currentFuel = Mathf.Max(0f, currentFuel - burn);

            // Reliability check (probabilistic)
            failureTimer += Time.fixedDeltaTime;
            float minutes = failureTimer / 60f;
            float failProb = 1f - Mathf.Pow(1f - failureChancePerMinute, minutes * engineThrottle);
            if (Random.value < failProb)
            {
                isBroken = true;
                repairTimer = repairTime;
                Debug.Log("Engine has failed!");
            }
            else
            {
                // Apply engine force
                _boatRigidbody.AddForce(
                   _boatRigidbody.transform.forward * maxEnginePower * engineThrottle
                  ,
                    ForceMode.Force
                );
            }
            isRunning = true;
        }
        else
        {
            isRunning = false;
            if (currentFuel <= 0f) engineThrottle = 0f;
        }

        // If broken, count down repair
        if (isBroken)
        {
            repairTimer -= Time.fixedDeltaTime;
            if (repairTimer <= 0f)
            {
                isBroken = false;
                failureTimer = 0f;
                Debug.Log("Engine repaired!");
            }
        }
    }

    // --- FUEL ACCESSORS ---
    public float GetFuelPercent() => maxFuel > 0f ? currentFuel / maxFuel : 0f;
    public void Refuel(float amount) => currentFuel = Mathf.Clamp(currentFuel + amount, 0f, maxFuel);

    // --- STATUS ACCESSORS ---
    public bool IsBroken() => isBroken;
    public float GetRepairTimeRemaining() => Mathf.Max(0f, repairTimer);
}
