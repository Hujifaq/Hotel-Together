using System;
using System.IO;
using UnityEngine;

namespace HotelTogether
{
    // Editor-only diagnostics for the graphics systems, written to <project>/Logs/HotelTogetherGraphics.log
    // so lighting and shader problems can be checked without scrolling the Console.
    public static class HotelGraphicsLog
    {
#if UNITY_EDITOR
        static string path;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "HotelTogetherGraphics.log"));
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, "Hotel Together graphics log " + DateTime.Now + "\n"); } catch { }
        }
#endif
        public static void Write(string message)
        {
#if UNITY_EDITOR
            if (path == null) Reset();
            try { File.AppendAllText(path, "[" + Time.realtimeSinceStartup.ToString("0.0") + "] " + message + "\n"); } catch { }
#endif
        }
    }
}
