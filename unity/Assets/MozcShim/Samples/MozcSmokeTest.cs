using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using MozcShim;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Device check for the Mozc shim. Add to any GameObject in an empty scene and run
/// on Quest. Results go to logcat with the tag [MozcSmoke]:
///   adb logcat -s Unity | grep MozcSmoke
/// Measures data install, engine start-up, per-key latency, conversion latency
/// and resident memory, then checks nihongo -> 日本語.
/// </summary>
public sealed class MozcSmokeTest : MonoBehaviour
{
    [Tooltip("Optional on-screen output. Japanese glyphs need a font that includes them.")]
    public TextMesh output;

    private readonly StringBuilder report = new StringBuilder();
    private int failures;

    private IEnumerator Start()
    {
        Log($"device={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} rss={RssKb()} kB");

        var watch = Stopwatch.StartNew();
        string dataPath = null, error = null;
        yield return MozcDataInstaller.Install((path, err) => { dataPath = path; error = err; });
        if (dataPath == null) { Fail("data install: " + error); yield break; }
        Log($"data install: {watch.ElapsedMilliseconds} ms, {new FileInfo(dataPath).Length} bytes at {dataPath}");

        long rssBefore = RssKb();
        watch.Restart();
        if (!MozcSession.InitializeEngine(MozcDataInstaller.ProfileDirectory, dataPath, out error))
        { Fail("initialize: " + error); yield break; }
        Log($"initialize: {watch.ElapsedMilliseconds} ms, data version {MozcSession.DataVersion}, " +
            $"rss +{RssKb() - rssBefore} kB (now {RssKb()} kB)");

        using (var session = new MozcSession())
        {
            // First choice committed directly.
            MozcState state = TypeAndConvert(session, "nihongo", "にほんご");
            state = session.SendSpecialKey("ENTER");
            Check(state.Committed == "日本語", $"  commit {state.Committed}");

            // First choice is not the wanted one (OSS dictionary gives 感じ変換):
            // open the candidate list and pick 漢字 for the first segment.
            TypeAndConvert(session, "kannjihenkann", "かんじへんかん");
            state = session.SendSpecialKey("SPACE"); // Next candidate; opens the list.
            var top = new StringBuilder();
            int kanji = -1;
            foreach (var (id, value) in state.Candidates)
            {
                if (top.Length < 80) top.Append(value).Append(' ');
                if (value == "漢字" && kanji < 0) kanji = id;
            }
            Log($"  candidates: {top}");
            Check(kanji >= 0, "  漢字 offered for the first segment");
            if (kanji >= 0) session.SelectCandidate(kanji);
            state = session.SendSpecialKey("ENTER");
            Check(state.Committed.StartsWith("漢字"), $"  commit {state.Committed}");

            // Warm second run, closer to steady-state typing.
            TypeAndConvert(session, "nihongo", "にほんご");
            session.SendSpecialKey("ENTER");
        }

        Log(failures == 0 ? "[ PASSED ]" : $"[ FAILED ] {failures}");
    }

    /// <summary>Types romaji, checks the kana preedit, converts once and logs timings.</summary>
    private MozcState TypeAndConvert(MozcSession session, string romaji, string expectedKana)
    {
        var watch = Stopwatch.StartNew();
        MozcState state = null;
        double slowestKey = 0;
        foreach (char c in romaji)
        {
            double before = watch.Elapsed.TotalMilliseconds;
            state = session.SendKey(c);
            slowestKey = System.Math.Max(slowestKey, watch.Elapsed.TotalMilliseconds - before);
        }
        double typing = watch.Elapsed.TotalMilliseconds;
        Check(state.Preedit == expectedKana, $"{romaji} -> preedit {state.Preedit}");

        watch.Restart();
        state = session.SendSpecialKey("SPACE");
        double conversion = watch.Elapsed.TotalMilliseconds;
        Log($"{romaji}: typing {typing:F2} ms ({typing / romaji.Length:F2} ms/key, slowest {slowestKey:F2}), " +
            $"conversion {conversion:F2} ms, first choice '{state.Preedit}'");
        return state;
    }

    private void Check(bool ok, string message)
    {
        if (!ok) failures++;
        Log((ok ? "OK " : "NG ") + message);
    }

    private void Fail(string message)
    {
        failures++;
        Log("ERROR " + message);
        Log("[ FAILED ]");
    }

    private void Log(string message)
    {
        Debug.Log("[MozcSmoke] " + message);
        report.AppendLine(message);
        if (output != null) output.text = report.ToString();
    }

    /// <summary>Resident set size from /proc (Android/Linux); -1 elsewhere.</summary>
    private static long RssKb()
    {
        try
        {
            foreach (var line in File.ReadAllLines("/proc/self/status"))
                if (line.StartsWith("VmRSS:"))
                    return long.Parse(line.Substring(6).Trim().Split(' ')[0]);
        }
        catch (System.Exception) { }
        return -1;
    }
}
