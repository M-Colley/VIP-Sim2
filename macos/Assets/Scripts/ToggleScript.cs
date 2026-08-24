
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

            // Both records must agree before this presses anything.
            //
            // The sprite alone used to decide it, and the sprite is only one of two places a
            // row's state lives -- ChangeButtonAppearance.isSprite1Active is the other, and
            // Start() forces the sprite without touching the flag, so there is a window in
            // which they disagree. Pressing a row in that state switched on a symptom the
            // user never chose: a fresh session, one click to pick a window, and the log
            // showed enabled(1) myFieldLoss with the row lit and its parameters open.
            //
            // Acting only on agreement makes a half-state inert instead of destructive, and
            // says so, which is what a report of "the state machine gets confused" needs.
            var appearance = obj.GetComponent<ChangeButtonAppearance>();
            bool spriteSaysOn = image != null && image.sprite == enableOnBGSprite;
            bool flagSaysOn = appearance != null && !appearance.isSprite1Active;

            if (spriteSaysOn != flagSaysOn)
            {
                Debug.LogWarning($"[ToggleScript] {obj.name} is in a half-state " +
                                 $"(sprite says {(spriteSaysOn ? "on" : "off")}, flag says " +
                                 $"{(flagSaysOn ? "on" : "off")}); leaving it alone.");
                continue;
            }

            if (spriteSaysOn && flagSaysOn)
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
            // No else. This branch is every effect that is currently switched OFF, which
            // is the normal state of most of them -- it warned seventeen times per toggle
            // and buried the one real error in the same log.

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
