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
                buttonObj.GetComponent<Image>().sprite = sprite1;
                
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
            if(buttonText != null)  
            buttonText.color = color2;
        }
        else
        {
            buttonImage.sprite = sprite1;
            if(buttonText != null)  
            buttonText.color = color1;
        }

        // Toggle the state
        isSprite1Active = !isSprite1Active;
    }
}
