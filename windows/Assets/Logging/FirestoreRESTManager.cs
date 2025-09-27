using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

public class FirestoreRESTManager : MonoBehaviour
{

    private const string FilePath = "UUID.data";// Local file path
    private const string LogFilePath = "log.json";

    private UserSessionData sessionData;
    private string firestoreUrl = "https://vip-sim-default-rtdb.europe-west1.firebasedatabase.app/";

    // Inspector variables
    public TMP_InputField learningsInputField;
    public TMP_InputField openFeedbackInputField;

    public ToggleGroup ratingSuSToggleGroup;
    public ToggleGroup ratingLearnedAccessibilityToggleGroup;


    void Start()
    {
        string uuid = LoadUUID();

        // Initialize session data
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

    private string LoadUUID()
    {
        string fullPath = Path.Combine(Application.persistentDataPath, FilePath);

        if (File.Exists(fullPath))
        {
            return File.ReadAllText(fullPath); // Load existing UUID
        }
        else
        {
            string newUUID = Guid.NewGuid().ToString();
            File.WriteAllText(fullPath, newUUID); // Save new UUID to file
            return newUUID;
        }
    }

    public void OnProgramClick(string programName)
    {
        sessionData.ActivePrograms.Add(new ProgramActivity(programName, DateTime.UtcNow.ToString("o")));
        Debug.Log($"Program {programName} clicked at {DateTime.UtcNow}");
    }

    public void OnButtonClick(string buttonType)
    {
        sessionData.EyeTrackerClicks.Add(new ButtonClick(buttonType, DateTime.UtcNow.ToString("o")));
        Debug.Log($"Button {buttonType} clicked at {DateTime.UtcNow}");
    }

    public void OnImpairmentClick(string impairmentName, float severity)
    {
        sessionData.Impairments.Add(new ImpairmentClick(impairmentName, severity, DateTime.UtcNow.ToString("o")));
        Debug.Log($"Impairment {impairmentName} clicked with severity {severity} at {DateTime.UtcNow}");
    }

    public void OnEndSessionButtonPressed()
    {
        // Holen der Benutzer-Eingaben
        int ratingSuS = GetSelectedToggleValue(ratingSuSToggleGroup);
        int ratingLearnedAccessibility = GetSelectedToggleValue(ratingLearnedAccessibilityToggleGroup);
        string learnings = learningsInputField.text;
        string openFeedback = openFeedbackInputField.text;

        // Setze die Werte im sessionData-Objekt
        sessionData.EndOfSession = System.DateTime.UtcNow.ToString("o");
        sessionData.Rating_SuS = ratingSuS;
        sessionData.Rating_LearnedAccessibility = ratingLearnedAccessibility;
        sessionData.Learnings = learnings;
        sessionData.OpenFeedback = openFeedback;

        if (toggleGroup == null)
        {
            Debug.LogWarning("Toggle group reference is missing.");
            return 0;
        }

        if (selectedToggle == null)
            Debug.LogWarning("No toggle is currently selected.");
            return 0;
        }

        string labelText = null;

        Text legacyText = selectedToggle.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            labelText = legacyText.text;
            TMP_Text tmpText = selectedToggle.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                labelText = tmpText.text;
            }
        }

        if (string.IsNullOrEmpty(labelText))
        {
            Debug.LogWarning("Selected toggle does not contain any readable text.");

        Match match = Regex.Match(labelText, @"\d+");
        if (match.Success && int.TryParse(match.Value, out int value))
        {
            return value;
        }

        Debug.LogWarning($"Unable to parse numeric value from toggle label '{labelText}'.");
        return 0;

        FirebaseResponse response;

        using (UnityWebRequest authRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            if (authRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Fehler bei der Anmeldung: " + authRequest.error);
                SaveLogLocally(jsonData);
                Application.Quit();
                yield break;
            }
            response = JsonConvert.DeserializeObject<FirebaseResponse>(authRequest.downloadHandler.text);
            if (response == null || string.IsNullOrEmpty(response.idToken))
                Debug.LogError("Failed to parse authentication response or missing ID token.");
                SaveLogLocally(jsonData);
                Application.Quit();
                yield break;
            }
        }

        using (UnityWebRequest request = new UnityWebRequest(firestoreUrl + sessionData.UUID.ToString() + ".json?auth=" + response.idToken, UnityWebRequest.kHttpVerbPOST))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Session data successfully sent to Firestore.");
                Debug.LogError("Error sending session data: " + request.error);

            authRequest.uploadHandler = new UploadHandlerRaw(authBodyRaw);
            authRequest.downloadHandler = new DownloadHandlerBuffer();
            authRequest.SetRequestHeader("Content-Type", "application/json");

            // Sende den Request und warte auf das Ergebnis
            yield return authRequest.SendWebRequest();

            FirebaseResponse response = new FirebaseResponse();

            if (authRequest.result == UnityWebRequest.Result.Success)
            {
                // Erfolgreich
                Debug.Log("Anmeldung erfolgreich!");
                Debug.Log("Antwort: " + authRequest.downloadHandler.text);

                // JSON-Antwort parsen (optional)
                response = JsonConvert.DeserializeObject<FirebaseResponse>(authRequest.downloadHandler.text);
                Debug.Log($"ID-Token: {response.idToken}");
            }
            else
            {
                // Fehlerbehandlung
                Debug.LogError("Fehler bei der Anmeldung: " + authRequest.error);
            }
       
    
        UnityWebRequest request = new UnityWebRequest(firestoreUrl + sessionData.UUID.ToString()+".json?auth=" + response.idToken, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Session data successfully sent to Firestore.");
        }
        else
        {
            Debug.LogError("Error sending session data: " + request.error);
        }

        SaveLogLocally(jsonData);

        Application.Quit();

    }

    private void SaveLogLocally(string jsonData)
    {
        string fullPath = Path.Combine(Application.persistentDataPath, LogFilePath);

        try
        {
            // Append log data to the local backup file
            File.AppendAllText(fullPath, jsonData + "\n");
            Debug.Log("Log saved locally as a backup.");
        }
        catch (IOException e)
        {
            Debug.LogError("Failed to save log locally: " + e.Message);
        }
    }

    // Klasse zum Speichern der Antwort-Daten
    private class FirebaseResponse
    {
        public string idToken;
        public string refreshToken;
        public string expiresIn;
        public string localId;
    }
}

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
