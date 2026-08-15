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
            settingsButton.onClick.Invoke();
            return;
        }
        PerformSwap();
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
