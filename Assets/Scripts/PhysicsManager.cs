using System;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsManager : MonoBehaviour
{
    static PhysicsManager instance = null;

    public static PhysicsManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<PhysicsManager>();
            }
            return instance;
        }
    }

    public List<GYTRigidbody> rigidBodies = new List<GYTRigidbody>();
    public List<GYTCollider> colliders = new List<GYTCollider>();

    [Header("BroadPhase")]
    [SerializeField]
    private BroadPhaseCollisions broadManager;

    [SerializeField, Min(0f)]
    private float boxMargin;

    [SerializeField, Min(1)]
    private int boxMarginVelocityMultiplier;

    [Header("NarrowPhase")]
    [SerializeField]
    NarrowPhaseCollisions narrowManager;

    [Header("CollisionResponse")]

    [SerializeField]
    private bool applyFriction = true;

    public BroadPhaseCollisions BroadManager { get { return broadManager; } }

    public float BoxMargin { get { return boxMargin; } }

    public int BoxMarginVelocityMultiplier { get { return boxMarginVelocityMultiplier; } }

    public NarrowPhaseCollisions NarrowManager { get { return narrowManager; } }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        broadManager = FindFirstObjectByType<BroadPhaseCollisions>();
        narrowManager = FindFirstObjectByType<NarrowPhaseCollisions>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        #region BroadPhase
        List<Tuple<GYTCollider, GYTCollider>> pairs = broadManager.UpdateTree();
        for (int i = 0; i < pairs.Count; ++i)
        {
            // If 1 static and 2 sleeping, ignore
            if (pairs[i].Item1.Rigidbody == null)
            {
                if (pairs[i].Item2.Rigidbody.isSleeping)
                    pairs.Remove(pairs[i]);
            }
            else
            {
                // If 2 static and 1 sleeping, ignore
                if (pairs[i].Item2.Rigidbody == null)
                {
                    if (pairs[i].Item1.Rigidbody.isSleeping)
                        pairs.Remove(pairs[i]);
                }
                else
                {
                    // If 1 and 2 sleeping, ignore, else wake up 1
                    if (pairs[i].Item1.Rigidbody.isSleeping)
                    {
                        if (pairs[i].Item2.Rigidbody.isSleeping)
                            pairs.Remove(pairs[i]);
                    }
                }
            }
        }
        #endregion

        #region NarrowPhase & Collisions reponse
        List<CollisionPoint> points = narrowManager.UpdateCollisions(pairs);
        foreach (CollisionPoint collisionPoint in points)
        {
            CollisionResponse(collisionPoint);

            collisionPoint.firstCollider.Collide(collisionPoint);
            collisionPoint.secondCollider.Collide(collisionPoint);
        }
        foreach (GYTCollider collider in colliders)
        {
            collider.UpdateCollisionEvents();
        }
        #endregion
    }

    void CollisionResponse(CollisionPoint collision)
    {
        GYTRigidbody firstRB = collision.firstCollider.Rigidbody;
        GYTRigidbody secondRB = collision.secondCollider.Rigidbody;

        GYTRigidbody.CollisionValues firstValues;
        GYTRigidbody.CollisionValues secondValues;

        Vector3 firstColliderPos = collision.firstCollider.gameObject.transform.position;
        Vector3 secondColliderPos = collision.secondCollider.gameObject.transform.position;

        if (firstRB == null)
        {
            firstValues = GYTRigidbody.GetDefaultValues();
        }
        else
        {
            firstValues = firstRB.GetValues();
        }
        if (secondRB == null)
        {
            secondValues = GYTRigidbody.GetDefaultValues();
        }
        else
        {
            secondValues = secondRB.GetValues();
        }

        float invMassA;
        float invMassB;

        if (firstValues.mass == 0f)
        {
            invMassA = 0f;
        }
        else
        {
            invMassA = 1f / firstValues.mass;
        }

        if (secondValues.mass == 0f)
        {
            invMassB = 0f;
        }
        else
        {
            invMassB = 1f / secondValues.mass;
        }

        Vector3 rPosA = collision.contactPoint - firstColliderPos;
        Vector3 rPosB = collision.contactPoint - secondColliderPos;

        Vector3 impactPointSpeedA = firstValues.speed + Vector3.Cross(firstValues.angularSpeed, rPosA);
        Vector3 impactPointSpeedB = secondValues.speed + Vector3.Cross(secondValues.angularSpeed, rPosB);

        Vector3 momentumA = firstValues.inertiaTensor.inverse * Vector3.Cross(rPosA, collision.normal);
        Vector3 momentumB = secondValues.inertiaTensor.inverse * Vector3.Cross(rPosB, collision.normal);

        float weightRotaA = Vector3.Dot(Vector3.Cross(momentumA, rPosA), collision.normal);
        float weightRotaB = Vector3.Dot(Vector3.Cross(momentumB, rPosB), collision.normal);

        PositionCorrection(firstValues, invMassA, secondValues, invMassB, collision.normal, collision.penetrationDepth, collision.contactPoint, 10);

        Vector3 vRel = impactPointSpeedA - impactPointSpeedB;
        float relativeSpeed = Vector3.Dot(vRel, collision.normal);
        if (relativeSpeed >= 0f)
        {
            return;
        }

        float restitution = (firstValues.restitutionCoeff + secondValues.restitutionCoeff) / 2f;
        float J = (-(1f + restitution) * relativeSpeed) / (invMassA + invMassB + weightRotaA + weightRotaB);

        if (!firstValues.isDefault)
        {
            firstRB.AngularSpeed += J * momentumA;
            firstRB.speed += J * invMassA * collision.normal;
        }
        if (!secondValues.isDefault)
        {
            secondRB.AngularSpeed -= J * momentumB;
            secondRB.speed -= J * invMassB * collision.normal;
        }

        if (applyFriction)
            ApplyFriction(firstValues, invMassA, rPosA, secondValues, invMassB, rPosB, vRel, collision.normal, J);

    }

    void PositionCorrection(GYTRigidbody.CollisionValues firstRB, float invMassA, GYTRigidbody.CollisionValues secondRB, float invMassB, Vector3 normal, float penetration, Vector3 point, int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            float damping = 1f;
            float correction = (penetration * damping) / (invMassA + invMassB);
            correction /= iterations;

            if (!firstRB.isDefault)
            {
                firstRB.rb.transform.position += invMassA * correction * normal;
            }
            if (!secondRB.isDefault)
            {
                secondRB.rb.transform.position -= invMassB * correction * normal;
            }
        }
    }

    void ApplyFriction(GYTRigidbody.CollisionValues firstValues, float invMassA, Vector3 rA, GYTRigidbody.CollisionValues secondValues, float invMassB, Vector3 rB, Vector3 relativeSpeed, Vector3 normal, float J)
    {
        Vector3 tSpeed = relativeSpeed - Vector3.Dot(relativeSpeed, normal) * normal;
        Vector3 t;

        if (tSpeed.magnitude > 0f)
        {
            t = tSpeed / tSpeed.magnitude;
        }
        else
        {
            t = Vector3.zero;
        }


        Vector3 relTA = Vector3.Cross(t, rA);
        Vector3 relTB = Vector3.Cross(t, rB);

        Vector3 momentumA = firstValues.inertiaTensor.inverse * relTA;
        Vector3 momentumB = secondValues.inertiaTensor.inverse * relTB;

        float lambda = invMassA + invMassB + Vector3.Dot(relTA, momentumA) + Vector3.Dot(relTB, momentumB);

        float Jt = -Vector3.Dot(relativeSpeed, t) / lambda;

        float staticFriction = (firstValues.staticFrictionCoef + secondValues.staticFrictionCoef) / 2f;

        if (Jt <= staticFriction * J)
        {
            Jt = -Vector3.Dot(relativeSpeed, t) / lambda;
        }
        else
        {
            float kineticFriction = (firstValues.kineticFrictionCoef + secondValues.kineticFrictionCoef) / 2f;
            Jt = kineticFriction * J;
        }

        if (!firstValues.isDefault)
        {
            firstValues.rb.speed += Jt * invMassA * t;
            firstValues.rb.AngularSpeed += Vector3.Cross(rA, Jt * t);
        }
        if (!secondValues.isDefault)
        {
            secondValues.rb.speed -= Jt * invMassB * t;
            secondValues.rb.AngularSpeed -= Vector3.Cross(rB, Jt * t);
        }
    }
}
