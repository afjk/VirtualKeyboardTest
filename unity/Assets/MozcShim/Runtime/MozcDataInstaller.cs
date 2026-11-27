using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MozcShim
{
    /// <summary>
    /// Mozc reads its dictionary from a file path. On Android, StreamingAssets live
    /// inside the APK, so the data is copied to persistentDataPath once per build.
    /// </summary>
    public static class MozcDataInstaller
    {
        public const string StreamingAssetsRelativePath = "mozc/mozc.data";

        public static string ProfileDirectory => Path.Combine(Application.persistentDataPath, "mozc-profile");

        /// <param name="onDone">Receives the data file path, or null and an error message.</param>
        public static IEnumerator Install(Action<string, string> onDone)
        {
            string source = Path.Combine(Application.streamingAssetsPath, StreamingAssetsRelativePath);
            if (!source.Contains("://"))
            {
                // Editor and desktop players: StreamingAssets is a normal directory.
                if (File.Exists(source)) onDone(source, null);
                else onDone(null, "missing " + source);
                yield break;
            }

            string dir = Path.Combine(Application.persistentDataPath, "mozc");
            string target = Path.Combine(dir, "mozc.data");
            string marker = target + ".build";
            string build = Application.buildGUID + "/" + Application.version;
            if (File.Exists(target) && File.Exists(marker) && File.ReadAllText(marker) == build)
            {
                onDone(target, null);
                yield break;
            }

            using (var request = UnityWebRequest.Get(source))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onDone(null, "could not read " + source + ": " + request.error);
                    yield break;
                }
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(target, request.downloadHandler.data);
                File.WriteAllText(marker, build);
            }
            onDone(target, null);
        }
    }
}
