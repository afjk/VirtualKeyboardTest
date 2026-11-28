using System;
using System.Text;

namespace MozcShim
{
    public enum KeyboardMode
    {
        /// <summary>Romaji input converted to kana and kanji by the engine.</summary>
        Kana,
        /// <summary>Letters, digits and symbols are inserted as typed.</summary>
        Alphanumeric,
    }

    /// <summary>
    /// Text editing state of a software keyboard with Japanese conversion.
    /// Independent of any UI: call the key methods from buttons, touches or a
    /// hardware keyboard and read <see cref="Text"/>, <see cref="Composition"/>.
    /// Committed text is appended at the end; there is no caret inside committed
    /// text (LEFT/RIGHT move within the composition only).
    /// </summary>
    public sealed class JapaneseKeyboardBuffer
    {
        private readonly IKanaKanjiConverter converter;
        private readonly StringBuilder text = new StringBuilder();

        /// <param name="converter">Null runs alphanumeric-only (e.g. while the dictionary loads).</param>
        public JapaneseKeyboardBuffer(IKanaKanjiConverter converter, KeyboardMode mode = KeyboardMode.Kana)
        {
            this.converter = converter;
            Mode = converter == null ? KeyboardMode.Alphanumeric : mode;
        }

        /// <summary>Committed text.</summary>
        public string Text => text.ToString();
        /// <summary>Current uncommitted composition, segments and candidates.</summary>
        public MozcState Composition { get; private set; } = new MozcState("", null);
        public bool IsComposing => Composition.Preedit.Length > 0;
        public KeyboardMode Mode { get; private set; }
        /// <summary>One-shot shift for the next letter.</summary>
        public bool Shift { get; private set; }
        public bool CanUseKana => converter != null;

        /// <summary>
        /// When false, ENTER without a composition raises <see cref="Submitted"/>
        /// instead of inserting a newline.
        /// </summary>
        public bool MultiLine { get; set; } = true;

        /// <summary>Text added to <see cref="Text"/> (a conversion result or a typed character).</summary>
        public event Action<string> Committed;
        /// <summary>ENTER with nothing to convert in single-line mode; receives the whole text.</summary>
        public event Action<string> Submitted;
        /// <summary>Anything visible changed.</summary>
        public event Action Changed;

        public void Type(char key)
        {
            if (key < ' ' || key > '~') return;
            if (Shift && char.IsLetter(key)) key = char.ToUpperInvariant(key);
            Shift = false;
            if (Mode == KeyboardMode.Kana)
            {
                // Mozc keeps upper-case letters as letters inside the composition.
                Apply(converter.SendKey(key));
                return;
            }
            Append(key.ToString());
            Changed?.Invoke();
        }

        /// <summary>Kana: convert / next candidate (space when not composing). Alphanumeric: space.</summary>
        public void Space()
        {
            if (Mode == KeyboardMode.Kana && IsComposing) Apply(converter.SendSpecialKey("SPACE"));
            else { Append(" "); Changed?.Invoke(); }
        }

        /// <summary>Commits the composition; otherwise newline or <see cref="Submitted"/>.</summary>
        public void Enter()
        {
            if (IsComposing) { Apply(converter.SendSpecialKey("ENTER")); return; }
            if (MultiLine) { Append("\n"); Changed?.Invoke(); }
            else Submitted?.Invoke(Text);
        }

        public void Backspace()
        {
            if (IsComposing) { Apply(converter.SendSpecialKey("BACKSPACE")); return; }
            if (text.Length == 0) return;
            // Do not split a surrogate pair.
            int remove = text.Length >= 2 && char.IsLowSurrogate(text[text.Length - 1])
                && char.IsHighSurrogate(text[text.Length - 2]) ? 2 : 1;
            text.Length -= remove;
            Changed?.Invoke();
        }

        /// <summary>Moves within the composition (or between segments while converting).</summary>
        public void Left() { if (IsComposing) Apply(converter.SendSpecialKey("LEFT")); }
        public void Right() { if (IsComposing) Apply(converter.SendSpecialKey("RIGHT")); }
        /// <summary>Previous / next candidate while converting.</summary>
        public void Up() { if (IsComposing) Apply(converter.SendSpecialKey("UP")); }
        public void Down() { if (IsComposing) Apply(converter.SendSpecialKey("DOWN")); }

        /// <summary>Cancels the conversion, or the whole composition when not converting.</summary>
        public void Escape() { if (IsComposing) Apply(converter.SendSpecialKey("ESCAPE")); }

        public void SelectCandidate(int candidateId)
        {
            if (IsComposing) Apply(converter.SelectCandidate(candidateId));
        }

        /// <summary>Inserts text as it is (e.g. a paste), committing any composition first.</summary>
        public void Insert(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            CommitComposition();
            Append(value);
            Changed?.Invoke();
        }

        public void ToggleShift() { Shift = !Shift; Changed?.Invoke(); }

        public void ToggleMode() =>
            SetMode(Mode == KeyboardMode.Kana ? KeyboardMode.Alphanumeric : KeyboardMode.Kana);

        /// <summary>Switching mode commits the composition as it is.</summary>
        public void SetMode(KeyboardMode mode)
        {
            if (mode == KeyboardMode.Kana && converter == null) return;
            if (mode == Mode) return;
            CommitComposition();
            Mode = mode;
            Shift = false;
            Changed?.Invoke();
        }

        /// <summary>Commits any composition as it is (e.g. before reading the text elsewhere).</summary>
        public void CommitComposition()
        {
            if (IsComposing) Apply(converter.Submit());
        }

        /// <summary>Clears committed text. The composition is cancelled.</summary>
        public void Clear()
        {
            if (IsComposing)
            {
                // ESCAPE twice: leave conversion, then drop the preedit.
                Apply(converter.SendSpecialKey("ESCAPE"), commit: false);
                if (IsComposing) Apply(converter.SendSpecialKey("ESCAPE"), commit: false);
            }
            text.Clear();
            Changed?.Invoke();
        }

        private void Apply(MozcState state, bool commit = true)
        {
            if (state == null) return; // Engine error: keep the previous composition.
            Composition = state;
            if (commit && state.Committed.Length > 0) Append(state.Committed);
            Changed?.Invoke();
        }

        private void Append(string value)
        {
            text.Append(value);
            Committed?.Invoke(value);
        }
    }
}
