using UnityEngine;

public class HighScoreSystem : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private bool showScoreOnGui = true;
    [SerializeField] private Vector2 guiPosition = new Vector2(10f, 10f);
    [SerializeField] private Vector2 guiSize = new Vector2(260f, 80f);
    [SerializeField] private int fontSize = 40;

    private float startZ;
    private float highestZ;
    private float elapsedTime;
    private int bonusScore;
    private int coins;

    public float HighestZ => highestZ;
    public int Coins => coins;

    public int Score =>
        Mathf.Max(0, Mathf.FloorToInt(highestZ - startZ))
        + bonusScore;

    private void Awake()
    {
        if (player == null)
            player = transform;

        startZ = highestZ = player.position.z;
    }

    private void Update()
    {
        if (player == null) return;

        elapsedTime += Time.deltaTime;

        float z = player.position.z;

        if (z > highestZ)
            highestZ = z;
    }

    private void OnGUI()
    {
        if (!showScoreOnGui) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = fontSize;
        style.alignment = TextAnchor.UpperLeft;

        GUI.Box(
            new Rect(
                guiPosition.x,
                guiPosition.y,
                guiSize.x,
                guiSize.y
            ),
            $"Score: {Score}\nCoins: {coins}\nTime: {FormatTime(elapsedTime)}",
            style
        );
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        int ms = Mathf.FloorToInt((seconds * 100f) % 100f);

        return $"{m:00}:{s:00}.{ms:00}";
    }

    public void ResetScore()
    {
        if (player == null) return;

        startZ = highestZ = player.position.z;
        elapsedTime = 0f;
        bonusScore = 0;
        coins = 0;
    }

    public void AddPoints(int points)
    {
        bonusScore += Mathf.Max(0, points);
    }

    public void AddCoin(int amount)
    {
        coins += Mathf.Max(0, amount);
    }
}