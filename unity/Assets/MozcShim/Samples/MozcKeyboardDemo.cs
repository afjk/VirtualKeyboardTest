using MozcShim;
using UnityEngine;

/// <summary>
/// Minimal use of <see cref="MozcSoftwareKeyboard"/>. Add to any GameObject in an
/// empty scene and run (Android arm64 or Linux x64). Committed text is logged:
///   adb logcat -s Unity | grep MozcDemo
/// </summary>
public sealed class MozcKeyboardDemo : MonoBehaviour
{
    private MozcSoftwareKeyboard keyboard;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        keyboard = gameObject.AddComponent<MozcSoftwareKeyboard>();
        keyboard.multiLine = true;
        keyboard.BufferReplaced += Subscribe;
    }

    private void Start() => Subscribe(keyboard.Buffer);

    private void Subscribe(JapaneseKeyboardBuffer buffer)
    {
        buffer.Committed += text => Debug.Log("[MozcDemo] committed: " + text);
        buffer.Submitted += text => Debug.Log("[MozcDemo] submitted: " + text);
    }
}
