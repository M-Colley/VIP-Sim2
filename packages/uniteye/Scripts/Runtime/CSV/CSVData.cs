using System.Collections.Generic;
using System;
using System.Globalization;

namespace UnitEye
{
    public class CSVData
    {
        public float gazePixelX;
        public float gazePixelY;
        public float gazeNormalizedX;
        public float gazeNormalizedY;
        public float gazeNormalizedUnfilteredX;
        public float gazeNormalizedUnfilteredY;
        public float distanceToCamera;
        public float eyeAspectRatio;
        public bool blinking;
        public List<string> AOIList;
        public DateTime timestamp;
        public string notes;
        //Measured pipeline latency of this sample (camera-frame consumption -> logging), in ms. Analysts
        //re-aligning gaze against MOVING stimuli must shift by this: 150ms x a 500px/s object = 75px of
        //systematic error if ignored. 0 for rows created without latency information (e.g. notes).
        public float captureLatencyMs;

        //Constructor to set all values in one line of code
        public CSVData(float gazePixelX, float gazePixelY, float gazeNormalizedX, float gazeNormalizedY, float gazeNormalizedUnfilteredX, float gazeNormalizedUnfilteredY, float distanceToCamera, float eyeAspectRatio, bool blinking, DateTime timestamp, List<string> AOIList, float captureLatencyMs = 0f)
        {
            this.gazePixelX = gazePixelX;
            this.gazePixelY = gazePixelY;
            this.gazeNormalizedX = gazeNormalizedX;
            this.gazeNormalizedY = gazeNormalizedY;
            this.gazeNormalizedUnfilteredX = gazeNormalizedUnfilteredX;
            this.gazeNormalizedUnfilteredY = gazeNormalizedUnfilteredY;
            this.distanceToCamera = distanceToCamera;
            this.eyeAspectRatio = eyeAspectRatio;
            this.blinking = blinking;
            this.timestamp = timestamp;
            this.AOIList = AOIList;
            this.captureLatencyMs = captureLatencyMs;
        }

        //Invariant culture so the decimal separator is always a period regardless of the user's locale.
        private static string F(float v) => v.ToString(CultureInfo.InvariantCulture);

        //Seralize all the CSVData properties into one string
        public string SerializeCSVData()
        {
            System.Text.StringBuilder bobTheStringBuilder = new System.Text.StringBuilder();

            //Gaze location both in pixels and normalized
            bobTheStringBuilder.Append($"{(int)gazePixelX};{(int)gazePixelY};{F(gazeNormalizedX)};{F(gazeNormalizedY)}");

            //Unfiltered normalized gaze location 
            bobTheStringBuilder.Append($";{F(gazeNormalizedUnfilteredX)};{F(gazeNormalizedUnfilteredY)}");

            //Distance to camera
            bobTheStringBuilder.Append($";{(float.IsPositiveInfinity(distanceToCamera) ? 0f : distanceToCamera).ToString("F1", CultureInfo.InvariantCulture)}");

            //Eye Aspect Ratio and blinking
            bobTheStringBuilder.Append($";{F(float.IsNaN(eyeAspectRatio) ? 0f : eyeAspectRatio)};{blinking}");

            //Add timestamp with milliseconds at the end
            bobTheStringBuilder.Append($";{timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}");

            //Add unix timestamp in milliseconds
            bobTheStringBuilder.Append($";{((DateTimeOffset)timestamp).ToUnixTimeMilliseconds()}");

            //Measured pipeline latency (see field comment)
            bobTheStringBuilder.Append($";{captureLatencyMs.ToString("F0", CultureInfo.InvariantCulture)}");

            //Write AOIList if not empty
            if (AOIList.Count > 0)
            {
                //Might need changing depending on the .ToString() behaviour of the AOIList
                bobTheStringBuilder.Append($";{string.Join(" | ", AOIList)}");
            }
            else
            {
                //If empty write empty value
                bobTheStringBuilder.Append($";");
            }

            //Write notes
            bobTheStringBuilder.Append($";{notes}");

            return bobTheStringBuilder.ToString();
        }
    }
}