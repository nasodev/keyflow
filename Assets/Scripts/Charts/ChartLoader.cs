using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using KeyFlow;

namespace KeyFlow.Charts
{
    public static class ChartLoader
    {
        private const int PitchMin = 36;
        private const int PitchMax = 83;

        public static ChartData LoadFromPath(string absolutePath)
        {
            if (!System.IO.File.Exists(absolutePath))
                throw new System.IO.FileNotFoundException($"Chart not found: {absolutePath}");
            return ParseJson(System.IO.File.ReadAllText(absolutePath));
        }

        public static IEnumerator LoadFromStreamingAssetsCo(
            string songId,
            System.Action<ChartData> onLoaded,
            System.Action<string> onError)
        {
            string path = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "charts", songId + ".kfchart");

#if UNITY_ANDROID && !UNITY_EDITOR
            var req = UnityEngine.Networking.UnityWebRequest.Get(path);
            yield return req.SendWebRequest();
            var result = req.result;
            var text = req.downloadHandler.text;
            var error = req.error;
            req.Dispose();
            if (result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"{path}: {error}");
                yield break;
            }
            ChartData loaded;
            try { loaded = ParseJson(text); }
            catch (System.Exception e) { onError?.Invoke(e.Message); yield break; }
            onLoaded?.Invoke(loaded);
#else
            ChartData chart;
            try { chart = LoadFromPath(path); }
            catch (System.Exception e) { onError?.Invoke(e.Message); yield break; }
            yield return null;  // yield once for symmetry with Android path
            onLoaded?.Invoke(chart);
#endif
        }

        public static ChartData ParseJson(string json)
        {
            var root = JObject.Parse(json);

            var chart = new ChartData
            {
                songId = (string)root["songId"],
                title = (string)root["title"],
                composer = (string)root["composer"],
                bpm = (int)root["bpm"],
                durationMs = (int)root["durationMs"],
                charts = new Dictionary<Difficulty, ChartDifficulty>()
            };

            var chartsObj = (JObject)root["charts"];
            foreach (var prop in chartsObj.Properties())
            {
                Difficulty diff = ParseDifficulty(prop.Name);
                var diffObj = (JObject)prop.Value;
                var cd = new ChartDifficulty
                {
                    totalNotes = (int)diffObj["totalNotes"],
                    notes = new List<ChartNote>()
                };
                foreach (var n in (JArray)diffObj["notes"])
                {
                    var note = new ChartNote
                    {
                        t = (int)n["t"],
                        lane = (int)n["lane"],
                        pitch = System.Math.Clamp((int)n["pitch"], PitchMin, PitchMax),
                        type = ParseType((string)n["type"]),
                        dur = (int)n["dur"]
                    };
                    Validate(note, chart.durationMs);
                    cd.notes.Add(note);
                }
                if (cd.notes.Count == 0)
                    throw new ChartValidationException($"{prop.Name} has no notes");
                for (int i = 1; i < cd.notes.Count; i++)
                {
                    if (cd.notes[i].t < cd.notes[i - 1].t)
                        throw new ChartValidationException($"{prop.Name} notes not sorted at idx {i}");
                }
                if (cd.totalNotes != cd.notes.Count)
                    throw new ChartValidationException(
                        $"{prop.Name} totalNotes {cd.totalNotes} != actual {cd.notes.Count}");
                chart.charts[diff] = cd;
            }
            return chart;
        }

        private static Difficulty ParseDifficulty(string s)
        {
            switch (s)
            {
                case "EASY": return Difficulty.Easy;
                case "NORMAL": return Difficulty.Normal;
                default: throw new ChartValidationException("Unknown difficulty: " + s);
            }
        }

        private static NoteType ParseType(string s)
        {
            switch (s)
            {
                case "TAP": return NoteType.TAP;
                case "HOLD": return NoteType.HOLD;
                default: throw new ChartValidationException("Unknown note type: " + s);
            }
        }

        private static void Validate(ChartNote n, int durationMs)
        {
            if (n.t < 0)
                throw new ChartValidationException($"t {n.t} negative");
            if (n.t > durationMs)
                throw new ChartValidationException($"t {n.t} exceeds durationMs {durationMs}");
            if (n.lane < 0 || n.lane > 3)
                throw new ChartValidationException($"lane {n.lane} out of range [0,3] at t={n.t}");
            if (n.dur < 0)
                throw new ChartValidationException($"dur {n.dur} negative at t={n.t}");
            if (n.type == NoteType.TAP && n.dur != 0)
                throw new ChartValidationException($"TAP must have dur=0, got {n.dur} at t={n.t}");
            if (n.type == NoteType.HOLD && n.dur <= 0)
                throw new ChartValidationException($"HOLD must have dur>0, got {n.dur} at t={n.t}");
        }
    }
}
