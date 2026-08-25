using UnityEngine;
using UnityEngine.UI;

namespace uWindowCapture
{

[RequireComponent(typeof(Image))]
public class UwcWindowListItem : MonoBehaviour 
{
    // Cached per row. WindowIconLoader caches by executable path, but resolving a process's
    // path opens and closes a handle, and this runs every frame for every window in the list.
    private int _iconProcessId = -1;
    private Texture2D _exeIcon;

    public Image image_;
    [SerializeField] public Color selected;
    [SerializeField] public Color notSelected;

    public UwcWindow window { get; set; }
    public UwcWindowList list { get; set; }
    public UwcWindowTexture windowTexture { get; set; }
    
    [SerializeField] RawImage icon;
    [SerializeField] public Text title;
    [SerializeField] Text x;
    [SerializeField] Text y;
    [SerializeField] Text z;
    [SerializeField] Text width;
    [SerializeField] Text height;
    [SerializeField] Text status;

    void Awake()
    {
        image_ = GetComponent<Image>();
        image_.color = notSelected;
    }

    void Update()
    {
        if (window == null) return;

        // Prefer the icon inside the owning process's executable. The window icon is 16 or
        // 32px and is drawn here at roughly 100, so it arrives blocky, and a good number of
        // applications register no window icon at all -- Electron ones especially -- leaving
        // uWindowCapture to substitute a placeholder that identifies nothing. Falls through
        // to the original behaviour whenever the executable cannot be read, which is normal
        // for protected and elevated processes.
        if (_iconProcessId != window.processId)
        {
            _iconProcessId = window.processId;
            _exeIcon = WindowIconLoader.GetIcon(_iconProcessId);
        }

        if (_exeIcon != null) {
            icon.texture = _exeIcon;
        } else if (!window.hasIconTexture && !window.isIconic) {
            icon.texture = window.texture;
        } else {
            icon.texture = window.iconTexture;
        }

        var windowTitle = window.title;
        title.text = string.IsNullOrEmpty(windowTitle) ? "-No Name-" : windowTitle;

        x.text = window.isMinimized ? "-" : window.x.ToString();
        y.text = window.isMinimized ? "-" : window.y.ToString();
        z.text = window.zOrder.ToString();

        width.text = window.width.ToString();
        height.text = window.height.ToString();

        status.text = 
            window.isIconic ? "Iconic" :
            window.isZoomed ? "Zoomed" :
            "-";
    }

    public void OnClick()
    {
        // By process, not by name -- and this is the SECOND copy of that title test. The
        // first lived in UwcWindowList and kept these rows out of the list entirely. With
        // that one fixed, this one produced something worse: the window appeared, and
        // clicking it did nothing whatsoever, silently, forever. Any window whose title
        // mentions VIP-Sim hit it -- a browser showing our own site above all.
        if (UwcWindowList.IsOwnWindow(window)) return;

        if (windowTexture == null) {
            list.DisableAllWindows();
            AddWindow();

            // Guarded: the questionnaire and its Firestore logger are gated off in shipping
            // builds, so this returns null there, and an unguarded call would throw on every
            // window selection -- the one action the whole application is built around.
            var logger = FindAnyObjectByType<FirestoreRESTManager>();
            if (logger != null) logger.OnProgramClick(title.text);
        } else {
            //RemoveWindow();
        }
    }

    void AddWindow()
    {
        var manager = list.windowTextureManager;
        windowTexture = manager.AddWindowTexture(window);
        image_.color = selected;
    }

    public void RemoveWindow()
    {
        var manager = list.windowTextureManager;
        manager.RemoveWindowTexture(window);
        windowTexture = null;
        image_.color = notSelected;
    }
}

}