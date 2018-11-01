using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using FunWithIndexes.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace FunWithIndexes.Core
{
    public static class SearchAssistant
    {
        internal const string SearcherGeneral = "ExternalSearcher";

        public static IEnumerable<MappedSearchResult> PerformContentSearch(this UmbracoHelper umbraco, string terms)
        {
            var provider = ExamineManager.Instance.SearchProviderCollection[SearcherGeneral];

            var bodyFields = new[]
            {
                "bodyText",
                "contents",
                "contactIntro",
                "excerpt",
            };

            var titleFields = new[]
            {

                "pageTitle",
            };

            var fields = bodyFields.Concat(titleFields);


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
                catch (Exception)
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

                yield return srItem;
            }
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
    }
}
