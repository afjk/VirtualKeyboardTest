// Minimal stand-ins for the UnityEngine APIs used by unity/Assets, so CI can
// type-check the C# without a Unity installation. Behaviour is not emulated.
using System;
using System.Collections;

namespace UnityEngine
{
    public class Object { }
    public class Component : Object { public GameObject gameObject => null; }
    public class GameObject : Object { public T AddComponent<T>() where T : Component, new() => new T(); }
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
        public static int targetFrameRate { get; set; }
    }
    public static class SystemInfo
    {
        public static string deviceModel => "";
        public static string operatingSystem => "";
    }
    public class AsyncOperation { }

    // IMGUI subset used by Samples/MozcKeyboardDemo.cs.
    public struct Vector2 { public float x, y; }
    public struct Vector3 { public Vector3(float x, float y, float z) { } }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public struct Quaternion { public static Quaternion identity => default; }
    public struct Color
    {
        public Color(float r, float g, float b) { }
    }
    public struct Matrix4x4
    {
        public static Matrix4x4 Scale(Vector3 v) => default;
        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s) => default;
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
    }
    public static class Screen
    {
        public static int width => 0;
        public static int height => 0;
    }
    public class Font : Object
    {
        public static string[] GetOSInstalledFontNames() => Array.Empty<string>();
        public static Font CreateDynamicFontFromOSFont(string fontname, int size) => new Font();
    }
    public enum TextAnchor { UpperLeft }
    public class GUIContent { public GUIContent(string text) { } }
    public class GUIStyle
    {
        public GUIStyle(GUIStyle other) { }
        public int fontSize;
        public TextAnchor alignment;
        public bool wordWrap;
        public Font font;
        public Vector2 CalcSize(GUIContent content) => default;
    }
    public class GUISkin : Object
    {
        public Font font;
        public GUIStyle box, label, button;
    }
    public static class GUI
    {
        public static GUISkin skin => new GUISkin();
        public static Matrix4x4 matrix { get; set; }
        public static Color backgroundColor { get; set; }
        public static void Label(Rect position, string text, GUIStyle style) { }
        public static void Box(Rect position, string text, GUIStyle style) { }
        public static bool Button(Rect position, string text, GUIStyle style) => false;
        public static bool Button(Rect position, GUIContent content, GUIStyle style) => false;
    }
    public enum EventType { KeyDown }
    public enum KeyCode
    {
        None, Space, Return, KeypadEnter, Backspace, Delete, Escape,
        LeftArrow, RightArrow, UpArrow, DownArrow,
    }
    public class Event
    {
        public static Event current => new Event();
        public EventType type;
        public KeyCode keyCode;
        public char character;
        public void Use() { }
    }
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
