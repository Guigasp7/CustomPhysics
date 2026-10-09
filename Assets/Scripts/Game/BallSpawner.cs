using UnityEngine;
using UnityEngine.InputSystem;

public class BallSpawner : MonoBehaviour
{
    [SerializeField] private GameObject ball;


    public void OnSpawnNewBall(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            Instantiate(ball, transform.position, Quaternion.identity);
        }
    }
}
