using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;

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

        void Start()
        {
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
            bool newState = checkForActiveWindows();
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

            if (window.title.ToLower().Replace("-", "").Replace("_", "").Contains("vipsim")) { return; }
            

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