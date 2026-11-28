using System;
using System.Text;
using UnityEngine;

namespace MozcShim
{
    /// <summary>
    /// One Mozc input session. Feed it key presses from any keyboard UI and read
    /// the preedit, candidates and committed text from the returned state.
    /// Not thread-safe; call from one thread (the native side serializes calls).
    /// </summary>
    public sealed class MozcSession : IKanaKanjiConverter, IDisposable
    {
        private readonly string id;

        public MozcState State { get; private set; } = new MozcState();

        /// <summary>Loads the dictionary and creates the engine once per process.</summary>
        public static bool InitializeEngine(string profileDir, string dataFilePath, out string error)
        {
            System.IO.Directory.CreateDirectory(profileDir);
            bool ok = MozcNative.Initialize(profileDir, dataFilePath);
            error = ok ? "" : MozcNative.LastError;
            // Same key map on every platform: romaji input, MS-IME style keys
            // (SPACE converts / next candidate, ENTER commits).
            if (ok) Eval("{\"input\":{\"type\":\"SET_CONFIG\",\"config\":" +
                         "{\"sessionKeymap\":\"MSIME\",\"preeditMethod\":\"ROMAN\"}}}");
            return ok;
        }

        public static string DataVersion => MozcNative.DataVersion;

        public MozcSession()
        {
            var created = Eval("{\"input\":{\"type\":\"CREATE_SESSION\"}}");
            id = created?.output?.id;
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException("CREATE_SESSION failed: " + MozcNative.LastError);
            // The desktop key map starts with the IME off; turn on Japanese input.
            SendSpecialKey("ON");
        }

        /// <summary>Printable ASCII key, e.g. 'n'. Romaji is converted to kana by Mozc.</summary>
        public MozcState SendKey(char key) =>
            SendKeyJson("{\"keyCode\":" + (int)key + "}");

        /// <summary>Mozc KeyEvent.SpecialKey name: SPACE, ENTER, BACKSPACE, LEFT, RIGHT, ESCAPE, ON, OFF…</summary>
        public MozcState SendSpecialKey(string specialKey) =>
            SendKeyJson("{\"specialKey\":\"" + specialKey + "\"}");

        /// <summary>Select a candidate by its id from <see cref="MozcState.Candidates"/>.</summary>
        public MozcState SelectCandidate(int candidateId) =>
            SendCommand("{\"type\":\"SELECT_CANDIDATE\",\"id\":" + candidateId + "}");

        /// <summary>Commit the current preedit or conversion.</summary>
        public MozcState Submit() => SendCommand("{\"type\":\"SUBMIT\"}");

        public void Dispose()
        {
            if (id != null) Eval("{\"input\":{\"type\":\"DELETE_SESSION\",\"id\":\"" + id + "\"}}");
        }

        private MozcState SendKeyJson(string keyJson) =>
            Update("{\"input\":{\"type\":\"SEND_KEY\",\"id\":\"" + id + "\",\"key\":" + keyJson + "}}");

        private MozcState SendCommand(string commandJson) =>
            Update("{\"input\":{\"type\":\"SEND_COMMAND\",\"id\":\"" + id + "\",\"command\":" + commandJson + "}}");

        private MozcState Update(string json)
        {
            var command = Eval(json);
            State = new MozcState(command?.output);
            return State;
        }

        private static CommandJson Eval(string json)
        {
            string response = MozcNative.Eval(json);
            if (response == null)
            {
                Debug.LogWarning("[Mozc] eval failed: " + MozcNative.LastError);
                return null;
            }
            return JsonUtility.FromJson<CommandJson>(response);
        }

        // --- Subset of mozc.commands.Command in protobuf JSON form ---
#pragma warning disable 0649
        [Serializable] internal sealed class CommandJson { public OutputJson output; }
        [Serializable] internal sealed class OutputJson
        {
            public string id; // uint64 is encoded as a JSON string.
            public bool consumed;
            public ResultJson result;
            public PreeditJson preedit;
            public CandidateWindowJson candidateWindow;
        }
        [Serializable] internal sealed class ResultJson { public string value; public string key; }
        [Serializable] internal sealed class PreeditJson { public int cursor; public SegmentJson[] segment; }
        [Serializable] internal sealed class SegmentJson { public string value; public string key; public string annotation; }
        [Serializable] internal sealed class CandidateWindowJson
        {
            public int focusedIndex;
            public int size;
            public CandidateJson[] candidate;
        }
        [Serializable] internal sealed class CandidateJson { public int id; public int index; public string value; }
#pragma warning restore 0649
    }

    /// <summary>Snapshot of the session after the last key or command.</summary>
    public sealed class MozcState
    {
        /// <summary>Text committed by the last key (empty if none).</summary>
        public string Committed { get; }
        /// <summary>Uncommitted text, all segments joined.</summary>
        public string Preedit { get; }
        public string[] Segments { get; }
        /// <summary>Candidates for the focused segment, if the candidate window is open.</summary>
        public (int Id, string Value)[] Candidates { get; }
        public int FocusedCandidateIndex { get; }
        public bool Consumed { get; }

        /// <summary>Builds a state directly, e.g. for tests or another converter.</summary>
        public MozcState(string committed, string[] segments,
            (int Id, string Value)[] candidates = null, int focusedCandidateIndex = -1)
        {
            Committed = committed ?? "";
            Segments = segments ?? Array.Empty<string>();
            Preedit = string.Concat(Segments);
            Candidates = candidates ?? Array.Empty<(int, string)>();
            FocusedCandidateIndex = focusedCandidateIndex;
            Consumed = true;
        }

        internal MozcState(MozcSession.OutputJson output = null)
        {
            Committed = output?.result?.value ?? "";
            var segments = output?.preedit?.segment ?? Array.Empty<MozcSession.SegmentJson>();
            Segments = Array.ConvertAll(segments, s => s.value ?? "");
            var preedit = new StringBuilder();
            foreach (var s in Segments) preedit.Append(s);
            Preedit = preedit.ToString();
            var candidates = output?.candidateWindow?.candidate ?? Array.Empty<MozcSession.CandidateJson>();
            Candidates = Array.ConvertAll(candidates, c => (c.id, c.value ?? ""));
            FocusedCandidateIndex = output?.candidateWindow?.focusedIndex ?? -1;
            Consumed = output?.consumed ?? false;
        }
    }
}
