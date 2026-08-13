
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleScript : MonoBehaviour
{


    public Image image;
    // The sprite to check against
    public Sprite enableOnBGSprite;

    private List<GameObject> lastObjects = new List<GameObject>();

    public void Awake()
    {
    }
    // Function to toggle the active state of a GameObject
    public void ToggleActiveState(GameObject obj)
    {
        // Check if the GameObject is not null
        if (obj != null)
        {
            // Toggle the active state
            obj.SetActive(!obj.activeSelf);
            Debug.Log($"{obj.name} active state toggled to {obj.activeSelf}");
        }
        else
        {
            Debug.LogWarning("The provided GameObject is null.");
        }
    }

    public void DisableAllMonoBehaviours(){
        GameObject[] enableObjects = GameObject.FindGameObjectsWithTag("Enable");
        lastObjects.Clear();

        foreach(GameObject obj in enableObjects){
            Image image = obj.GetComponent<Image>();

            if (image != null && image.sprite == enableOnBGSprite){
                Button button = obj.GetComponent<Button>();
                
                if(button != null){
                    lastObjects.Add(obj);
                    button.onClick.Invoke();
                }
            }
        }
    }

    // Function to toggle the enabled state of a script (MonoBehaviour)
    public void ToggleMonoBehaviour(MonoBehaviour script)
    {
        // Check if the script is not null
        if (script != null)
        {
            // Toggle the enabled state
            script.enabled = !script.enabled;
            Debug.Log($"{script.GetType().Name} script enabled state toggled to {script.enabled}");
        }
        else
        {
            Debug.LogWarning("The provided script is null.");
        }
    }

    // This function toggles all MonoBehaviour scripts on the provided GameObject
    public void ToggleAllMonoBehaviours(GameObject target)
    {
        /*
        // Get all MonoBehaviour components attached to the target GameObject
        MonoBehaviour[] monoBehaviours = target.GetComponents<MonoBehaviour>();

        // Iterate through each MonoBehaviour and toggle its enabled state
        foreach (MonoBehaviour monoBehaviour in monoBehaviours)
        {
            if (monoBehaviour.enabled)
            {
                monoBehaviour.enabled = !monoBehaviour.enabled;
            }   

        }*/

        // Find all GameObjects with the tag "Enable"
        GameObject[] enableObjects = GameObject.FindGameObjectsWithTag("Enable");

        foreach (GameObject obj in enableObjects)
        {
            // Get the Image component from the GameObject
            Image image = obj.GetComponent<Image>();

            if (image != null && image.sprite == enableOnBGSprite)
            {
                // Get the Button component from the GameObject
                Button button = obj.GetComponent<Button>();

                if (button != null)
                {
                    // Invoke the button's onClick event
                    button.onClick.Invoke();
                }
                else
                {
                    Debug.LogWarning($"No Button component found on {obj.name}");
                }
            }
            else
            {
                Debug.LogWarning($"No Image component found or the sprite doesn't match on {obj.name}");
            }
        }
    }

    public void EnableAllMonoBehaviors(){
        foreach(GameObject obj in lastObjects){
            Button button = obj.GetComponent<Button>();

            if(button != null){
                button.onClick.Invoke();
            } else {
                
            }
        }
    }

    public void setFillGreenColor()
    {
        image.color = new Color(255f / 255f, 169f / 255f, 62f / 255f, 255f / 255f);
    }

    public void setFillGreyColor()
    {
        image.color = Color.grey;
    }
}
