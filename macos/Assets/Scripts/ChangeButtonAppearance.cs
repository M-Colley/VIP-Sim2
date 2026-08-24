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
        // Set BOTH records, not just the sprite.
        //
        // This used to assign the sprite and leave isSprite1Active at whatever the scene had
        // serialized. The two are read by different code -- the master switch reads the
        // sprite, the gear logic reads the flag -- so a disagreement between them is a state
        // no part of the UI is written to handle.
        //
        // Guarded, because Start runs when the row is FIRST SHOWN, which can be long after a
        // profile has already set its real state: the effect list is hidden until a window is
        // picked and the simulation switched on, and the Load button works throughout. Without
        // the guard, revealing the list would quietly reset every row to off while the effects
        // it just loaded went on running.
        if (!_stateSet) SetState(false);
    }

    // Whether anything has told this button its real state yet. See Start.
    private bool _stateSet;

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
                var gear = settingsButton != null ? settingsButton.GetComponent<ChangeButtonAppearance>() : null;
                if (gear != null)
                {
                    gear.ResetToIdle();

                    // Only close the panel if the effect being switched off is the one
                    // whose parameters are actually showing. Without that test this
                    // hides the panel whenever ANY effect is turned off, which is what
                    // made every setting disappear at once on the previous attempt.
                    if (_openGear == gear)
                    {
                        _openGear = null;
                        SetSettingsPanel(false);
                    }
                }
            }
            return;
        }
        PerformSwap();

        // Reaching here means a gear was pressed, so this row's settings are now the ones
        // on display. Recording which gear that is, is the missing piece the UI never had:
        // it knew a global open/closed state and which gear was last clicked, but nothing
        // connected the two, so "was the effect I just switched off the one being shown?"
        // could not be answered.
        if (CompareTag("Settings"))
        {
            _openGear = this;
            SetSettingsPanel(true);
        }
    }

    // The gear whose settings are currently displayed. A single static is enough because
    // only one can be open at a time -- the deselect loop above enforces that.
    private static ChangeButtonAppearance _openGear;

    /// <summary>
    /// Whether any effect currently has its settings panel open.
    ///
    /// This is the piece of state the UI was missing. It previously tracked a global
    /// open/closed flag AND which gear was last clicked, with nothing connecting them, so
    /// "are the parameters on screen the ones belonging to a selected effect?" had no
    /// answer. Two bugs came out of that gap in a single session: parameters lingering for
    /// an effect that had been switched off, and -- when the first fix reached for the
    /// global flag instead -- every effect's settings vanishing at once.
    ///
    /// The panel derives from this rather than being pushed at, so the two cannot disagree.
    /// </summary>
    public static bool HasOpenSettings => _openGear != null;

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

    /// <summary>
    /// Force this button to a known state, without toggling and without opening anything.
    ///
    /// PerformSwap flips, which is only correct when a human pressed the button. Loading a
    /// profile sets state that was decided elsewhere, and it sets several at once -- so it
    /// needs to say "this row is on" rather than "flip this row", and it must not fire the
    /// gear the way a click does, or six effects in a profile would each try to open their
    /// parameters and the last one would win.
    ///
    /// isSprite1Active is inverted by construction: sprite1 is the OFF background.
    /// </summary>
    public void SetState(bool on)
    {
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        if (buttonImage == null) return;

        buttonImage.sprite = on ? sprite2 : sprite1;
        buttonImage.color = on ? imageColor2 : imageColor1;
        if (buttonText != null) buttonText.color = on ? color2 : color1;
        isSprite1Active = !on;
        _stateSet = true;
    }

    /// <summary>
    /// Close whichever effect's parameters are on screen, whatever they are.
    ///
    /// For use after a bulk change of state, where the panel may now be showing the
    /// parameters of an effect that has just been switched off -- the exact thing the
    /// _openGear bookkeeping exists to prevent when a single effect is toggled. This is
    /// unconditional rather than selective, so it cannot reintroduce either of the two
    /// bugs that logic was written for: it never leaves a dead effect's parameters up, and
    /// it never has to decide which of several rows owns the panel.
    /// </summary>
    public static void CloseOpenSettings()
    {
        if (_openGear == null) return;
        _openGear.ResetToIdle();
        _openGear = null;
        SetSettingsPanel(false);
    }

    /// <summary>
    /// Show or hide the panel of parameters for the selected effect.
    ///
    /// This used to look up a HideImpairmentSelection and ask it to move a slider. There are
    /// two of those components and they share one slider -- the master Enable switch -- so
    /// closing a panel switched the simulation off and hid the effect list. The panel now
    /// has state of its own and this just sets it.
    /// </summary>
    private static void SetSettingsPanel(bool open)
    {
        HideImpairmentSelection.SetSettingsOpen(open);
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
