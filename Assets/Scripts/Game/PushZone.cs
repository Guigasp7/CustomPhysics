using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PushZone : MonoBehaviour
{
    [SerializeField] GYTCollider coll;
    [SerializeField] bool instantforce;
    [SerializeField] bool ActivatePermanently;
    [SerializeField] float force;
    [SerializeField] Vector3 dir;
    [SerializeField] bool isDirLocal;
    bool isActivated = false;
    private List<GYTCollider> colliders = new();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (coll == null)
            coll = GetComponent<GYTCollider>();

        coll.OnCollisionEnter.AddListener(OnCollEnter);
        coll.OnTriggerStay.AddListener(OnTrigStay);
        coll.OnTriggerExit.AddListener(OnTrigExit);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnCollEnter(CollisionPoint point)
    {
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

        other.Rigidbody.AddImpulse(force * normal.normalized);
    }

    public void OnTrigStay(GYTCollider other)
    {
        Vector3 globalDir = isDirLocal ? (transform.rotation * dir).normalized : dir.normalized;

        if (!instantforce)
        {
            if (ActivatePermanently || isActivated)
                other.Rigidbody.AddImpulse(force * Time.fixedDeltaTime * globalDir);
        }
        else
        {
            if ((ActivatePermanently || isActivated))
            {
                if (!colliders.Contains(other))
                {
                    other.Rigidbody.AddImpulse(force * globalDir);
                    colliders.Add(other);
                }
            }
        }
    }

    public void OnTrigExit(GYTCollider other)
    {
        colliders.Remove(other);
    }

    public void OnAddBall(InputAction.CallbackContext context)
    {
        if (context.started)
            isActivated = true;
        else if (context.canceled)
            isActivated = false;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 globalDir = isDirLocal ? (transform.rotation * dir).normalized : dir.normalized;

        Gizmos.color = Color.green;

        Gizmos.DrawLine(transform.position, transform.position + globalDir * force);

        Gizmos.color = Color.white;
    }
}
