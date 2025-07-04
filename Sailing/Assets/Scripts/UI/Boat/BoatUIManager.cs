using UnityEngine;

public class BoatUIManager : MonoBehaviour
{
    [SerializeField] RudderUIController rudder;
    [SerializeField] GenoaSailUIController genoaSailUIController;
    [SerializeField] ThrottleUIController throttleUIController;


    [SerializeField] BoatController boatController;


    private void Start()
    {
        throttleUIController.Init(boatController.engine);
        rudder.InitRudder(boatController.rudder);
    }
}
