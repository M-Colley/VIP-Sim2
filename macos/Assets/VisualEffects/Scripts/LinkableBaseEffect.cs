using UnityEngine;

namespace VisSim
{
    /// <summary>
    /// Base class for VIP-Sim's symptom effects.
    ///
    /// This used to keep two copies of every effect in step -- one per eye -- from the
    /// FOVE stereo rig the project was originally built on. VIP-Sim is monoscopic: it
    /// composites onto a desktop, there is no second eye, and the machinery cost far more
    /// than the duplication it maintained.
    ///
    /// What it did: on enable, each instance searched for its opposite-eye counterpart by
    /// tag, cached every [Linkable] field by reflection, and copied those fields to the
    /// twin every frame. If exactly one counterpart could not be found it logged an error
    /// and set `this.enabled = false`.
    ///
    /// That last line is why disabling the RightEye object appeared to remove the overlay:
    /// not because that camera was rendering it, but because every remaining effect then
    /// failed to find its twin and switched ITSELF off. Measurement settled the rendering
    /// question separately -- the overlay's alpha comes from the captured window content,
    /// not from either camera's clear, both of which clear at alpha 0.
    ///
    /// Removing the lookup is therefore the prerequisite for deleting the second camera,
    /// and had to come first.
    ///
    /// `LinkEyes` and `isLeftEye` are deliberately kept. Both are serialized in the scene
    /// and `isLeftEye` is read by subclasses -- myDoubleVision mirrors its displacement on
    /// it -- so removing them would mean touching all nineteen subclasses and dirtying
    /// every effect object for no behavioural gain. With one eye, `isLeftEye` is simply
    /// always true, which is the correct answer rather than a placeholder.
    /// </summary>
    [HelpURL("http://http://www.ucl.ac.uk/~smgxprj")]
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    abstract public class LinkableBaseEffect : BaseEffect
    {
        protected abstract void OnUpdate();
        protected abstract override void OnRenderImage(RenderTexture source, RenderTexture destination);

        // Retained for scene serialization; no longer does anything. Kept rather than
        // removed so existing scenes and prefabs do not lose the field on deserialize.
        public bool LinkEyes = true;

        // Always true now. Subclasses branch on it for eye-specific offsets; with a single
        // eye the left-eye branch is the one that should run.
        protected bool isLeftEye = true;

        public void OnEnable()
        {
            // Force the material to be created. The property getter builds it lazily, and
            // several subclasses assume it exists by the time OnUpdate first runs.
            Material.GetType();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        public void Update()
        {
            OnUpdate();
        }

        protected override string GetShaderName()
        {
            return "Hidden/VisSim/LinkableBaseEffect (this should be overriden)";
        }
    }
}

/// Attribute marking fields that were synchronised between eyes. The synchronisation is
/// gone, but the attribute is still applied throughout the effect subclasses, so the type
/// has to remain for them to compile. Harmless: nothing reads it any more.
public class LinkableAttribute : System.Attribute
{
    public LinkableAttribute()
    {
    }
}
