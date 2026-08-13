/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System;
using System.Runtime.InteropServices;
using System.Collections;
using UnityEngine;

public class TransparentWindow : MonoBehaviour {

    [DllImport("user32.dll")]
    public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern int SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    public Camera maincam;
    public RectTransform canvasRectTransform;
    public RectTransform panelRectTransform;

    private bool _lastClickthrough;
    private bool missingWindowHandleLogged;

    private struct MARGINS {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    const int GWL_EXSTYLE = -20;

    const uint WS_EX_LAYERED = 0x00080000;
    const uint WS_EX_TRANSPARENT = 0x00000020;

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    const uint LWA_COLORKEY = 0x00000000;

    private IntPtr hWnd;

    private void Start() {
        //MessageBox(new IntPtr(0), "Hello World!", "Hello Dialog", 0);

#if !UNITY_EDITOR
        hWnd = GetActiveWindow();

        if (hWnd != IntPtr.Zero)
        {
            MARGINS margins = new MARGINS { cxLeftWidth = -1 };
            DwmExtendFrameIntoClientArea(hWnd, ref margins);

            SetClickthrough(true);
            //SetLayeredWindowAttributes(hWnd, 0, 0, LWA_COLORKEY);

            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, 0);
            _lastClickthrough = true;
        }
        else
        {
            Debug.LogWarning("TransparentWindow could not retrieve the native window handle. Clickthrough will be disabled.", this);
            _lastClickthrough = false;
        }
#else
        _lastClickthrough = false;
#endif

        Application.runInBackground = true;
    }

    // Set while a modal panel (e.g. the end-of-session questionnaire) is open, so
    // the overlay stops being click-through and the panel can actually be used.
    //
    // These exist on the Windows TransparentWindow but were missing here, which is
    // a plain API divergence between two copies of the same class: any shared code
    // calling them compiled on Windows and failed on macOS.
    private bool feedbackState = false;

    public void enableFeedbackState()
    {
        feedbackState = true;
    }

    public void disableFeedbackState()
    {
        feedbackState = false;
    }

    private void Update() {
        //SetClickthrough(Physics2D.OverlapPoint(GetMouseWorldPosition()) == null);
        bool clickthrough = IsCoordinateOutsidePanel();
        if (feedbackState)
        {
            clickthrough = false;
        }
        if (clickthrough != _lastClickthrough)
        {
            SetClickthrough(clickthrough);
            _lastClickthrough = clickthrough;
        }


    }

    private void SetClickthrough(bool clickthrough) {


#if UNITY_EDITOR
        _lastClickthrough = clickthrough;
        return;
#else
        if (hWnd == IntPtr.Zero)
        {
            if (!missingWindowHandleLogged)
            {
                Debug.LogWarning("TransparentWindow does not have a valid window handle, unable to update clickthrough state.", this);
                missingWindowHandleLogged = true;
            }
            return;
        }

        missingWindowHandleLogged = false;

        if (clickthrough) {
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        } else {
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED);
        }
#endif
    }

    // Get Mouse Position in World with Z = 0f
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 vec = GetMouseWorldPositionWithZ(Input.mousePosition, maincam);
        vec.z = 0f;
        return vec;
    }
    public static Vector3 GetMouseWorldPositionWithZ()
    {
        return GetMouseWorldPositionWithZ(Input.mousePosition, Camera.main);
    }
    public static Vector3 GetMouseWorldPositionWithZ(Camera worldCamera)
    {
        return GetMouseWorldPositionWithZ(Input.mousePosition, worldCamera);
    }
    public static Vector3 GetMouseWorldPositionWithZ(Vector3 screenPosition, Camera worldCamera)
    {
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        return worldPosition;
    }

    public bool IsCoordinateOutsidePanel()
    {
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(panelRectTransform, Input.mousePosition, null);
        return !inside;
    }
}
