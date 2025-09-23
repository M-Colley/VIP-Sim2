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

        // Jetzt die sessionData an Firestore senden (siehe vorherige L�sung)
        string jsonData = JsonUtility.ToJson(sessionData);
        StartCoroutine(SendSessionDataToFirestore(jsonData));
    }

    private int GetSelectedToggleValue(ToggleGroup toggleGroup)
    {
        // Sucht den aktiven Toggle in der Gruppe
        Toggle selectedToggle = toggleGroup.ActiveToggles().FirstOrDefault();

        if (selectedToggle != null)
        {
            var label = selectedToggle.GetComponentInChildren<Text>();
            if (label != null)
            {
                string numericPart = Regex.Match(label.text, @"\d+").Value;
                if (int.TryParse(numericPart, out int value))
                {
                    return value;
                }
            }
        }

        Debug.LogWarning("No valid toggle selected.");
        return 0;
    }

    private IEnumerator SendSessionDataToFirestore(string jsonData)
    {
        string url = $"ANON";

        // Der Request-Body
        var requestBody = new
        {
            returnSecureToken = true
        };

        // JSON-Daten f�r den POST-Request
        string jData = JsonUtility.ToJson(requestBody);

        UnityWebRequest authRequest = new UnityWebRequest(url, "POST");
        
            byte[] authBodyRaw = System.Text.Encoding.UTF8.GetBytes(jData);
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
