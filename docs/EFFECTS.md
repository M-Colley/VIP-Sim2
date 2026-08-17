# Effects reference

The eighteen symptoms VIP-Sim simulates, the clinical term for each, and what it does to
vision. Ordered as they appear in the panel — grouped so related symptoms sit together
rather than in the order they happened to be added.

The **Clinical term** column is the important one for anything published: the plain-language
labels exist so a designer can find the right effect, but a paper should name the condition.

## Central vision

| Label | Clinical term | What it does |
|---|---|---|
| Vision Loss, Central | Central scotoma | A blind or blurred patch in the middle of vision. Reading, faces and fine detail go; the periphery stays usable. |
| Central Dark Spot | Foveal darkness | Darkening at the precise centre of gaze, moving with the eye. |
| Detail Loss | Reduced acuity | Fine detail is lost everywhere without the image blurring uniformly. |

## Peripheral vision

| Label | Clinical term | What it does |
|---|---|---|
| Vision Loss, Peripheral | Peripheral scotoma | Loss around the edges, progressing inwards. Central detail is preserved, so it is easily unnoticed — characteristic of glaucoma. |
| In-Filling | Perceptual filling-in | The brain completes missing regions with surrounding texture, so gaps are not perceived as gaps. |

## Distortion

| Label | Clinical term | What it does |
|---|---|---|
| Wavy Distortion | Metamorphopsia | Straight lines bend or ripple. A common early sign of macular disease. |
| Wavy Distortion II | Metamorphopsia (variant) | A second implementation with a different distortion field. |
| Distortion | Geometric distortion | General warping of the image. |

## Blur and refraction

| Label | Clinical term | What it does |
|---|---|---|
| Farsightedness | Hyperopia | Near objects are out of focus; distance is clearer. |
| Cataract | Cataract | Clouding of the lens. Hazy vision, dulled colour, light scattering into glare. |

## Colour and contrast

| Label | Clinical term | What it does |
|---|---|---|
| Color Vision Deficiency | Dyschromatopsia | Reduced ability to distinguish colours, most often red from green. Sharpness is unaffected. |
| Contrast Sensitivity | Reduced contrast sensitivity | Low-contrast edges become hard to separate; text on a tinted background disappears first. |

## Light

| Label | Clinical term | What it does |
|---|---|---|
| Glare Vision/Photophobia | Photophobia | Bright regions bloom and become painful to look at. |

## Eye movement

| Label | Clinical term | What it does |
|---|---|---|
| Eye Tremor | Nystagmus | Involuntary rhythmic eye movement; the image drifts and jerks. |
| Double Vision | Diplopia | Two offset copies of the image. Monocular mode displaces one copy against the other. |

## Transient and floating

| Label | Clinical term | What it does |
|---|---|---|
| Retinopathy/Floaters | Vitreous floaters | Dark shapes drifting across vision, moving with the eye and settling slowly. |
| Flickering Specks | Photopsia | Small flickering points of light. |
| Visual Aura | Teichopsia | A shimmering, often geometric disturbance that expands across the field, associated with migraine. |

---

## Renamed labels

The UI used clinical vocabulary until `7b66e74`. Correct, but opaque to the designers the
tool is aimed at — and nobody can choose to simulate a condition they cannot recognise from
its name. `Metamorphopsia2` was a developer name that had reached the interface outright.

The clinical terms are unchanged in code, in the shaders and in this document. Only the
button labels changed.

| Before | After |
|---|---|
| Teichopsia | Visual Aura |
| Metamorphopsia | Wavy Distortion |
| Metamorphopsia2 | Wavy Distortion II |
| Hyperopia | Farsightedness |
| Nystagmus | Eye Tremor |
| Foveal Darkness | Central Dark Spot |
| Flickering Stars | Flickering Specks |

Left alone because they were already plain, and renaming would have lost precision for no
gain: Cataract, Double Vision, Distortion, Detail Loss, Contrast Sensitivity, In-Filling,
Color Vision Deficiency, Retinopathy/Floaters, Glare Vision/Photophobia, and both Vision
Loss entries.

Replacements were kept close to the original length on purpose. The buttons are a fixed
230px with auto-sizing text, so a longer name shrinks the type rather than wrapping — which
would trade one legibility problem for another.

---

## A caution on interpretation

Each effect is an approximation of one symptom, not a model of a diagnosis. Real conditions
combine several, vary enormously between individuals, and change over time. The presets in
`VipSimPresets` group effects by condition, but their severities are **uncalibrated starting
points** rather than validated stimuli.

Anything published from this should state which effects and values were used, and how they
were arrived at.
