using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

public class FirestoreRESTManager : MonoBehaviour
{
    [Header("Local files")]
    [SerializeField] private string uuidFileName = "UUID.data";
    [SerializeField] private string logFileName = "log.json";

    [Header("Firebase Realtime Database")]
    [Tooltip("Example, https://your-db-id.europe-west1.firebasedatabase.app/")]
    [SerializeField] private string realtimeDatabaseUrl = "https://vip-sim-default-rtdb.europe-west1.firebasedatabase.app/";

    [Header("Optional Firebase Auth")]
    [SerializeField] private string firebaseApiKey = "";                 // set if you want auth
    [SerializeField] private bool useEmailPasswordAuth = false;          // false uses anonymous auth
    [SerializeField] private string authEmail = "";
    [SerializeField] private string authPassword = "";

    [Header("UI")]
    public TMP_InputField learningsInputField;
    public TMP_InputField openFeedbackInputField;
    public ToggleGroup ratingSuSToggleGroup;
    public ToggleGroup ratingLearnedAccessibilityToggleGroup;

    private UserSessionData sessionData;

    private void Start()
    {
        string uuid = LoadOrCreateUUID(uuidFileName);
        sessionData = new UserSessionData
        {
            UUID = uuid,
            StartOfSession = DateTime.UtcNow.ToString("o"),
            ActivePrograms = new List<ProgramActivity>(),
            EyeTrackerClicks = new List<ButtonClick>(),
            SaveLoadClicks = new List<ButtonClick>(),
            Impairments = new List<ImpairmentClick>(),
            Learnings = "",
            OpenFeedback = ""
        };
    }

