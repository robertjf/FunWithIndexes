using Examine;
using Examine.LuceneEngine;
using Examine.LuceneEngine.SearchCriteria;
using Examine.Providers;
using Examine.SearchCriteria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Mvc;
using YourITTeam.Core.Models.Content;
using YourITTeam.Core.Models.Search;

namespace YourITTeam.Core
{
    public static class SearchAssistant
    {
        internal const string SearcherGeneral = "ExternalSearcher";

        public static IEnumerable<MappedSearchResult> PerformContentSearch(this UmbracoHelper umbraco, string terms)
        {
            var provider = ExamineManager.Instance.SearchProviderCollection[SearcherGeneral];

            var bodyFields = new[]
            {
                "_content",
            };

            var titleFields = new[]
            {
                "_title",
            };

            var fields = bodyFields.Concat(titleFields.Concat(new[] {
                "_tags",
                "_location",
                "_categories",
                "_author"
                }));


            var results = provider.PerformSearch(terms, fields.ToArray());
            if (results == null)
            {
                yield break;
            }

            foreach (var item in results)
            {
                IPublishedContent publishedContent = null;
                try
                {
                    publishedContent = umbraco.TypedContent(item.Id);

                    while (publishedContent != null && publishedContent.Url == "#")
                    {
                        publishedContent = publishedContent.Parent;
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Send an alert.
                }

                // skip if the content is not published
                if (publishedContent == null) continue;

                // TODO: Check for child items that don't have their own url and depend on the parent.
                var srItem = new MappedSearchResult(publishedContent, publishedContent.Url, item.Score);

                // Set up the Title field with the first one available.
                foreach (var titleField in titleFields)
                {
                    if (item.Fields.ContainsKey(titleField) && !string.IsNullOrWhiteSpace(item.Fields[titleField]))
                    {
                        srItem.Title = item.Fields[titleField];
                        continue;
                    }
                }

                // Set up the summary field.
                foreach (var bodyField in bodyFields)
                {
                    if (item.Fields.ContainsKey(bodyField))
                    {
                        srItem.Summary += " " + item.Fields[bodyField];
                    }
                }

                srItem.Title = srItem.Title.HighlightTerms(terms);
                srItem.Summary = umbraco.StripHtml(srItem.Summary).ToString().HighlightTerms(terms);

                yield return srItem;
            }
        }

        public static IEnumerable<PageBase> GetPageSuggestions(this UmbracoHelper umbraco, HttpRequestBase request, bool useFuzzy = true)
        {
            var targetUrl = request.Url;

            // attempt to get a page name...
            var urlParts = targetUrl.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var filter = urlParts.Last().Replace('-', ' ');
            if (filter.Contains('.'))
            {
                filter = filter.Substring(0, filter.LastIndexOf('.'));
            }

            try
            {
                var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];
                var alternateTitle = "_title";
                var criteria = searcher.CreateSearchCriteria(BooleanOperation.Or);

                criteria.NodeName(filter.Boost(10.0f));
                criteria.NodeName(filter.MultipleCharacterWildcard());
                if (useFuzzy)
                {
                    criteria.NodeName(filter.Fuzzy(0.5f));
                }

                criteria.Field(alternateTitle, filter.Boost(7.0f));
                criteria.Field(alternateTitle, filter.MultipleCharacterWildcard());
                if (useFuzzy)
                {
                    criteria.Field(alternateTitle, filter.Fuzzy(0.8f));
                }

                var results = searcher.Search(criteria);
                if (results.TotalItemCount == 0 && !useFuzzy)
                {
                    // Try again with fuzzy search enabled
                    return umbraco.GetPageSuggestions(request, true);
                }
                return results.Select(r => umbraco.TypedContent(r.Id).As<PageBase>()).Where(p => p != null);
            }
            catch
            {

            }
            return Enumerable.Empty<PageBase>();
        }

        private static ISearchResults PerformSearch(this BaseSearchProvider provider, string terms, string[] fields)
        {
            var criteria = provider.CreateSearchCriteria(BooleanOperation.And);
            var filter = (terms ?? string.Empty).Trim('?', '*').Trim();

            // Set a flag indicating whether the criteria has been built or not.
            bool criteriaBuilt = false;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var singleTerms = filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var sb = new StringBuilder();
                var wrapTerm = false;
                if (filter.Contains(' '))
                {
                    wrapTerm = true;

                    // Proximity search on all terms:
                    foreach (var field in fields)
                        sb.Append($@"{field}: ""{filter}""~10 ");

                }

                // Basic and wildcard term search:
                foreach (var field in fields)
                {
                    sb.AppendFormat("{0}:{2}{1}{2} ", field, filter, wrapTerm ? '"' : (char?)null);
                    sb.AppendFormat("{0}:{2}{1}*{2} ", field, filter, wrapTerm ? '"' : (char?)null);
                }

                criteria.RawQuery(sb.ToString());
                criteriaBuilt = true;
            }

            if (criteriaBuilt)
            {
                return provider.Search(criteria);
            }

            return null;
        }


        internal static string HighlightTerms(this string content, string terms, int maxLength = 200)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content;
            // If terms contains spaces, then we want to split and match on each term. The search is using a Proximity search.
            var splitTerms = terms.ToLower().Split(' ').Distinct();
            // This is the number of characters each side of the highlighted term that we should preseve.
            int paddingLength = 10;
            if (content.Length > maxLength && terms.Length < maxLength) //unlikely to fail the latter, but let's validate it anyway.
            {
                // Cut off any text before the first match within a reasonable distance (10 characters)
                // We want to make sure the term has enough characters around each end.
                // Preserve Word boundaries.
                int firstOccurrence = -1;
                foreach (var term in splitTerms)
                {
                    int index = content.IndexOf(term, System.StringComparison.OrdinalIgnoreCase);
                    if (firstOccurrence == -1 || index < firstOccurrence)
                        firstOccurrence = index;
                }
                if (firstOccurrence > -1)
                {
                    int closestWhitespace = content.LastIndexOf(' ', firstOccurrence);
                    if (content.Length - closestWhitespace - paddingLength < maxLength) {
                        // Remove excess content from start and add ellipses
                        content = "... " + content.Substring(Math.Max(content.Length - maxLength - 4, 0));

                    }
                    else if (closestWhitespace > paddingLength && 
                            firstOccurrence + terms.Length + paddingLength < content.Length)
                    {
                        // Remove excess content from start and add ellipses
                        content = "... " + content.Substring(closestWhitespace - paddingLength);
                    }
                }
                // Now we can truncate the string before performing highlighting.
                if (content.Length > maxLength)
                    content = content.Truncate(maxLength);
            }
            //Use Regex to parse the content and replace occurences of term with <span class="higlight">term</span>.
            string replaceTemplate = @"<span class=""text-success"">${term}</span>";
            string searchTemplate = "(?!(<(.*></.*)>))(?<term>{0})";
            foreach (var term in splitTerms)
            {
                content = Regex.Replace(content, string.Format(searchTemplate, term), replaceTemplate, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture);
            }
            return content;
        }

        /// <summary>
        /// The strip html and limit length.
        /// </summary>
        /// <param name="html">
        /// The html.
        /// </param>
        /// <param name="length">
        /// The length.
        /// </param>
        /// <param name="addElipsis">
        /// The add elipsis.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string StripHtmlAndLimitLength(this string html, UmbracoHelper umbraco, int length = 200, bool addElipsis = true)
        {
            return umbraco.Truncate(umbraco.StripHtml(html).ToString(), length, addElipsis).ToString();
        }

    }
}
