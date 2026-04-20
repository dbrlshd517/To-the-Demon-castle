using UnityEngine;
using UnityEngine.SceneManagement;

public class GoToMainMenu : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}