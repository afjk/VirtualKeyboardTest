using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MozcShim;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Interactive Mozc demo for a 2D Android panel (IMGUI, no extra packages).
/// Add to any GameObject in an empty scene.
/// Tap the on-screen romaji keyboard or use a hardware keyboard.
/// SPACE = convert / next candidate, ENTER = commit, tap a candidate to select it.
/// </summary>
public sealed class MozcKeyboardDemo : MonoBehaviour
{
    private static readonly string[] Rows = { "1234567890-", "qwertyuiop", "asdfghjkl", "zxcvbnm,.?" };

    private MozcSession session;
    private MozcState state = new MozcState();
    private readonly StringBuilder text = new StringBuilder();
    private string status = "辞書を準備中…";
    private Font font;
    private GUIStyle box, button, candidate, label;

    private IEnumerator Start()
    {
        Application.targetFrameRate = 60;
        var names = Font.GetOSInstalledFontNames();
        string jp = names.FirstOrDefault(n => n.Contains("CJK") && n.Contains("JP"))
                    ?? names.FirstOrDefault(n => n.Contains("CJK"))
                    ?? names.FirstOrDefault(n => n.Contains("Fallback"));
        Debug.Log("[MozcDemo] font: " + (jp ?? "(none, using built-in)"));
        if (jp != null) font = Font.CreateDynamicFontFromOSFont(jp, 32);

        string dataPath = null, error = null;
        yield return MozcDataInstaller.Install((p, e) => { dataPath = p; error = e; });
        if (dataPath == null) { status = "データ展開に失敗: " + error; yield break; }
        if (!MozcSession.InitializeEngine(MozcDataInstaller.ProfileDirectory, dataPath, out error))
        { status = "初期化に失敗: " + error; yield break; }
        session = new MozcSession();
        status = "準備完了 (" + MozcSession.DataVersion + ")";
    }

    private void OnDestroy() => session?.Dispose();

    private void Apply(System.Func<MozcState> action, string label)
    {
        if (session == null) return;
        var watch = Stopwatch.StartNew();
        state = action();
        status = $"{label}: {watch.Elapsed.TotalMilliseconds:F2} ms";
        if (state.Committed.Length > 0) text.Append(state.Committed);
    }

    private void Key(char c) => Apply(() => session.SendKey(c), "key '" + c + "'");
    private void Special(string k) => Apply(() => session.SendSpecialKey(k), k);

    private void Backspace()
    {
        // With no preedit, delete from the committed text instead.
        if (state.Preedit.Length == 0) { if (text.Length > 0) text.Length--; return; }
        Special("BACKSPACE");
    }

    private void EnsureStyles()
    {
        if (box != null) return;
        if (font != null) GUI.skin.font = font;
        box = new GUIStyle(GUI.skin.box) { fontSize = 34, alignment = TextAnchor.UpperLeft, wordWrap = true };
        label = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        button = new GUIStyle(GUI.skin.button) { fontSize = 34 };
        candidate = new GUIStyle(GUI.skin.button) { fontSize = 32 };
        if (font != null) box.font = label.font = button.font = candidate.font = font;
    }

    private void OnGUI()
    {
        EnsureStyles();
        HandleHardwareKeys();

        // Lay out on a 1280-wide virtual canvas scaled to the window.
        float scale = Screen.width / 1280f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = 1280, h = Screen.height / scale, pad = 12;
        float y = pad;

        GUI.Label(new Rect(pad, y, w - 2 * pad, 30), status, label);
        y += 34;

        // Committed text + preedit (preedit underlined with brackets).
        string preedit = state.Preedit.Length > 0 ? "［" + string.Join("|", state.Segments) + "］" : "";
        GUI.Box(new Rect(pad, y, w - 2 * pad, 150), text + preedit + "▏", box);
        y += 160;

        // Candidates.
        var cands = state.Candidates;
        float cx = pad;
        for (int i = 0; i < cands.Length; i++)
        {
            var (id, value) = cands[i];
            var content = new GUIContent(value);
            float cw = Mathf.Max(70, candidate.CalcSize(content).x + 20);
            if (cx + cw > w - pad) break;
            bool focused = i == state.FocusedCandidateIndex;
            var old = GUI.backgroundColor;
            if (focused) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUI.Button(new Rect(cx, y, cw, 64), content, candidate))
            {
                Apply(() => session.SelectCandidate(id), "select " + value);
            }
            GUI.backgroundColor = old;
            cx += cw + 6;
        }
        y += 74;

        // Keyboard fills the rest.
        float rowH = Mathf.Min(90, (h - y - pad) / (Rows.Length + 1) - 6);
        float keyW = (w - 2 * pad) / 11f;
        foreach (var row in Rows)
        {
            float x = pad + (11 - row.Length) * keyW / 2;
            foreach (char c in row)
            {
                if (GUI.Button(new Rect(x + 3, y, keyW - 6, rowH), c.ToString(), button)) Key(c);
                x += keyW;
            }
            y += rowH + 6;
        }
        float bx = pad, bw = (w - 2 * pad) / 7f;
        if (GUI.Button(new Rect(bx + 3, y, bw - 6, rowH), "BS", button)) Backspace(); bx += bw;
        if (GUI.Button(new Rect(bx + 3, y, bw - 6, rowH), "削除", button)) Special("DEL"); bx += bw;
        if (GUI.Button(new Rect(bx + 3, y, bw - 6, rowH), "←", button)) Special("LEFT"); bx += bw;
        if (GUI.Button(new Rect(bx + 3, y, bw * 2 - 6, rowH), "変換 / 空白", button)) Special("SPACE"); bx += bw * 2;
        if (GUI.Button(new Rect(bx + 3, y, bw - 6, rowH), "→", button)) Special("RIGHT"); bx += bw;
        if (GUI.Button(new Rect(bx + 3, y, bw - 6, rowH), "確定", button))
        {
            if (state.Preedit.Length == 0) text.Append('\n'); else Special("ENTER");
        }
    }

    private void HandleHardwareKeys()
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown) return;
        switch (e.keyCode)
        {
            case KeyCode.Space: Special("SPACE"); break;
            case KeyCode.Return: case KeyCode.KeypadEnter:
                if (state.Preedit.Length == 0) text.Append('\n'); else Special("ENTER"); break;
            case KeyCode.Backspace: Backspace(); break;
            case KeyCode.Delete: Special("DEL"); break;
            case KeyCode.Escape: Special("ESCAPE"); break;
            case KeyCode.LeftArrow: Special("LEFT"); break;
            case KeyCode.RightArrow: Special("RIGHT"); break;
            case KeyCode.UpArrow: Special("UP"); break;
            case KeyCode.DownArrow: Special("DOWN"); break;
            default:
                char c = e.character;
                if (c > ' ' && c < 127) Key(char.ToLowerInvariant(c)); else return;
                break;
        }
        e.Use();
    }
}
