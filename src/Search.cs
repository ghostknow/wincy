using System;
using System.Collections.Generic;
using System.Globalization;

namespace Wincy
{
    internal sealed class MatchResult
    {
        public bool Matched;
        public int Score;
        public int[] Positions = EmptyPositions;

        public static readonly int[] EmptyPositions = new int[0];
        public static readonly MatchResult NoMatch = new MatchResult();

        public static MatchResult Yes(int score, int[] positions)
        {
            MatchResult r = new MatchResult();
            r.Matched = true;
            r.Score = score;
            r.Positions = positions ?? EmptyPositions;
            return r;
        }
    }

    /// <summary>
    /// Ranking used by the search field: exact match beats prefix, prefix beats
    /// substring, substring beats loose subsequence ("cpb" → "Clipboard").
    /// Positions are reported for highlight painting.
    /// </summary>
    internal static class Fuzzy
    {
        public static MatchResult Match(string text, string query)
        {
            if (string.IsNullOrEmpty(query)) return MatchResult.Yes(0, null);
            if (string.IsNullOrEmpty(text)) return MatchResult.NoMatch;

            // Keep the scan bounded: huge clipboards would otherwise cost per keystroke.
            int limit = Math.Min(text.Length, 20000);
            int qlen = query.Length;

            int index = IndexOfIgnoreCase(text, query, limit);
            if (index == 0)
            {
                int[] positions = new int[qlen];
                for (int i = 0; i < qlen; i++) positions[i] = i;
                int score = 9000 - Math.Min(600, text.Length / 8);
                if (text.Length == qlen) score += 1000;
                return MatchResult.Yes(score, positions);
            }
            if (index > 0)
            {
                int[] positions = new int[qlen];
                for (int i = 0; i < qlen; i++) positions[i] = index + i;
                int score = 8000 - Math.Min(1500, index * 6);
                if (IsBoundary(text[index - 1])) score += 350;
                if (index + qlen == text.Length) score += 100;
                return MatchResult.Yes(score, positions);
            }

            // Loose subsequence.
            int[] hits = new int[qlen];
            int hitCount = 0;
            int qi = 0;
            for (int i = 0; i < limit && qi < qlen; i++)
            {
                if (Lower(text[i]) == Lower(query[qi]))
                {
                    hits[hitCount++] = i;
                    qi++;
                }
            }
            if (qi < qlen) return MatchResult.NoMatch;
            if (hitCount > qlen) Array.Resize(ref hits, qlen);
            int span = hits[qlen - 1] - hits[0] + 1;
            int looseScore = 4000 - Math.Min(2000, (span - qlen) * 12) - Math.Min(500, hits[0] * 3);
            for (int i = 0; i < qlen; i++)
            {
                if (i == 0 || IsBoundary(text[hits[i] - 1])) looseScore += 60;
            }
            return MatchResult.Yes(looseScore, hits);
        }

        /// <summary>
        /// Predictable substring match (what the popup uses): the query must appear
        /// verbatim, ignoring case, so results never look unrelated to what was typed.
        /// </summary>
        public static MatchResult Substring(string text, string query)
        {
            if (string.IsNullOrEmpty(query)) return MatchResult.Yes(0, null);
            if (string.IsNullOrEmpty(text)) return MatchResult.NoMatch;
            int limit = Math.Min(text.Length, 20000);
            int index = IndexOfIgnoreCase(text, query, limit);
            if (index < 0) return MatchResult.NoMatch;
            int[] positions = new int[query.Length];
            for (int i = 0; i < positions.Length; i++) positions[i] = index + i;
            int score = index == 0 ? 9000 + (text.Length == query.Length ? 1000 : 0) : 8000 - Math.Min(1500, index * 6);
            if (index > 0 && IsBoundary(text[index - 1])) score += 350;
            return MatchResult.Yes(score, positions);
        }

        private static char Lower(char c)
        {
            return char.ToLowerInvariant(c);
        }

        private static bool IsBoundary(char c)
        {
            return char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '/' || c == '\\' || c == '.' ||
                   c == ':' || c == '(' || c == '[' || c == '{' || c == '"' || c == '\'';
        }

        private static int IndexOfIgnoreCase(string text, string query, int limit)
        {
            int qlen = query.Length;
            int last = Math.Min(text.Length, limit) - qlen;
            for (int i = 0; i <= last; i++)
            {
                int j = 0;
                while (j < qlen && Lower(text[i + j]) == Lower(query[j])) j++;
                if (j == qlen) return i;
            }
            return -1;
        }
    }

    internal static class Humanize
    {
        public static string Ago(DateTime utc)
        {
            DateTime local = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            TimeSpan delta = DateTime.Now - local;
            if (delta.TotalSeconds < 0) delta = TimeSpan.Zero;
            if (delta.TotalSeconds < 45) return I18n.T("time.now");
            if (delta.TotalMinutes < 60) return I18n.T("time.min", Math.Max(1, (int)Math.Round(delta.TotalMinutes)));
            if (delta.TotalHours < 24) return I18n.T("time.hour", Math.Max(1, (int)Math.Round(delta.TotalHours)));
            DateTime today = DateTime.Today;
            if (local.Date == today.AddDays(-1)) return I18n.T("time.yesterday");
            if (delta.TotalDays < 7) return I18n.T("time.day", Math.Max(2, (int)Math.Round(delta.TotalDays)));
            if (local.Year == today.Year) return local.ToString("M/d", CultureInfo.CurrentCulture);
            return local.ToString("yyyy/M/d", CultureInfo.CurrentCulture);
        }


    }
}
