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
using System.Collections.Generic;
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

    private Vector3[] panelCorners = new Vector3[4];
    private Vector2 lastPanelSize;
    private Vector2 lastCanvasSize;

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

        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);

        SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        //SetLayeredWindowAttributes(hWnd, 0, 0, LWA_COLORKEY);

        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, 0);
#endif

        Application.runInBackground = true;
        CachePanelCorners();
        if (panelRectTransform != null) {
            lastPanelSize = panelRectTransform.rect.size;
        }
        if (canvasRectTransform != null) {
            lastCanvasSize = canvasRectTransform.rect.size;
        }
    }

    private void Update() {
        //SetClickthrough(Physics2D.OverlapPoint(GetMouseWorldPosition()) == null);
        bool clickthrough = IsCoordinateOutsidePanel();
        SetClickthrough(clickthrough);


    }

    private void CachePanelCorners() {
        if (panelRectTransform != null) {
            panelRectTransform.GetWorldCorners(panelCorners);
        }
    }

    private void OnRectTransformDimensionsChange() {
        if (panelRectTransform == null || canvasRectTransform == null) return;
        Vector2 panelSize = panelRectTransform.rect.size;
        Vector2 canvasSize = canvasRectTransform.rect.size;
        if (panelSize != lastPanelSize || canvasSize != lastCanvasSize) {
            lastPanelSize = panelSize;
            lastCanvasSize = canvasSize;
            CachePanelCorners();
        }
    }

    private void SetClickthrough(bool clickthrough) {

        
        if (clickthrough) {
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        } else {
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED);
        }
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
