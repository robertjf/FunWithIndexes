using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace FunWithIndexes.Core.ViewModels
{
    [DataContract(Name = "searchResult")]
    public class MappedSearchResult
    {
        internal protected IPublishedContent Node { get; private set; }

        public MappedSearchResult(IPublishedContent node, string url, float score)
        {
            Node = node;
            ResultType = node.DocumentTypeAlias;

            Id = node.Id;
            Title = node.Name;
            Url = url;
            Score = score;
            Summary = string.Empty;

        }

        [DataMember(Name = "resultType")]
        public string ResultType { get; protected set; }

        [DataMember(Name = "id")]
        public object Id { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "content")]
        public string Summary { get; set; }

        [DataMember(Name = "score")]
        public float Score { get; set; }
    }
}
