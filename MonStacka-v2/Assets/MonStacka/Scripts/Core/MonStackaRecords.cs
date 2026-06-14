using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace MonStacka.Core
{
    /// <summary>
    /// Local top-3 records persistence (PlayerPrefs). O.G.B.M. keeps best scores;
    /// X(4)-LINES keeps best times. Sprint runs where a held assist actually fired
    /// are recorded on a separate assisted board so pure sprint integrity is kept
    /// (handoff: "separate sprint records with assists from pure sprint records").
    /// </summary>
    public static class MonStackaRecords
    {
        public const int MaxEntries = 3;

        private const string OgbmKey = "monstacka.records.ogbm.scores";
        private const string OgbmZanyKey = "monstacka.records.ogbm.zany.scores";
        private const string SprintPureKey = "monstacka.records.sprint.pure";
        private const string SprintAssistedKey = "monstacka.records.sprint.assisted";

        /// <summary>Returns true when the score made the board.</summary>
        public static bool TryAddOgbmScore(int score, bool zany = false)
        {
            if (score <= 0)
            {
                return false;
            }

            var scores = LoadValues(zany ? OgbmZanyKey : OgbmKey);
            scores.Add(score);
            var kept = scores.OrderByDescending(value => value).Take(MaxEntries).ToList();
            SaveValues(zany ? OgbmZanyKey : OgbmKey, kept);
            return kept.Contains(score);
        }

        /// <summary>Returns true when the completed sprint time made its board.</summary>
        public static bool TryAddSprintTime(int milliseconds, bool zany)
        {
            if (milliseconds <= 0)
            {
                return false;
            }

            var key = zany ? SprintAssistedKey : SprintPureKey;
            var times = LoadValues(key);
            times.Add(milliseconds);
            var kept = times.OrderBy(value => value).Take(MaxEntries).ToList();
            SaveValues(key, kept);
            return kept.Contains(milliseconds);
        }

        public static IReadOnlyList<int> GetOgbmScores(bool zany = false) =>
            LoadValues(zany ? OgbmZanyKey : OgbmKey);

        public static IReadOnlyList<int> GetSprintTimes(bool zany) =>
            LoadValues(zany ? SprintAssistedKey : SprintPureKey);

        /// <summary>Display rows for the in-match leaderboard panel, padded to MaxEntries.</summary>
        public static List<string> GetDisplayRows(MonStackaMode mode, bool zany = false)
        {
            var rows = new List<string>(MaxEntries);
            switch (mode)
            {
                case MonStackaMode.Ogbm:
                    rows.AddRange(GetOgbmScores(zany).Select(score => score.ToString(CultureInfo.InvariantCulture)));
                    break;
                case MonStackaMode.Sprint40:
                    rows.AddRange(GetSprintTimes(zany).Select(FormatMs));
                    break;
            }

            while (rows.Count < MaxEntries)
            {
                rows.Add("---");
            }

            return rows;
        }

        public static string FormatMs(int totalMs)
        {
            var minutes = totalMs / 60000;
            var seconds = (totalMs / 1000) % 60;
            var milliseconds = totalMs % 1000;
            return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
        }

        private static List<int> LoadValues(string key)
        {
            var raw = PlayerPrefs.GetString(key, string.Empty);
            var values = new List<int>(MaxEntries);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return values;
            }

            foreach (var token in raw.Split(','))
            {
                if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        private static void SaveValues(string key, IReadOnlyList<int> values)
        {
            PlayerPrefs.SetString(key, string.Join(",", values));
            PlayerPrefs.Save();
        }
    }
}
