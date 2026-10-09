using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;



[RequireComponent(typeof(Collider))]
public class GYTCollider : MonoBehaviour
{
    public enum ColliderType
    {
        BOX,
        SPHERE,
        MESH,
        NONE
    }

    private enum CollisionState
    {
        ENTER,
        STAY,
        LEAVING
    }

    public ColliderType type = ColliderType.NONE;
    private BoxCollider boxCollider;
    private SphereCollider sphereCollider;
    private MeshCollider meshCollider;
    private GYTRigidbody GYTRigidbody;
    public GYTRigidbody Rigidbody { get { return GYTRigidbody; } }
    List<Vector3> worldSpaceVertices = new List<Vector3>();
    UnityEvent onTransformChanged = new();
    UnityEvent<bool> onDeactivate = new();//bool: permanently (destroy)
    UnityEvent onActivate = new();
    UnityEvent<GYTCollider> onTriggerEnter = new();
    UnityEvent<GYTCollider> onTriggerStay = new();
    UnityEvent<GYTCollider> onTriggerExit = new();
    UnityEvent<CollisionPoint> onCollisionEnter = new();
    UnityEvent<CollisionPoint> onCollisionStay = new();
    UnityEvent<CollisionPoint> onCollisionExit = new();
    private Dictionary<CollisionPoint, CollisionState> collisionList = new Dictionary<CollisionPoint, CollisionState>();
    private Dictionary<GYTCollider, CollisionState> triggerList = new Dictionary<GYTCollider, CollisionState>();
    [SerializeField] private bool isTrigger = false;
    public Bounds bounds;


    public UnityEvent OnTransformChanged { get { return onTransformChanged; } }
    public UnityEvent<bool> OnDeactivate { get { return onDeactivate; } }
    public UnityEvent OnActivate { get { return onActivate; } }
    public UnityEvent<GYTCollider> OnTriggerEnter { get { return onTriggerEnter; } }
    public UnityEvent<GYTCollider> OnTriggerStay {  get { return onTriggerStay; } }
    public UnityEvent<GYTCollider> OnTriggerExit { get { return onTriggerExit; } }
    public UnityEvent<CollisionPoint> OnCollisionEnter { get { return onCollisionEnter; } }
    public UnityEvent<CollisionPoint> OnCollisionStay {  get { return onCollisionStay; } }
    public UnityEvent<CollisionPoint> OnCollisionExit { get { return onCollisionExit; } }

    public bool IsTrigger {  get { return isTrigger; } }

    public List<Vector3> WorldSpaceVertices()
    {
        return worldSpaceVertices;
    }

    void Start()
    {
        GetCollider();

        GYTRigidbody = gameObject.GetComponent<GYTRigidbody>();
        if (GYTRigidbody != null)
            GYTRigidbody.PickBodyTensor(type);

        VerticesToWorldSpace();

        PhysicsManager.Instance.colliders.Add(this);
        transform.hasChanged = false;
        PhysicsManager.Instance.BroadManager.AddCollider(this);
    }

