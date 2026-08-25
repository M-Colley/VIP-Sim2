using UnityEngine;
using System;
using System.Collections.Generic;

namespace uWindowCapture
{

    public class UwcWindowList : MonoBehaviour
    {
        [SerializeField] GameObject windowListItem;
        [SerializeField] Transform listRoot;

        public UwcWindowTextureManager windowTextureManager;

        Dictionary<int, UwcWindowListItem> items_ = new Dictionary<int, UwcWindowListItem>();

        public static bool thereIsActiveWindow = false;
        public static event Action<bool> OnActiveWindowChanged;

        // Our own process, so our own windows can be recognised without guessing from
        // their titles. See OnWindowAdded.
        private int ownProcessId_;

        void Start()
        {
            ownProcessId_ = System.Diagnostics.Process.GetCurrentProcess().Id;

            UwcManager.onWindowAdded.AddListener(OnWindowAdded);
            UwcManager.onWindowRemoved.AddListener(OnWindowRemoved);

            foreach (var pair in UwcManager.windows)
            {
                OnWindowAdded(pair.Value);
            }
        }

        public void DisableAllWindows()
        {
            foreach (UwcWindowListItem window in items_.Values)
            {
                window.RemoveWindow();
            }
        }

        private void Update()
        {
            // VipSimDiagnostics.ForceMenusVisible (F7) pretends a window is selected, so
            // the effect list can be inspected without one. Without it that half of the UI
            // cannot be seen at all during development -- a click-through overlay ignores
            // synthetic clicks, so no window can be selected programmatically.
            bool newState = VipSimDiagnostics.ForceMenusVisible || checkForActiveWindows();
            if (thereIsActiveWindow != newState)
            {
                thereIsActiveWindow = newState;
                OnActiveWindowChanged?.Invoke(thereIsActiveWindow);
            }
        }

        public bool checkForActiveWindows()
        {
            foreach (UwcWindowListItem window in items_.Values)
            {
                if (window.image_.color == window.selected)
                {
                    return true;
                }
            }
            return false;
        }

        void OnWindowAdded(UwcWindow window)
        {
            if (!window.isAltTabWindow || window.isBackground) return;

            // Skip our own overlay -- by PROCESS, not by name.
            //
            // This used to drop any window whose title contained "vipsim", which is a much
            // larger set than it sounds: a browser showing the VIP-Sim website, the folder
            // the release was unzipped into, an editor with a VIP-Sim file open. A designer
            // who opened our own page to look at it could not select it, and the failure
            // looked like "Chrome is not supported" rather than a name collision. Windows
            // belonging to this process are the ones actually worth hiding, and the process
            // id says so exactly.
            if (window.processId == ownProcessId_) return;


            var gameObject = Instantiate(windowListItem, listRoot, false);
            var listItem = gameObject.GetComponent<UwcWindowListItem>();
            //Debug.Log(listItem.title.text);
            listItem.window = window;
            listItem.list = this;
            items_.Add(window.id, listItem);

            window.RequestCaptureIcon();
            window.RequestCapture(CapturePriority.Low);
        }

        void OnWindowRemoved(UwcWindow window)
        {
            UwcWindowListItem listItem;
            items_.TryGetValue(window.id, out listItem);
            if (listItem)
            {
                listItem.RemoveWindow();
                Destroy(listItem.gameObject);
            }
        }
    }

}