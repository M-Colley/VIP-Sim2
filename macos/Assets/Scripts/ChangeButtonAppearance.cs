using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChangeButtonAppearance : MonoBehaviour
{
    [SerializeField] private Sprite sprite1; // First sprite
    [SerializeField] private Sprite sprite2; // Second sprite
    [SerializeField] private TextMeshProUGUI buttonText; // Reference to the button's text
    [SerializeField] private Color color1 = Color.white; // First color
    [SerializeField] private Color color2 = Color.black; // Second color

    // Tint applied to the Image itself, on top of the sprite swap.
    //
    // The sprite swap alone is not enough to read as a state change on the per-effect
    // settings gears: settingsOffBG and settingONBG differ only in shade, so the row
    // whose settings are currently open was almost indistinguishable from the fifteen
    // that were not. Both default to white, which multiplies to a no-op, so every
    // existing button keeps its current appearance until a tint is deliberately set.
    [SerializeField] private Color imageColor1 = Color.white; // tint in state 1
    [SerializeField] private Color imageColor2 = Color.white; // tint in state 2
    [SerializeField] private Button settingsButton; // Use this to enable the settings with the most current impairment

    private Image buttonImage;
    public bool isSprite1Active = true;

    void Start()
    {
        // Get the Image component of the button
        buttonImage = GetComponent<Image>();

        // Check if the button has an Image component
        if (buttonImage == null)
        {
            Debug.LogError("No Image component found on the button.");
            return;
        }



        // Set the initial sprite and text color
        buttonImage.sprite = sprite1;
        buttonImage.color = imageColor1;
        if (buttonText != null)
            buttonText.color = color1;
    }

    // Method to be called on button click
    /*
    public void SwapSpritesAndTextColor()
    {
        if (isSprite1Active)
        {
            buttonImage.sprite = sprite2;
            if (buttonText != null)
            {
                buttonText.color = color2;
            }
        }
        else
        {
            buttonImage.sprite = sprite1;
            if (buttonText != null)
            {
                buttonText.color = color1;
            }
        }

        // Toggle the state
        isSprite1Active = !isSprite1Active;
    }*/

    // Method to be called on button click
    public void SwapSpritesAndTextColor()
    {
        // Check if the button has the tag "Settings"
        if (CompareTag("Settings"))
        {
            Debug.Log("settings");
            // Find all buttons with the "Settings" tag in the scene
            GameObject[] settingsButtons = GameObject.FindGameObjectsWithTag("Settings");

            foreach (GameObject buttonObj in settingsButtons)
            {
                var otherImage = buttonObj.GetComponent<Image>();
                otherImage.sprite = sprite1;
                // The tint has to be reset alongside the sprite. Only one effect's
                // settings can be open at a time, so this loop is what deselects the
                // other fifteen gears -- leaving their colour on the selected tint
                // would mean every gear ever clicked stayed highlighted.
                otherImage.color = imageColor1;
            }
        }
        if (CompareTag("Settings"))
            isSprite1Active = true;
        else
        {
            PerformSwap();

            // PerformSwap has already flipped the state, so isSprite1Active == false now
            // means the effect was just switched ON.
            //
            // The gear used to be invoked either way, which is how a disabled effect ended
            // up with its settings still on screen: turning an effect off opened -- or left
            // open -- a panel of parameters for something that was no longer running, with
            // no way to tell they belonged to a dead effect.
            if (!isSprite1Active)
            {
                settingsButton.onClick.Invoke();
            }
            else
            {
                // Switching an effect off must NOT touch HideImpairmentSelection's enable
                // slider. That slider is the master switch for the whole settings panel,
                // not a per-effect one, so clearing it here made every effect's settings
                // vanish at once -- far worse than the problem it was meant to solve.
                //
                // What is safe, and is the actual original fault, is that the gear used to
                // be invoked on the way down as well: turning an effect off would OPEN its
                // settings. Now it only resets the gear, so the amber "settings open"
                // marker does not stay lit on a row that was just switched off.
                var gear = settingsButton != null ? settingsButton.GetComponent<ChangeButtonAppearance>() : null;
                if (gear != null) gear.ResetToIdle();
            }
            return;
        }
        PerformSwap();
    }

    /// <summary>
    /// Force this button back to its unselected appearance without toggling anything.
    /// PerformSwap flips state, which is wrong when the state is already known.
    /// </summary>
    public void ResetToIdle()
    {
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        if (buttonImage == null) return;

        buttonImage.sprite = sprite1;
        buttonImage.color = imageColor1;
        if (buttonText != null) buttonText.color = color1;
        isSprite1Active = true;
    }

    private static HideImpairmentSelection _settingsPanel;

    /// <summary>
    /// Hide the per-effect settings panel. Its visibility is driven by
    /// HideImpairmentSelection's enable slider, so that is what has to be cleared --
    /// deactivating the object directly would be undone on the next frame, since that
    /// component re-evaluates and re-applies the slider's value in Update.
    /// </summary>
    private static void CloseSettingsPanel()
    {
        if (_settingsPanel == null)
            _settingsPanel = FindFirstObjectByType<HideImpairmentSelection>(FindObjectsInactive.Include);

        if (_settingsPanel != null) _settingsPanel.CloseSettings();
    }

    // Helper method to perform the swap
    private void PerformSwap()
    {
        if (isSprite1Active)
        {
            buttonImage.sprite = sprite2;
            buttonImage.color = imageColor2;
            if(buttonText != null)
            buttonText.color = color2;
        }
        else
        {
            buttonImage.sprite = sprite1;
            buttonImage.color = imageColor1;
            if(buttonText != null)
            buttonText.color = color1;
        }

        // Toggle the state
        isSprite1Active = !isSprite1Active;
    }
}
