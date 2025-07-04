using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class ThrottleUIController : MonoBehaviour
{

    [SerializeField] Slider slider;
    [SerializeField] BoatEngine engine;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        slider.onValueChanged.AddListener(onThrottleChanged);
    }

    private void onThrottleChanged(float aValue)
    {
        if(engine.engineThrottle != aValue)
        {
            engine.engineThrottle = aValue;
        }
    }

    private void OnDestroy()
    {
        slider.onValueChanged.RemoveListener(onThrottleChanged);
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    internal void Init(BoatEngine aEngine)
    {
        this.engine = aEngine;
        onThrottleChanged(engine.engineThrottle);
    }
}
