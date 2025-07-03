using UnityEngine;

public class BoatFollowTarget : MonoBehaviour
{
    public Transform target;             
    public float     maxSpeed      = 5f; 
    public float     smoothTime    = 3;
    public float     acceleration  = 2f;  
    public float     deceleration  = 2.5f;
    public float     rotationSpeed = 0.2f; 
    public float     stopDistance  = 0.5f; 

    private float   currentSpeed = 0f;
    private Vector3 velocity;

    void Update()
    {
        if (target == null) return;

        Vector3 direction = (target.position - transform.position).normalized;
        float   distance  = Vector3.Distance(transform.position, target.position);
        transform.position = Vector3.SmoothDamp(transform.position, target.position, ref velocity, smoothTime);
       
        if (distance > stopDistance)
        {
            currentSpeed = Mathf.Lerp(currentSpeed, maxSpeed, Time.deltaTime * acceleration);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0f, Time.deltaTime * deceleration);
        }

        if (currentSpeed > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed * velocity.magnitude);
        }

        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }
}