    private void GetCollider()
    {
        Collider collider = GetComponent<Collider>();
        switch (collider.GetType().Name)
        {
            case nameof(BoxCollider):
                type = ColliderType.BOX;
                boxCollider = (BoxCollider)collider;
                bounds = boxCollider.bounds;
                break;
            case nameof(SphereCollider):
                type = ColliderType.SPHERE;
                sphereCollider = (SphereCollider)collider;
                bounds = sphereCollider.bounds;
                break;
            case nameof(MeshCollider):
                type = ColliderType.MESH;
                meshCollider = (MeshCollider)collider;
                meshCollider.convex = true;
                bounds = meshCollider.bounds;
                break;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (transform.hasChanged)
        {
            UpdateBounds();
            VerticesToWorldSpace();
            onTransformChanged?.Invoke();
            transform.hasChanged = false;
        }
    }

    private void UpdateBounds()
    {
        switch (type)
        {
            case ColliderType.BOX:
                bounds = boxCollider.bounds;
                break;
            case ColliderType.SPHERE:
                bounds = sphereCollider.bounds;
                break;
            case ColliderType.MESH:
                bounds = meshCollider.bounds;
                break;
            case ColliderType.NONE:
                Debug.LogError("wat");
                break;
            default:
                break;
        }
    }
    private void VerticesToWorldSpace()
    {
        switch (type)
        {
            case ColliderType.BOX:
                Vector3[] boxVertices = new Vector3[8];
                Vector3 min = boxCollider.center - boxCollider.size * 0.5f;
                Vector3 max = boxCollider.center + boxCollider.size * 0.5f;
                Transform boxTransform = boxCollider.transform;

                boxVertices[0] = min;
                boxVertices[1] = new Vector3(max.x, min.y, min.z);
                boxVertices[2] = new Vector3(max.x, min.y, max.z);
                boxVertices[3] = new Vector3(min.x, min.y, max.z);
                boxVertices[4] = new Vector3(min.x, max.y, min.z);
                boxVertices[5] = new Vector3(max.x, max.y, min.z);
                boxVertices[6] = max;
                boxVertices[7] = new Vector3(min.x, max.y, max.z);

                worldSpaceVertices.Clear();
                for (int i = 0; i < boxVertices.Length; i++)
                    worldSpaceVertices.Add(transform.TransformPoint(boxVertices[i]));
                break;
            case ColliderType.SPHERE:
                worldSpaceVertices.Clear();
                worldSpaceVertices.Add(transform.TransformPoint(sphereCollider.center));
                break;
            case ColliderType.MESH:
                Vector3[] meshVertices = meshCollider.sharedMesh.vertices;
                worldSpaceVertices.Clear();
                for (int i = 0; i < meshVertices.Length; i++)
                    worldSpaceVertices.Add(transform.TransformPoint(meshVertices[i]));
                break;
        }
    }

    public List<Vector3> SupportFunction(Vector3 _direction)
    {
        switch (type)
        {
            case ColliderType.BOX:
                return InternalBoxColliderSupportFunction(_direction);
            case ColliderType.SPHERE:
                return InternalSphereColliderSupportFunction(_direction);
            case ColliderType.MESH:
                return InternalMeshColliderSupportFunction(_direction);
        }
        return new List<Vector3>() { Vector3.zero };
    }

    private List<Vector3> InternalBoxColliderSupportFunction(Vector3 _direction)
    {
        float dot = float.MinValue;
        Vector3 normalizedD = _direction.normalized;
        List<Vector3> supportPoints = new List<Vector3>();
        for (int i = 0; i < worldSpaceVertices.Count; i++)
        {
            // We substract the collider's position to bring the vertice back to local space, while taking into account rotation and scale
            float test = Vector3.Dot((worldSpaceVertices[i] - transform.position).normalized, normalizedD);
            if (test >= dot)
            {
                if(test > dot)
                    supportPoints.Clear();

                supportPoints.Add(worldSpaceVertices[i]);
                dot = test;
            }
        }
        return supportPoints;
    }

    private List<Vector3> InternalSphereColliderSupportFunction(Vector3 _direction)
    {
        Vector3 normalizedD = _direction.normalized;
        Vector3 vertice = worldSpaceVertices[0] + normalizedD * sphereCollider.radius * Mathf.Max(Mathf.Max(transform.lossyScale.x, transform.lossyScale.y), transform.lossyScale.z);
        return new List<Vector3>() { vertice };
    }

    private List<Vector3> InternalMeshColliderSupportFunction(Vector3 _direction)
    {
        float dot = -1f;
        Vector3 normalizedD = _direction.normalized;
        Vector3 vertice = worldSpaceVertices[0];
        for (int i = 0; i < worldSpaceVertices.Count; i++)
        {
            // We substract the collider's position to bring the vertice back to local space, while taking into account rotation and scale
            float test = Vector3.Dot((worldSpaceVertices[i] - transform.position).normalized, normalizedD);
            if (test > dot)
            {
                dot = test;
                vertice = worldSpaceVertices[i];
            }
        }
        return new List<Vector3>() { vertice };
    }

    private void OnDestroy()
    {
        onDeactivate.Invoke(true);

        onTransformChanged.RemoveAllListeners();
        onDeactivate.RemoveAllListeners();
        onActivate.RemoveAllListeners();
        onTriggerEnter.RemoveAllListeners();
        onTriggerStay.RemoveAllListeners();
        onTriggerExit.RemoveAllListeners();
    }

    private void OnEnable()
    {
        GetCollider();
        onActivate.Invoke();
    }

    private void OnDisable()
    {
        onDeactivate.Invoke(false);
    }

    public void Trigger(GYTCollider other)
    {
        if (triggerList.TryGetValue(other, out CollisionState trigger))
        {
            if (trigger == CollisionState.LEAVING)
                triggerList[other] = CollisionState.STAY;
        }
        else
        {
            triggerList.Add(other, CollisionState.ENTER);
        }
    }

    public void Collide(CollisionPoint point)
    {
        if (collisionList.TryGetValue(point, out CollisionState state))
        {
            if (state == CollisionState.LEAVING)
                collisionList[point] = CollisionState.STAY;
        }
        else
        {
            collisionList.Add(point, CollisionState.ENTER);
        }
    }

    public void UpdateCollisionEvents()
    {
        List<GYTCollider> triggList = new(triggerList.Keys);

        foreach (GYTCollider collider in triggList)
        {
            switch (triggerList[collider])
            {
                case CollisionState.ENTER:
                    onTriggerEnter.Invoke(collider);
                    triggerList[collider] = CollisionState.LEAVING;
                    break;
                case CollisionState.STAY:
                    onTriggerStay.Invoke(collider);
                    triggerList[collider] = CollisionState.LEAVING;
                    break;
                case CollisionState.LEAVING:
                    onTriggerExit.Invoke(collider);
                    triggerList.Remove(collider);
                    break;
                default:
                    break;

            }
        }

        List<CollisionPoint> collList = new(collisionList.Keys);

        foreach (CollisionPoint collider in collList)
        {
            switch (collisionList[collider])
            {
                case CollisionState.ENTER:
                    onCollisionEnter.Invoke(collider);
                    collisionList[collider] = CollisionState.LEAVING;
                    break;
                case CollisionState.STAY:
                    onCollisionStay.Invoke(collider);
                    collisionList[collider] = CollisionState.LEAVING;
                    break;
                case CollisionState.LEAVING:
                    onCollisionExit.Invoke(collider);
                    collisionList.Remove(collider);
                    break;
                default:
                    break;

            }
        }
    }
}
