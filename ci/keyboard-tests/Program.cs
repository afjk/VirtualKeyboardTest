using System;
using System.Collections.Generic;
using MozcShim;

/// <summary>Stand-in engine: keeps the typed letters as the preedit; SPACE converts
/// to "変換(letters)" with two candidates; ENTER/Submit commit the shown value.</summary>
sealed class FakeConverter : IKanaKanjiConverter
{
    private string reading = "", shown = "";
    private bool converting;
    private static readonly (int, string)[] NoCandidates = Array.Empty<(int, string)>();

    private MozcState State(string committed = "") => new MozcState(committed,
        shown.Length > 0 ? new[] { shown } : null,
        converting ? new[] { (1, "変換" + reading), (2, "候補" + reading) } : NoCandidates,
        converting ? 0 : -1);

    private MozcState Commit()
    {
        string value = shown;
        reading = shown = "";
        converting = false;
        return State(value);
    }

    public MozcState SendKey(char key) { if (converting) Commit(); reading += key; shown = reading; return State(); }
    public MozcState Submit() => Commit();
    public MozcState SelectCandidate(int id) { shown = (id == 1 ? "変換" : "候補") + reading; return State(); }
    public MozcState SendSpecialKey(string key)
    {
        switch (key)
        {
            case "SPACE": if (reading.Length > 0) { converting = true; shown = "変換" + reading; } return State();
            case "ENTER": return Commit();
            case "BACKSPACE":
                if (converting) { converting = false; shown = reading; }
                else if (reading.Length > 0) { reading = reading.Substring(0, reading.Length - 1); shown = reading; }
                return State();
            case "ESCAPE":
                if (converting) { converting = false; shown = reading; } else { reading = shown = ""; }
                return State();
            default: return State();
        }
    }
}

static class Program
{
    static int failures;
    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "[OK] " : "[NG] ") + what);
        if (!ok) failures++;
    }

    static int Main()
    {
        // Kana: compose, convert, choose a candidate, commit.
        var b = new JapaneseKeyboardBuffer(new FakeConverter());
        var committed = new List<string>();
        b.Committed += committed.Add;
        b.Type('a'); b.Type('b');
        Check(b.IsComposing && b.Text == "" && b.Composition.Preedit == "ab", "typing composes without committing");
        b.Space();
        Check(b.Composition.Candidates.Length == 2, "space converts and lists candidates");
        b.SelectCandidate(2);
        b.Enter();
        Check(b.Text == "候補ab" && !b.IsComposing, "enter commits the chosen candidate");
        Check(committed.Count == 1 && committed[0] == "候補ab", "Committed event carries the result");

        // Space without composition is a space; enter is a newline in multi-line mode.
        b.Space(); b.Enter();
        Check(b.Text == "候補ab \n", "space and newline when nothing is composed");

        // Backspace: inside the composition first, then committed text.
        b.Type('x'); b.Backspace();
        Check(!b.IsComposing && b.Text == "候補ab \n", "backspace removes the composition before the text");
        b.Backspace();
        Check(b.Text == "候補ab ", "backspace removes committed text");
        b.Insert("😀");
        b.Backspace();
        Check(b.Text == "候補ab ", "backspace removes a whole surrogate pair");

        // Mode switch commits the composition as it is; shift is one-shot.
        b.Type('k');
        b.ToggleMode();
        Check(b.Mode == KeyboardMode.Alphanumeric && b.Text == "候補ab k", "switching to 英数 commits the composition");
        b.ToggleShift(); b.Type('h'); b.Type('i');
        Check(b.Text.EndsWith("Hi"), "shift capitalises one letter in 英数");

        // Single-line: enter submits the whole text.
        string submitted = null;
        b.MultiLine = false;
        b.Submitted += t => submitted = t;
        b.Enter();
        Check(submitted == b.Text, "single-line enter raises Submitted");

        // Clear drops the composition without committing it.
        b.SetMode(KeyboardMode.Kana);
        b.Type('z');
        b.Clear();
        Check(b.Text == "" && !b.IsComposing, "clear drops text and composition");

        // Without an engine: alphanumeric only.
        var plain = new JapaneseKeyboardBuffer(null);
        plain.SetMode(KeyboardMode.Kana);
        plain.Type('a');
        Check(plain.Mode == KeyboardMode.Alphanumeric && plain.Text == "a" && !plain.CanUseKana,
            "no engine: stays in 英数");

        Console.WriteLine(failures == 0 ? "[ PASSED ]" : $"[ FAILED ] {failures}");
        return failures == 0 ? 0 : 1;
    }
}
