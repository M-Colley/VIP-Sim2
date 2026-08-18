using UnityEngine;

/// <summary>
/// Shared visual language for VIP-Sim's IMGUI surfaces (the symptom reference and the
/// first-run tutorial).
///
/// Those panels were drawn with Unity's built-in IMGUI skin, which is a developer tool
/// from another decade: hard grey boxes, 1px chiselled borders, fixed 1080p metrics, no
/// type hierarchy. Fine for a research prototype, wrong for something sold. Everything
/// here is generated procedurally at runtime -- no textures to import, no scene surgery,
/// no per-platform asset drift, which are the three things that have repeatedly broken
/// this UI when it was changed by hand.
///
/// Two rules the rest of the app already follows and this must not break:
///   - every metric scales with the display; the panels are used on 4K desktops and on
///     laptop screens, and IMGUI's defaults are authored for 1080p;
///   - alpha is load-bearing. VIP-Sim composites through the desktop compositor, so a
///     panel is only visible where it writes non-zero alpha. Backgrounds here are opaque
///     on purpose -- a translucent panel over an arbitrary desktop is unreadable, and a
///     transparent one is invisible.
/// </summary>
public static class VipSimSkin
{
    /// <summary>VIP-Sim's accent, matching the toolbar and the effect list.</summary>
    public static Color Accent { get; private set; } = new Color(1f, 0.62f, 0.16f);

    /// <summary>Keyboard focus indicator. Deliberately the loudest colour in the palette.</summary>
    public static Color Focus { get; private set; } = new Color(0.30f, 0.80f, 1f);

    /// <summary>
    /// Palette colours as rich-text hex, for the inline &lt;color&gt; tags in labels.
    ///
    /// These exist because hardcoding those tags silently defeats high-contrast mode: the
    /// eyebrow stayed orange and the clinical terms stayed 40% grey on pure black, which
    /// measures 3.66:1 and fails the 4.5:1 minimum for body text. A tag has no idea which
    /// palette is active unless it is told.
    /// </summary>
    public static string AccentHex { get; private set; } = "FF9E29";

    /// <summary>Secondary text as rich-text hex, including its alpha.</summary>
    public static string MutedHex { get; private set; } = "FFFFFF66";

    private static Color PanelFill = new Color(0.086f, 0.090f, 0.106f, 0.98f);
    private static Color PanelEdge = new Color(1f, 1f, 1f, 0.10f);
    private static Color CardFill = new Color(1f, 1f, 1f, 0.045f);
    private static Color ButtonFill = new Color(1f, 1f, 1f, 0.08f);
    private static Color ButtonHover = new Color(1f, 1f, 1f, 0.15f);
    private static Color TextBright = new Color(0.96f, 0.96f, 0.97f);
    private static Color TextMuted = new Color(0.96f, 0.96f, 0.97f, 0.55f);

    private const string ScalePref = "vipsim.a11y.textscale";
    private const string ContrastPref = "vipsim.a11y.highcontrast";

    private static float _builtFor = -1f;
    private static bool _builtHighContrast;

