using UnityEngine;

public class HighScoreSystem : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private bool showScoreOnGui = true;
    [SerializeField] private Vector2 guiPosition = new Vector2(10f, 10f);
    [SerializeField] private Vector2 guiSize = new Vector2(210f, 55f);
    [SerializeField] private int fontSize = 20; 

    private float startZ, highestZ, elapsedTime;
    public float HighestZ => highestZ;
    public int Score => Mathf.Max(0, Mathf.FloorToInt(highestZ - startZ));

    private void Awake()
    {
        if (player == null) player = transform;
        startZ = highestZ = player.position.z;
    }

    private void Update()
    {
        if (player == null) return;
        elapsedTime += Time.deltaTime;
        float z = player.position.z;
        if (z > highestZ) highestZ = z;
    }

    private void OnGUI()
    {
        if (!showScoreOnGui) return;
        
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = fontSize;
        style.alignment = TextAnchor.UpperLeft;
        
        GUI.Box(new Rect(guiPosition.x, guiPosition.y, guiSize.x, guiSize.y), 
                $"Score: {Score}\nTime: {FormatTime(elapsedTime)}", 
                style);
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f), s = Mathf.FloorToInt(seconds % 60f), ms = Mathf.FloorToInt((seconds * 100f) % 100f);
        return $"{m:00}:{s:00}.{ms:00}";
    }

    public void ResetScore()
    {
        if (player == null) return;
        startZ = highestZ = player.position.z;
        elapsedTime = 0f;
    }
}