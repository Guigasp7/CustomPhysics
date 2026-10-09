using UnityEngine;
using System;
using UnityEngine.Rendering;
using Unity.VisualScripting;
using UnityEditor;

public class GYTRigidbody : MonoBehaviour
{
    public bool isDefault = false;
    public float mass;
    public Vector3 speed;
    public Vector3 angularSpeed;
    public Vector3 AngularSpeed
    {
        get { return angularSpeed; }
        set
        {
            if(value.magnitude > 0.01f)
                angularSpeed = value;
        }
    }
    public Vector3 linearAcceleration;
    public Vector3 angularAcceleration;
    public float linearDamping = 0.05f;
    public float angularDamping = 0.1f;
    private Matrix4x4 bodyTensor;
    private Matrix4x4 inertiaTensor;
    public float restitutionCoeff;
    public float staticFrictionCoef;
    public float kineticFrictionCoef;
    public bool useGravity;
    private float timeBeforeSleeping = 3f;
    private float sleepTimer = 0f;
    public bool isSleeping = false;

    public struct CollisionValues
    {
        public bool isDefault;
        public float mass;
        public Vector3 speed;
        public Vector3 angularSpeed;
        public Matrix4x4 inertiaTensor;
        public float restitutionCoeff;
        public float staticFrictionCoef;
        public float kineticFrictionCoef;
        public GYTRigidbody rb;
    }
    public CollisionValues GetValues()
    {
        CollisionValues values = new CollisionValues();

        values.rb = this;
        values.isDefault = isDefault;
        values.mass = mass;
        values.speed = speed;
        values.angularSpeed = angularSpeed;
        values.restitutionCoeff = restitutionCoeff;
        values.staticFrictionCoef = staticFrictionCoef;
        values.kineticFrictionCoef = kineticFrictionCoef;
        values.inertiaTensor = inertiaTensor;

        return values;
    }

    public static CollisionValues GetDefaultValues()
    {
        CollisionValues values = new CollisionValues();
        values.isDefault = true;
        values.mass = 0;
        values.speed = Vector3.zero;
        values.angularSpeed = Vector3.zero;
        values.restitutionCoeff = 0f;
        values.staticFrictionCoef = 0f;
        values.kineticFrictionCoef = 0f;
        values.inertiaTensor = Matrix4x4.zero;
        values.inertiaTensor.m33 = 1f;

        return values;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CalculateInertiaTensor();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        #region SleepCheck
        Vector3 noGravitySpeed = new Vector3(speed.x, speed.y + 9.81f * Time.fixedDeltaTime, speed.z);
        if (noGravitySpeed.magnitude < 0.3f && angularSpeed.magnitude < 0.75f)
        {
            if (!isSleeping)
            {
                sleepTimer += Time.fixedDeltaTime;
                if (sleepTimer >= timeBeforeSleeping)
                {
                    Sleep();
                }
            }
        }
        else
        {
            if (sleepTimer != 0f)
                sleepTimer = 0f;
            if (isSleeping)
                isSleeping = false;
        }
        #endregion

        if (!isSleeping)
        {
            CalculateInertiaTensor();

            if (useGravity)
            {
                speed.y += -9.81f * Time.fixedDeltaTime;
            }

            speed += linearAcceleration * Time.fixedDeltaTime;
            angularSpeed += angularAcceleration * Time.fixedDeltaTime;

            speed *= (1f - linearDamping);
            angularSpeed *= (1f - angularDamping);

            gameObject.transform.position += speed * Time.fixedDeltaTime;
            gameObject.transform.rotation = Quaternion.Euler(angularSpeed * Mathf.Rad2Deg * Time.fixedDeltaTime) * gameObject.transform.rotation;
        }
    }

    void CalculateInertiaTensor()
    {
        Matrix4x4 rotation = Matrix4x4.Rotate(gameObject.transform.rotation);
        inertiaTensor = rotation * bodyTensor * rotation.transpose;
    }

    public void PickBodyTensor(GYTCollider.ColliderType type)
    {
        float radius;
        switch (type)
        {
            case GYTCollider.ColliderType.BOX:
                float x = gameObject.transform.lossyScale.x;
                float y = gameObject.transform.lossyScale.y;
                float z = gameObject.transform.lossyScale.z;
                bodyTensor = new Matrix4x4(
                    new Vector4(1f / 12f * mass * (z * z + y * y), 0, 0, 0),
                    new Vector4(0, 1f / 12f * mass * (x * x + z * z), 0, 0),
                    new Vector4(0, 0, 1f / 12f * mass * (x * x + y * y), 0),
                    new Vector4(0, 0, 0, 1f));
                break;

            case GYTCollider.ColliderType.MESH:
                radius = (this.gameObject.GetComponent<GYTCollider>().bounds.max - gameObject.transform.position).magnitude;
                bodyTensor = new Matrix4x4(
                    new Vector4(2f / 5f * mass * radius * radius, 0, 0, 0),
                    new Vector4(0, 2f / 5f * mass * radius * radius, 0, 0),
                    new Vector4(0, 0, 2f / 5f * mass * radius * radius),
                    new Vector4(0, 0, 0, 1f));
                break;

            case GYTCollider.ColliderType.SPHERE:
                radius = this.gameObject.GetComponent<SphereCollider>().radius;
                bodyTensor = new Matrix4x4(
                    new Vector4(2f / 5f * mass * radius * radius, 0, 0, 0),
                    new Vector4(0, 2f / 5f * mass * radius * radius, 0, 0),
                    new Vector4(0, 0, 2f / 5f * mass * radius * radius),
                    new Vector4(0, 0, 0, 1f));
                break;

        }
    }

    public void AddImpulse(Vector3 impulse)
    {
        speed += impulse / mass;
    }

    public void Sleep()
    {
        isSleeping = true;
        speed = Vector3.zero;
        angularSpeed = Vector3.zero;
    }

    public void WakeUp()
    {
        sleepTimer = 0f;
        isSleeping = false;
    }
}