    /// <summary>
    /// User text-size multiplier, 1.0 = default. Persisted.
    ///
    /// Unity exposes no reliable per-platform "OS text scale" value, and a vision tool that
    /// cannot make its own text bigger is indefensible -- so the size is put in the user's
    /// hands directly rather than inferred.
    /// </summary>
    public static float UserScale
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(ScalePref, 1f), 0.8f, 2.5f);
        set
        {
            PlayerPrefs.SetFloat(ScalePref, Mathf.Clamp(value, 0.8f, 2.5f));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// High-contrast palette: pure black ground, pure white text, yellow accent. Persisted.
    /// Every foreground pair exceeds 7:1, i.e. WCAG AAA rather than AA.
    /// </summary>
    public static bool HighContrast
    {
        get => PlayerPrefs.GetInt(ContrastPref, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ContrastPref, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static GUIStyle Panel { get; private set; }
    public static GUIStyle Card { get; private set; }
    public static GUIStyle Title { get; private set; }
    public static GUIStyle Heading { get; private set; }
    public static GUIStyle Body { get; private set; }
    public static GUIStyle Term { get; private set; }
    public static GUIStyle Muted { get; private set; }
    public static GUIStyle Primary { get; private set; }
    public static GUIStyle Secondary { get; private set; }

    /// <summary>Display scale (1.0 at 1080p, 2.0 at 4K) times the user's text-size setting.</summary>
    public static float Scale => Mathf.Max(1f, Screen.height / 1080f) * UserScale;

    /// <summary>Standard control height, display-scaled.</summary>
    public static float ControlHeight => Mathf.Round(44f * Scale);

    /// <summary>
    /// Build the styles if they are missing or the display has changed size. Cheap to
    /// call from OnGUI; the work happens once per resolution.
    /// </summary>
    public static void Ensure()
    {
        bool hc = HighContrast;
        if (Panel != null && Mathf.Approximately(_builtFor, Scale) && _builtHighContrast == hc) return;
        _builtFor = Scale;
        _builtHighContrast = hc;
        ApplyPalette(hc);
        float s = Scale;

        int Px(float v) => Mathf.Max(1, Mathf.RoundToInt(v * s));

        Panel = new GUIStyle
        {
            normal = { background = RoundedRect(PanelFill, PanelEdge, 1f) },
            border = new RectOffset(14, 14, 14, 14),
            padding = new RectOffset(Px(30), Px(30), Px(26), Px(26)),
        };

        Card = new GUIStyle
        {
            normal = { background = RoundedRect(CardFill, Color.clear, 0f) },
            border = new RectOffset(14, 14, 14, 14),
            padding = new RectOffset(Px(18), Px(18), Px(14), Px(14)),
        };

        Title = new GUIStyle
        {
            fontSize = Px(30),
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            richText = true,
            normal = { textColor = TextBright },
        };

        Heading = new GUIStyle
        {
            fontSize = Px(19),
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            richText = true,
            normal = { textColor = Accent },
            margin = new RectOffset(0, 0, Px(16), Px(4)),
        };

        Body = new GUIStyle
        {
            fontSize = Px(17),
            wordWrap = true,
            richText = true,
            normal = { textColor = TextMuted },
        };

        Term = new GUIStyle
        {
            fontSize = Px(18),
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            richText = true,
            normal = { textColor = TextBright },
        };

        Muted = new GUIStyle
        {
            fontSize = Px(14),
            wordWrap = true,
            richText = true,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = TextMuted },
        };

        // Primary: filled accent. Used for the one action a panel most expects.
        Primary = new GUIStyle
        {
            fontSize = Px(17),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(Px(18), Px(18), 0, 0),
            border = new RectOffset(14, 14, 14, 14),
            normal = { background = RoundedRect(Accent, Color.clear, 0f), textColor = new Color(0.07f, 0.07f, 0.08f) },
            hover = { background = RoundedRect(Lighten(Accent, 0.12f), Color.clear, 0f), textColor = new Color(0.07f, 0.07f, 0.08f) },
            active = { background = RoundedRect(Lighten(Accent, -0.12f), Color.clear, 0f), textColor = new Color(0.07f, 0.07f, 0.08f) },
        };

        // Secondary: quiet surface that brightens on hover -- the same ColorTint idiom the
        // toolbar buttons use, so the two halves of the app feel like one product.
        Secondary = new GUIStyle
        {
            fontSize = Px(17),
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(Px(18), Px(18), 0, 0),
            border = new RectOffset(14, 14, 14, 14),
            normal = { background = RoundedRect(ButtonFill, PanelEdge, 1f), textColor = TextBright },
            hover = { background = RoundedRect(ButtonHover, new Color(1f, 1f, 1f, 0.22f), 1f), textColor = Color.white },
            active = { background = RoundedRect(ButtonHover, Accent, 1.5f), textColor = Color.white },
        };
    }

    /// <summary>
    /// Swap the palette. The high-contrast set is not a darker version of the default: it
    /// drops translucency entirely, because a semi-transparent surface has no defined
    /// contrast ratio against an arbitrary desktop, and moves the accent to yellow, which
    /// stays distinguishable under every common form of colour vision deficiency.
    /// </summary>
    private static void ApplyPalette(bool highContrast)
    {
        if (highContrast)
        {
            PanelFill   = new Color(0f, 0f, 0f, 1f);
            PanelEdge   = new Color(1f, 1f, 1f, 1f);
            CardFill    = new Color(1f, 1f, 1f, 0.12f);
            ButtonFill  = new Color(0f, 0f, 0f, 1f);
            ButtonHover = new Color(1f, 1f, 1f, 0.25f);
            TextBright  = Color.white;
            TextMuted   = Color.white;
            Accent      = new Color(1f, 0.92f, 0.20f);
            Focus       = new Color(0.30f, 0.90f, 1f);
            AccentHex   = "FFEB33";
            MutedHex    = "FFFFFF";   // fully opaque: 40% white on black is only 3.66:1
        }
        else
        {
            PanelFill   = new Color(0.086f, 0.090f, 0.106f, 0.98f);
            PanelEdge   = new Color(1f, 1f, 1f, 0.10f);
            CardFill    = new Color(1f, 1f, 1f, 0.045f);
            ButtonFill  = new Color(1f, 1f, 1f, 0.08f);
            ButtonHover = new Color(1f, 1f, 1f, 0.15f);
            TextBright  = new Color(0.96f, 0.96f, 0.97f);
            TextMuted   = new Color(0.96f, 0.96f, 0.97f, 0.55f);
            Accent      = new Color(1f, 0.62f, 0.16f);
            Focus       = new Color(0.30f, 0.80f, 1f);
            AccentHex   = "FF9E29";
            MutedHex    = "FFFFFF66";
        }
    }

    /// <summary>Draw a focus ring around a screen rect, in IMGUI coordinates.</summary>
    public static void FocusRing(Rect r, float thickness)
    {
        float t = Mathf.Max(2f, thickness);
        // Dark backing first, so the ring stays visible on a light UI as well as a dark one.
        var dark = new Color(0f, 0f, 0f, 0.85f);
        Fill(new Rect(r.x - t * 2, r.y - t * 2, r.width + t * 4, t), dark);
        Fill(new Rect(r.x - t * 2, r.yMax + t, r.width + t * 4, t), dark);
        Fill(new Rect(r.x - t * 2, r.y - t * 2, t, r.height + t * 4), dark);
        Fill(new Rect(r.xMax + t, r.y - t * 2, t, r.height + t * 4), dark);

        Fill(new Rect(r.x - t, r.y - t, r.width + t * 2, t), Focus);
        Fill(new Rect(r.x - t, r.yMax, r.width + t * 2, t), Focus);
        Fill(new Rect(r.x - t, r.y - t, t, r.height + t * 2), Focus);
        Fill(new Rect(r.xMax, r.y - t, t, r.height + t * 2), Focus);
    }

    /// <summary>Fill a rect with a flat colour, for dimmed backdrops and hairlines.</summary>
    public static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    /// <summary>A one-pixel separator, display-scaled so it stays visible at 4K.</summary>
    public static void Separator(float width)
    {
        var r = GUILayoutUtility.GetRect(width, Mathf.Max(1f, Scale), GUILayout.ExpandWidth(true));
        Fill(r, new Color(1f, 1f, 1f, 0.09f));
    }

    private static Color Lighten(Color c, float amount)
    {
        return new Color(
            Mathf.Clamp01(c.r + amount),
            Mathf.Clamp01(c.g + amount),
            Mathf.Clamp01(c.b + amount),
            c.a);
    }

    /// <summary>
    /// A rounded rectangle for 9-slicing.
    ///
    /// Generated at a fixed 32x32 with a 14px corner: IMGUI stretches the middle and
    /// leaves the corners alone (that is what GUIStyle.border means), so one small
    /// texture serves every button and panel size without distortion. Coverage is
    /// computed from the distance to the corner circle and smoothed across one pixel,
    /// which is what stops the corners looking like staircases.
    /// </summary>
    private static Texture2D RoundedRect(Color fill, Color edge, float edgeWidth)
    {
        const int Size = 32;
        const float Radius = 14f;

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var px = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // Distance outside the rounded rect, in pixels: negative inside.
                float dx = Mathf.Max(Radius - (x + 0.5f), (x + 0.5f) - (Size - Radius));
                float dy = Mathf.Max(Radius - (y + 0.5f), (y + 0.5f) - (Size - Radius));
                float d;
                if (dx > 0f && dy > 0f) d = Mathf.Sqrt(dx * dx + dy * dy) - Radius;
                else                    d = Mathf.Max(dx, dy) - Radius;

                float inside = Mathf.Clamp01(0.5f - d);           // 1 inside, 0 outside
                var c = fill;

                if (edgeWidth > 0f && edge.a > 0f)
                {
                    // Ring of the requested width just inside the outline.
                    float ring = Mathf.Clamp01(0.5f - Mathf.Abs(d + edgeWidth * 0.5f) + edgeWidth * 0.5f);
                    c = Color.Lerp(fill, edge, ring * edge.a);
                    c.a = Mathf.Max(fill.a, ring * edge.a);
                }

                c.a *= inside;
                px[y * Size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
