
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    public static string SelectedSubject = "Chemistry";

    public void StartGame()
    {
        if (TajarbagahExit.InApp) { TajarbagahExit.ToAppMenu(); return; }   // INTEGRATION (Tajarbagah): her menus are replaced by the app menu
        SceneManager.LoadScene("SubjectSelectScene");
    }

    public void SelectPhysics()
    {
        SelectSubject("Physics");
    }

    public void SelectChemistry()
    {
        SelectSubject("Chemistry");
    }

    public void SelectBiology()
    {
        SelectSubject("Biology");
    }

    private void SelectSubject(string subject)
    {
        SelectedSubject = subject;
        SceneManager.LoadScene("PracticalSelectScene");
    }

    public void OpenCuSO4Practical()
    {
        if (SelectedSubject != "Chemistry")
        {
            Debug.LogWarning(
                "This practical belongs to Chemistry."
            );
            return;
        }

        SceneManager.LoadScene("ChemistryCuSO4Scene");
    }

    public void BackToSubjects()
    {
        if (TajarbagahExit.InApp) { TajarbagahExit.ToAppMenu(); return; }   // INTEGRATION (Tajarbagah): her menus are replaced by the app menu
        SceneManager.LoadScene("SubjectSelectScene");
    }

    public void BackToPracticals()
    {
        if (TajarbagahExit.InApp) { TajarbagahExit.ToAppMenu(); return; }   // INTEGRATION (Tajarbagah): her menus are replaced by the app menu
        SceneManager.LoadScene("PracticalSelectScene");
    }

    public void BackToStart()
    {
        if (TajarbagahExit.InApp) { TajarbagahExit.ToAppMenu(); return; }   // INTEGRATION (Tajarbagah): her menus are replaced by the app menu
        SceneManager.LoadScene("StartScene");
    }

    public void QuitGame()
    {
        if (TajarbagahExit.InApp) { TajarbagahExit.ToAppMenu(); return; }   // INTEGRATION (Tajarbagah): her menus are replaced by the app menu
        Application.Quit();
    }
}
