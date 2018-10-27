using FunWithIndexes.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using Umbraco.Web.WebApi.Filters;

namespace FunWithIndexes.Core.Controllers
{
    [PluginController("SiteSearch")]
    public class SearchApiController : UmbracoApiController
    {
        [HttpGet]
        [HttpQueryStringFilter("queryStrings")]
        public PagedResult<MappedSearchResult> GetSearchResults(FormDataCollection queryStrings)
        {
            int pageNumber = queryStrings.HasKey("pageNumber") ? queryStrings.GetValue<int>("pageNumber") : 1;
            int pageSize = queryStrings.HasKey("pageSize") ? queryStrings.GetValue<int>("pageSize") : 0;

            string filter = queryStrings.HasKey("filter") ? queryStrings.GetValue<string>("filter") : string.Empty;

            if (string.IsNullOrEmpty(filter))
                return new PagedResult<MappedSearchResult>(0, pageNumber, pageSize);

            // todo: consider caching search results in session for paging optimisation.
            var results = Umbraco.PerformContentSearch(filter);
            var count = results.Count();
            var pagedResult = new PagedResult<MappedSearchResult>(
               count,
               pageNumber,
               pageSize);

            if (count > 0)
            {
                if (pageSize > 0)
                {
                    int skipCount = (pageNumber > 0 && pageSize > 0) ? Convert.ToInt32((pageNumber - 1) * pageSize) : 0;
                    if (count < skipCount)
                    {
                        skipCount = count / pageSize;
                    }

                    pagedResult.Items = results.Skip(skipCount).Take(pageSize);
                }
                else
                    pagedResult.Items = results;
            }
            return pagedResult;
        }
    }
}