    private string LoadOrCreateUUID(string fileName)
    {
        string fullPath = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(fullPath))
        {
            return File.ReadAllText(fullPath);
        }
        string newUUID = Guid.NewGuid().ToString();
        File.WriteAllText(fullPath, newUUID);
        return newUUID;
    }

    public void OnProgramClick(string programName)
    {
        sessionData.ActivePrograms.Add(new ProgramActivity(programName, DateTime.UtcNow.ToString("o")));
        Debug.Log($"Program {programName} clicked at {DateTime.UtcNow:o}");
    }

    public void OnButtonClick(string buttonType)
    {
        sessionData.EyeTrackerClicks.Add(new ButtonClick(buttonType, DateTime.UtcNow.ToString("o")));
        Debug.Log($"Button {buttonType} clicked at {DateTime.UtcNow:o}");
    }

    public void OnImpairmentClick(string impairmentName, float severity)
    {
        sessionData.Impairments.Add(new ImpairmentClick(impairmentName, severity, DateTime.UtcNow.ToString("o")));
        Debug.Log($"Impairment {impairmentName} severity {severity} at {DateTime.UtcNow:o}");
    }

    // UI hook, called by your End Session button
    public void OnEndSessionButtonPressed()
    {
        // Read UI
        int ratingSuS = GetSelectedToggleValue(ratingSuSToggleGroup);
        int ratingLearned = GetSelectedToggleValue(ratingLearnedAccessibilityToggleGroup);
        string learnings = learningsInputField != null ? learningsInputField.text : "";
        string openFeedback = openFeedbackInputField != null ? openFeedbackInputField.text : "";

        // Fill session
        sessionData.EndOfSession = DateTime.UtcNow.ToString("o");
        sessionData.Rating_SuS = ratingSuS;
        sessionData.Rating_LearnedAccessibility = ratingLearned;
        sessionData.Learnings = learnings;
        sessionData.OpenFeedback = openFeedback;

        string jsonData = JsonConvert.SerializeObject(sessionData);
        SaveLogLocally(jsonData, logFileName); // always keep a local copy

        StartCoroutine(SendThenQuit(jsonData));
    }

    private int GetSelectedToggleValue(ToggleGroup toggleGroup)
    {
        if (toggleGroup == null)
        {
            Debug.LogWarning("Toggle group reference is missing");
            return 0;
        }

        Toggle selectedToggle = toggleGroup.ActiveToggles().FirstOrDefault();
        if (selectedToggle == null)
        {
            Debug.LogWarning("No toggle is currently selected");
            return 0;
        }

        // Try TMP first, then legacy Text
        string labelText = null;
        TMP_Text tmp = selectedToggle.GetComponentInChildren<TMP_Text>();
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            labelText = tmp.text;
        else
        {
            Text legacy = selectedToggle.GetComponentInChildren<Text>();
            if (legacy != null && !string.IsNullOrWhiteSpace(legacy.text))
                labelText = legacy.text;
        }

        if (string.IsNullOrWhiteSpace(labelText))
        {
            Debug.LogWarning("Selected toggle has no readable text");
            return 0;
        }

        Match match = Regex.Match(labelText, @"\d+");
        if (match.Success && int.TryParse(match.Value, out int value))
            return value;

        Debug.LogWarning($"Unable to parse numeric value from toggle label '{labelText}'");
        return 0;
    }

    private IEnumerator SendThenQuit(string jsonData)
    {
        string idToken = null;

        // Optional authentication
        if (!string.IsNullOrWhiteSpace(firebaseApiKey))
        {
            yield return StartCoroutine(SignInFirebase(token => idToken = token));
        }

        // Post to Realtime Database
        yield return StartCoroutine(PostSessionToRealtimeDatabase(jsonData, idToken));

#if UNITY_EDITOR
        // In editor this does nothing, kept for completeness
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator SignInFirebase(Action<string> onToken)
    {
        string url;
        object payload;

        if (useEmailPasswordAuth)
        {
            if (string.IsNullOrWhiteSpace(authEmail) || string.IsNullOrWhiteSpace(authPassword))
            {
                Debug.LogError("Email or password missing for sign-in");
                onToken?.Invoke(null);
                yield break;
            }

            url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={firebaseApiKey}";
            payload = new { email = authEmail, password = authPassword, returnSecureToken = true };
        }
        else
        {
            url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={firebaseApiKey}";
            payload = new { returnSecureToken = true };
        }

        string body = JsonConvert.SerializeObject(payload);
        using (UnityWebRequest authRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            authRequest.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            authRequest.downloadHandler = new DownloadHandlerBuffer();
            authRequest.SetRequestHeader("Content-Type", "application/json");
            yield return authRequest.SendWebRequest();

            if (authRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Auth error, {authRequest.error}");
                onToken?.Invoke(null);
                yield break;
            }

            FirebaseResponse response = null;
            try
            {
                response = JsonConvert.DeserializeObject<FirebaseResponse>(authRequest.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"Auth parse error, {e.Message}");
            }

            if (response == null || string.IsNullOrWhiteSpace(response.idToken))
            {
                Debug.LogError("Missing ID token in auth response");
                onToken?.Invoke(null);
                yield break;
            }

            onToken?.Invoke(response.idToken);
        }
    }

    private IEnumerator PostSessionToRealtimeDatabase(string jsonData, string idToken)
    {
        // Push under /UUID with a new child key
        string url = $"{realtimeDatabaseUrl}{sessionData.UUID}.json";
        if (!string.IsNullOrWhiteSpace(idToken))
            url += $"?auth={UnityWebRequest.EscapeURL(idToken)}";

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Session data sent to Realtime Database");
            }
            else
            {
                Debug.LogError($"Error sending session data, {request.error}");
            }
        }
    }

    private void SaveLogLocally(string jsonData, string fileName)
    {
        string fullPath = Path.Combine(Application.persistentDataPath, fileName);
        try
        {
            File.AppendAllText(fullPath, jsonData + Environment.NewLine);
            Debug.Log("Log saved locally");
        }
        catch (IOException e)
        {
            Debug.LogError($"Failed to save log, {e.Message}");
        }
    }

    [Serializable]
    private class FirebaseResponse
    {
        public string idToken;
        public string refreshToken;
        public string expiresIn;
        public string localId;
    }
}

// Data models can stay in the same file
[Serializable]
public class UserSessionData
{
    public string UUID;
    public string StartOfSession;
    public string EndOfSession;
    public List<ProgramActivity> ActivePrograms;
    public List<ButtonClick> EyeTrackerClicks;
    public List<ButtonClick> SaveLoadClicks;
    public List<ImpairmentClick> Impairments;
    public int Rating_SuS;
    public int Rating_LearnedAccessibility;
    public string Learnings;
    public string OpenFeedback;

    public UserSessionData()
    {
        ActivePrograms = new List<ProgramActivity>();
        EyeTrackerClicks = new List<ButtonClick>();
        SaveLoadClicks = new List<ButtonClick>();
        Impairments = new List<ImpairmentClick>();
    }
}

[Serializable]
public class ProgramActivity
{
    public string ProgramName;
    public string TimeClicked;

    public ProgramActivity(string programName, string timeClicked)
    {
        ProgramName = programName;
        TimeClicked = timeClicked;
    }
}

[Serializable]
public class ButtonClick
{
    public string ButtonType;
    public string TimeClicked;

    public ButtonClick(string buttonType, string timeClicked)
    {
        ButtonType = buttonType;
        TimeClicked = timeClicked;
    }
}

[Serializable]
public class ImpairmentClick
{
    public string ImpairmentName;
    public float Severity;
    public string TimeClicked;

    public ImpairmentClick(string impairmentName, float severity, string timeClicked)
    {
        ImpairmentName = impairmentName;
        Severity = severity;
        TimeClicked = timeClicked;
    }
}
