// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Core.Models
{
    public class CitationUtils
    {
        /// <summary>
        /// Clips the text to a maximum length in case it exceeds the limit.
        /// Replaces the last 3 characters with "..."
        /// </summary>
        /// <param name="text">The text to clip.</param>
        /// <param name="maxLength">The max text length. Must be at least 4 characters long</param>
        /// <returns>The clipped text.</returns>
        public static string Snippet(string text, int maxLength)
        {
            if (text.Length <= maxLength)
            {
                return text;
            }

            string snippet = text.Substring(0, maxLength - 3).Trim();
            snippet += "...";
            return snippet;
        }

        /// <summary>
        /// Convert citation tags `[doc(s)n]` to `[n]` where n is a number.
        /// </summary>
        /// <param name="text">The text to format</param>
        /// <returns>The formatted text.</returns>
        public static string FormatCitationsResponse(string text)
        {
            return Regex.Replace(text, @"\[docs?(\d+)\]", "[$1]", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Filters out citations that are not referenced in the `text` as `[n]` tags (ex. `[1]` or `[2]`)
        /// </summary>
        /// <param name="text">Text that has citation tags.</param>
        /// <param name="citations">List of citations</param>
        /// <returns>List of citations found in the text, or null for no matches.</returns>
        public static List<ClientCitation>? GetUsedCitations(string text, List<ClientCitation> citations)
        {
            if (citations == null)
            {
                return null;
            }
            Regex regex = new(@"\[(\d+)\]");
            MatchCollection matches = regex.Matches(text);

            if (matches.Count == 0)
            {
                return null;
            }
            else
            {
                List<ClientCitation> usedCitations = [];
                foreach (Match match in matches)
                {
                    citations.Find((citation) =>
                    {
                        if ($"[{citation.Position}]" == match.Value)
                        {
                            if (usedCitations.Any(a => a.Position == citation.Position))
                            { // only add citation once if it has already been added to the list
                                return false;
                            }
                            usedCitations.Add(citation);
                            return true;
                        }
                        return false;
                    });
                }
                return usedCitations.Count > 0 ? usedCitations : null;
            }
        }
    }
}
