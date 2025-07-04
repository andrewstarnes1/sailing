using UnityEngine;

public class BoatController : MonoBehaviour
{
    public Rigidbody physicsBody;
    [SerializeField]public Rudder rudder;
    [SerializeField] RealisticSailPhysics genoaSail;
    [SerializeField] BoatEngine boatEngine; 


    private void Start()
    {
        boatEngine.InitBoat(this);
    }

    public BoatEngine engine
    {
        get
        {
            return boatEngine;
        }
    }
}
