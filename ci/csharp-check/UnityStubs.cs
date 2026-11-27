// Minimal stand-ins for the UnityEngine APIs used by unity/Assets, so CI can
// type-check the C# without a Unity installation. Behaviour is not emulated.
using System;
using System.Collections;

namespace UnityEngine
{
    public class Object { }
    public class Component : Object { }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public class TextMesh : Component { public string text; }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
    }
    public static class JsonUtility { public static T FromJson<T>(string json) => default; }
    public static class Application
    {
        public static string persistentDataPath => "";
        public static string streamingAssetsPath => "";
        public static string buildGUID => "";
        public static string version => "";
    }
    public static class SystemInfo
    {
        public static string deviceModel => "";
        public static string operatingSystem => "";
    }
    public class AsyncOperation { }
}

namespace UnityEngine.Networking
{
    public class DownloadHandler { public byte[] data => Array.Empty<byte>(); }
    public sealed class UnityWebRequestAsyncOperation : UnityEngine.AsyncOperation { }
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public static UnityWebRequest Get(string uri) => new UnityWebRequest();
        public UnityWebRequestAsyncOperation SendWebRequest() => new UnityWebRequestAsyncOperation();
        public Result result => Result.Success;
        public string error => "";
        public DownloadHandler downloadHandler => new DownloadHandler();
        public void Dispose() { }
    }
}
