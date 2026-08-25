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
        private static int ownProcessId_;
        private static string ownProcessName_;

        // Answered once per process id: resolving a name opens a handle, and this is asked
        // for every window in the list.
        private static readonly Dictionary<int, bool> isOursByPid_ = new Dictionary<int, bool>();

        /// <summary>
        /// Whether a window belongs to VIP-Sim -- this copy of it, or another one.
        ///
        /// The process id alone is not enough. A second instance is a different process, so
        /// a pid test lists it happily, and each copy then offers to capture the other: the
        /// overlay simulating the overlay. The old title test caught that case by accident,
        /// which is the one thing it did right, so the name check replaces it deliberately.
        /// </summary>
        public static bool IsOwnWindow(UwcWindow window)
        {
            if (window == null) return true;

            int pid = window.processId;
            if (pid == ownProcessId_) return true;

            bool ours;
            if (isOursByPid_.TryGetValue(pid, out ours)) return ours;

            ours = false;
            try
            {
                using (var other = System.Diagnostics.Process.GetProcessById(pid))
                    ours = string.Equals(other.ProcessName, ownProcessName_,
                                         System.StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Exited, protected, or elevated. Not ours as far as we can tell.
            }

            isOursByPid_[pid] = ours;
            return ours;
        }

        void Start()
        {
            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                ownProcessId_ = self.Id;
                ownProcessName_ = self.ProcessName;
            }
            isOursByPid_.Clear();

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
            if (IsOwnWindow(window)) return;

            // One line per window, so a list that shows something it should not can be
            // diagnosed from a user's log instead of a screenshot.
            Debug.Log($"[WindowList] '{window.title}' pid={window.processId} ours={ownProcessId_}");


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