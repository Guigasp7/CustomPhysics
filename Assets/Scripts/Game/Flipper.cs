using UnityEngine;
using UnityEngine.InputSystem;

public class Flipper : MonoBehaviour
{
    [SerializeField] GYTCollider coll;
    [SerializeField] float maxAngle;
    [SerializeField] float angleSpeed;
    [SerializeField] bool isEnabled = false;
    [SerializeField] bool isFinished = false;
    [SerializeField] float bumpForce;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        coll.OnCollisionEnter.AddListener(OnCollEnter);
    }

    // Update is called once per frame
    void Update()
    {
        if (isFinished) 
            return;

        if (isEnabled)
        {
            if (Quaternion.Angle(transform.localRotation, Quaternion.Euler(0f, maxAngle, 0f)) > 0.001f)
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.Euler(0f,maxAngle,0f), Time.deltaTime * angleSpeed);
            else
                isFinished = true;
        }
        else
        {
            if (Quaternion.Angle(transform.localRotation, Quaternion.Euler(0f, 0f, 0f)) > 0.001f)
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.identity, Time.deltaTime * angleSpeed);
            else
                isFinished = true;
        }
    }

    public void OnCollEnter(CollisionPoint point)
    {
        if (isFinished) 
            return;

        GYTCollider other;
        Vector3 normal;

        if (point.firstCollider == coll)
        {
            other = point.secondCollider;
            normal = -point.normal;
        }
        else
        {
            other = point.firstCollider;
            normal = point.normal;
        }

        other.Rigidbody.AddImpulse(normal * bumpForce);
    }

    public void OnActivate(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            isFinished = false;
            isEnabled = true;
        }
        else if (context.canceled)
        {
            isFinished = false;
            isEnabled = false;
        }
    }
}
