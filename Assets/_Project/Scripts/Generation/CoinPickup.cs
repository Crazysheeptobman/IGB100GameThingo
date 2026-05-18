using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    [SerializeField] private int coinValue = 1;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        HighScoreSystem scoreSystem =
            FindFirstObjectByType<HighScoreSystem>();

        if (scoreSystem != null)
        {
            scoreSystem.AddCoin(coinValue);
        }

        Destroy(gameObject);
    }
}