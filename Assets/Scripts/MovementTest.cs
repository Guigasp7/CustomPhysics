using UnityEngine;

public class MovementTest : MonoBehaviour
{
    [SerializeField] private float speed = 1f;
    [SerializeField] private Vector3 direction = Vector3.zero;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.position += direction * speed * Time.deltaTime;
    }
}
