using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private string ReturnSceneName = "MainMenu";


    public void MainMenu()
    {
        SceneManager.LoadScene(ReturnSceneName);
    }
}