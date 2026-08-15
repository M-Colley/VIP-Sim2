using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Condition presets: named groups of effects that together approximate a real diagnosis.
///
/// The effect list is eighteen individually-toggled symptoms. Someone who knows they want
/// to see "what glaucoma looks like" has to already know that glaucoma presents as
/// peripheral field loss, which is precisely the knowledge the tool exists to convey.
/// Presets close that gap.
///
/// WHAT THESE VALUES ARE, AND ARE NOT
///
/// The effect GROUPINGS below reflect the uncontroversial, well-documented presentation of
/// each condition -- macular degeneration affects central vision, glaucoma the periphery,
/// cataract scatters light and washes out contrast. Those associations are not in dispute
/// and are safe to encode.
///
/// The SEVERITIES are not clinically calibrated. They are moderate starting points chosen
/// so each preset is visibly distinct, and they carry no claim about how any individual
/// experiences the condition. Severity in every one of these varies enormously between
/// people and over time, and a simulator that presents one value as "what X looks like"
/// misleads exactly the audience it is meant to inform.
///
/// So: treat a preset as a starting point to calibrate against your own participants and
/// clinical input, not as a validated stimulus. Anything published from this should say
/// which values were used and how they were arrived at.
/// </summary>
[CreateAssetMenu(fileName = "VipSimPresets", menuName = "VIP-Sim/Condition Presets")]
public class VipSimPresets : ScriptableObject
{
    [Serializable]
    public class EffectSetting
    {
        [Tooltip("Effect GameObject name in the VerticalMenu, e.g. 'VisionLossC'.")]
        public string effectName;

        [Range(0f, 1f)]
        [Tooltip("Starting severity. NOT clinically calibrated -- see the class summary.")]
        public float severity = 0.5f;
    }

    [Serializable]
    public class Preset
    {
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("Shown to the user. Say what the condition does to vision, in plain words.")]
        public string description;

        public EffectSetting[] effects;
    }

    public Preset[] presets = DefaultPresets();

    /// <summary>
    /// Defaults, so the asset is useful the moment it is created rather than empty.
    /// Groupings follow each condition's documented presentation; severities are
    /// deliberately moderate and deliberately arbitrary.
    /// </summary>
    public static Preset[] DefaultPresets()
    {
        return new[]
        {
            new Preset
            {
                displayName = "Macular degeneration",
                description = "Loss of central vision. Reading, faces and fine detail are " +
                              "affected while peripheral vision remains usable. Straight " +
                              "lines can appear bent.",
                effects = new[]
                {
                    new EffectSetting { effectName = "VisionLossC",     severity = 0.6f },
                    new EffectSetting { effectName = "Metamorphopsia",  severity = 0.4f },
                    new EffectSetting { effectName = "DetailLoss",      severity = 0.5f },
                }
            },
            new Preset
            {
                displayName = "Glaucoma",
                description = "Loss of peripheral vision, progressing inwards. Central " +
                              "detail is preserved until late, so it is easily missed.",
                effects = new[]
                {
                    new EffectSetting { effectName = "Vision loss, peripheral", severity = 0.6f },
                    new EffectSetting { effectName = "Contrast Sensitivity",    severity = 0.3f },
                }
            },
            new Preset
            {
                displayName = "Cataract",
                description = "Clouding of the lens. Vision is hazy, colours dull, and " +
                              "bright lights scatter into glare.",
                effects = new[]
                {
                    new EffectSetting { effectName = "Cataract",                 severity = 0.5f },
                    new EffectSetting { effectName = "Glare Vision/Photophobia", severity = 0.5f },
                    new EffectSetting { effectName = "Contrast Sensitivity",     severity = 0.4f },
                }
            },
            new Preset
            {
                displayName = "Diabetic retinopathy",
                description = "Patchy loss scattered across the field, often with floaters " +
                              "drifting through vision.",
                effects = new[]
                {
                    new EffectSetting { effectName = "Retinopathy/Floaters", severity = 0.5f },
                    new EffectSetting { effectName = "In-Filling",           severity = 0.4f },
                    new EffectSetting { effectName = "Contrast Sensitivity", severity = 0.3f },
                }
            },
            new Preset
            {
                displayName = "Colour blindness",
                description = "Reduced ability to distinguish certain colours, most often " +
                              "reds from greens. Detail and sharpness are unaffected.",
                effects = new[]
                {
                    new EffectSetting { effectName = "Color vision deficiency", severity = 1.0f },
                }
            },
        };
    }

    /// <summary>
    /// Find a preset by display name, case-insensitively. Returns null when absent rather
    /// than throwing: presets are user-editable data, and a renamed entry should not take
    /// the application down mid-session.
    /// </summary>
    public Preset Find(string displayName)
    {
        if (presets == null || string.IsNullOrEmpty(displayName)) return null;
        return presets.FirstOrDefault(p =>
            p != null && string.Equals(p.displayName, displayName, StringComparison.OrdinalIgnoreCase));
    }
}
