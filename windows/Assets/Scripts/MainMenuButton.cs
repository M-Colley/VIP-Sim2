using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuButton : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }


    // This function will be called when the Quit button is clicked
    public void QuitGame()
    {
            // If we are running in the editor, stop playing the scene
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #else
            // If we are running in a standalone build, quit the application
            Application.Quit();
    #endif
    }
}
