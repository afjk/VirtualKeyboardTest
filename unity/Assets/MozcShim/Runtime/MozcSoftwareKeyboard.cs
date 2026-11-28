using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace MozcShim
{
    /// <summary>
    /// On-screen software keyboard with Japanese (romaji → kana → kanji) conversion.
    /// Add it to a GameObject; it loads the Mozc dictionary, draws itself with IMGUI
    /// in <see cref="screenArea"/> and works with taps, mouse clicks and a hardware
    /// keyboard. Read <see cref="Buffer"/> or subscribe to its events for the text.
    ///
    /// Keys: 英数/かな toggles the mode, ⇧ shifts the next letter, 変換 converts and
    /// cycles candidates, 確定 commits (or inserts a newline / submits), tap a
    /// candidate to choose it.
    /// </summary>
    public sealed class MozcSoftwareKeyboard : MonoBehaviour
    {
        private static readonly string[] Rows = { "1234567890-", "qwertyuiop", "asdfghjkl", "zxcvbnm,.?" };

        [Tooltip("Area to draw in, as fractions of the screen (x, y from the top left, width, height).")]
        public Rect screenArea = new Rect(0, 0, 1, 1);
        [Tooltip("Show the committed text and composition above the candidates.")]
        public bool showTextArea = true;
        [Tooltip("ENTER inserts a newline. When off, ENTER raises Submitted instead.")]
        public bool multiLine = true;
        [Tooltip("Start in kana mode once the dictionary is ready.")]
        public bool startInKana = true;
        [Tooltip("Handle a hardware keyboard while this component is enabled.")]
        public bool hardwareKeyboard = true;

        private MozcSession session;
        private string status = "辞書を準備中…";
        private Font font;
        private GUIStyle box, button, candidate, label;

        /// <summary>Editing state. Alphanumeric-only until the dictionary is ready.</summary>
        public JapaneseKeyboardBuffer Buffer { get; private set; }
        /// <summary>True once the Mozc engine is available.</summary>
        public bool Ready => session != null;
        /// <summary>Raised when the buffer is replaced (once the engine becomes ready).</summary>
        public event Action<JapaneseKeyboardBuffer> BufferReplaced;

        private void Awake()
        {
            Buffer = new JapaneseKeyboardBuffer(null) { MultiLine = multiLine };
        }

        private IEnumerator Start()
        {
            var names = Font.GetOSInstalledFontNames();
            string jp = names.FirstOrDefault(n => n.Contains("CJK") && n.Contains("JP"))
                        ?? names.FirstOrDefault(n => n.Contains("CJK"))
                        ?? names.FirstOrDefault(n => n.Contains("Fallback"));
            if (jp != null) font = Font.CreateDynamicFontFromOSFont(jp, 32);

            string dataPath = null, error = null;
            yield return MozcDataInstaller.Install((p, e) => { dataPath = p; error = e; });
            if (dataPath == null) { status = "辞書の展開に失敗: " + error; yield break; }
            if (!MozcSession.InitializeEngine(MozcDataInstaller.ProfileDirectory, dataPath, out error))
            { status = "初期化に失敗: " + error; yield break; }

            session = new MozcSession();
            var previous = Buffer;
            Buffer = new JapaneseKeyboardBuffer(session, startInKana ? KeyboardMode.Kana : KeyboardMode.Alphanumeric)
            { MultiLine = multiLine };
            // Keep what was typed while the dictionary loaded.
            Buffer.Insert(previous.Text);
            status = "";
            BufferReplaced?.Invoke(Buffer);
        }

        private void OnDestroy() => session?.Dispose();

        private void EnsureStyles()
        {
            if (box != null) return;
            box = new GUIStyle(GUI.skin.box) { fontSize = 34, alignment = TextAnchor.UpperLeft, wordWrap = true };
            label = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 34 };
            candidate = new GUIStyle(GUI.skin.button) { fontSize = 32 };
            if (font != null) box.font = label.font = button.font = candidate.font = font;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (hardwareKeyboard) HandleHardwareKeys();
            var buffer = Buffer;
            var composition = buffer.Composition;

            // Lay out on a 1280-wide virtual canvas scaled to the area.
            float areaX = screenArea.x * Screen.width, areaY = screenArea.y * Screen.height;
            float areaW = screenArea.width * Screen.width, areaH = screenArea.height * Screen.height;
            float scale = areaW / 1280f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(areaX, areaY, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            float w = 1280, h = areaH / scale, pad = 12;
            float y = pad;

            string mode = buffer.Mode == KeyboardMode.Kana ? "かな" : "英数";
            GUI.Label(new Rect(pad, y, w - 2 * pad, 30), status.Length > 0 ? status : mode, label);
            y += 34;

            if (showTextArea)
            {
                string preedit = composition.Preedit.Length > 0 ? "［" + string.Join("|", composition.Segments) + "］" : "";
                GUI.Box(new Rect(pad, y, w - 2 * pad, 150), buffer.Text + preedit + "▏", box);
                y += 160;
            }

            // Candidates for the focused segment.
            var cands = composition.Candidates;
            float cx = pad;
            for (int i = 0; i < cands.Length; i++)
            {
                var (id, value) = cands[i];
                var content = new GUIContent(value);
                float cw = Mathf.Max(70, candidate.CalcSize(content).x + 20);
                if (cx + cw > w - pad) break;
                var old = GUI.backgroundColor;
                if (i == composition.FocusedCandidateIndex) GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUI.Button(new Rect(cx, y, cw, 64), content, candidate)) buffer.SelectCandidate(id);
                GUI.backgroundColor = old;
                cx += cw + 6;
            }
            y += 74;

            // Keys fill the rest.
            float rowH = Mathf.Min(90, (h - y - pad) / (Rows.Length + 1) - 6);
            float keyW = (w - 2 * pad) / 11f;
            foreach (var row in Rows)
            {
                float x = pad + (11 - row.Length) * keyW / 2;
                foreach (char c in row)
                {
                    string face = buffer.Shift && char.IsLetter(c) ? char.ToUpperInvariant(c).ToString() : c.ToString();
                    if (GUI.Button(new Rect(x + 3, y, keyW - 6, rowH), face, button)) buffer.Type(c);
                    x += keyW;
                }
                y += rowH + 6;
            }

            float bx = pad, bw = (w - 2 * pad) / 9f;
            bool Key(float units, string face)
            {
                bool pressed = GUI.Button(new Rect(bx + 3, y, bw * units - 6, rowH), face, button);
                bx += bw * units;
                return pressed;
            }
            if (Key(1, buffer.Mode == KeyboardMode.Kana ? "英数" : "かな") && buffer.CanUseKana) buffer.ToggleMode();
            if (Key(1, buffer.Shift ? "⇧●" : "⇧")) buffer.ToggleShift();
            if (Key(1, "BS")) buffer.Backspace();
            if (Key(1, "←")) buffer.Left();
            if (Key(2, buffer.Mode == KeyboardMode.Kana && buffer.IsComposing ? "変換" : "空白")) buffer.Space();
            if (Key(1, "→")) buffer.Right();
            if (Key(2, buffer.IsComposing ? "確定" : buffer.MultiLine ? "改行" : "送信")) buffer.Enter();
        }

        private void HandleHardwareKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            var buffer = Buffer;
            switch (e.keyCode)
            {
                case KeyCode.Space: buffer.Space(); break;
                case KeyCode.Return: case KeyCode.KeypadEnter: buffer.Enter(); break;
                case KeyCode.Backspace: buffer.Backspace(); break;
                case KeyCode.Escape: buffer.Escape(); break;
                case KeyCode.LeftArrow: buffer.Left(); break;
                case KeyCode.RightArrow: buffer.Right(); break;
                case KeyCode.UpArrow: buffer.Up(); break;
                case KeyCode.DownArrow: buffer.Down(); break;
                default:
                    char c = e.character;
                    if (c > ' ' && c < 127) buffer.Type(c); else return;
                    break;
            }
            e.Use();
        }
    }
}
