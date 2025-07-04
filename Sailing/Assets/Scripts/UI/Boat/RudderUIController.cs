using System;
using UnityEngine;
using UnityEngine.UI;

public class RudderUIController : MonoBehaviour
{
    [SerializeField] Slider slider;
    Rudder _rudder;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        slider.onValueChanged.AddListener(onRudderValueChange);
    }

    public void InitRudder(Rudder aRudder)
    {
        _rudder = aRudder;
        onRudderValueChange(slider.value);
    }
    private void onRudderValueChange(float aRudderValue)
    {
        _rudder.steerInput = aRudderValue;
    }

    private void OnDestroy()
    {

        slider.onValueChanged.RemoveListener(onRudderValueChange);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
