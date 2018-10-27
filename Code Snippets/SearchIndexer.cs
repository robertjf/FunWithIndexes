using Examine;
using Examine.Providers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web;
using UmbracoExamine;

namespace YourITTeam.Core.EventHandlers
{
    public class ExamineIndexer : ApplicationEventHandler
    {

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            foreach (var provider in ExamineManager.Instance.IndexProviderCollection.AsEnumerable<BaseIndexProvider>())
            {
                if (provider.Name.StartsWith("Internal"))
                {
                    continue;
                }

                provider.GatheringNodeData += provider_GatheringNodeData;
            }
        }

        void provider_GatheringNodeData(object sender, IndexingNodeDataEventArgs e)
        {
            if (e.IndexType == IndexTypes.Content)
            {

                var node = ApplicationContext.Current.Services.ContentService.GetById(e.NodeId);
                InjectTagsWithoutComma(e, node);
                ExtractAuthorData(e, node);
                AggregateFields(e, node, "_title", new[] {
                    "pageTitle",
                    "subTitle"
                });

                AggregateFields(e, node, "_content", new[] {
                    "bodyContent",
                    "detail",
                    "extract",
                    "description",
                    "introduction",
                    "quote",
                    "heading",
                    "body",
                });
            }
            else if (e.IndexType == IndexTypes.Media)
            {
                var node = ApplicationContext.Current.Services.MediaService.GetById(e.NodeId);
                InjectTagsWithoutComma(e, node);
            }
        }

        /// <summary>
        /// Munge the fields into a new one identified by key for searching.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="node"></param>
        /// <param name="key"></param>
        /// <param name="fields"></param>
        private void AggregateFields(IndexingNodeDataEventArgs e, IContentBase node, string key, string[] fields)
        {
            var combinedFields = new StringBuilder();
            foreach (var prop in node.PropertyTypes.Where(p => fields.Contains(p.Alias)))
            {
                switch (prop.PropertyEditorAlias)
                {
                    // We want to attempt to retrieve the relevant fields from the properties that return JSON content.
                    case Constants.PropertyEditors.NestedContentAlias:
                    case "Our.Umbraco.StackedContent":
                        {
                            var editor = PropertyEditorResolver.Current.GetByAlias(prop.PropertyEditorAlias).ValueEditor;
                            var scJson = editor.ConvertDbToEditor(node.Properties.Single(p => p.Alias == prop.Alias),
                                                                prop,
                                                                ApplicationContext.Current.Services.DataTypeService) as JToken;
                            combinedFields.AppendLine(IndexNestedJObject(scJson, fields));
                            break;
                        }
                    case Constants.PropertyEditors.GridAlias:
                        {
                            var editor = PropertyEditorResolver.Current.GetByAlias(prop.PropertyEditorAlias).ValueEditor;
                            var gridJson = editor.ConvertDbToEditor(node.Properties.Single(p => p.Alias == prop.Alias),
                                                                prop,
                                                                ApplicationContext.Current.Services.DataTypeService) as JToken;
                            combinedFields.AppendLine(IndexNestedJObject(gridJson, new[] { "value", "caption" }));
                            break;
                        }

                    default:
                        combinedFields.AppendLine(node.GetValue<string>(prop.Alias));
                        break;
                }
            }
            e.Fields.Add(key, combinedFields.ToString());
        }

        /// <summary>
        /// Index the properties in a JToken object and return as a combined string
        /// </summary>
        /// <param name="token"></param>
        /// <param name="targetedFields">list of fields to extract data from - extracts all if null.</param>
        /// <returns></returns>
        private string IndexNestedJObject(JToken token, string[] targetedFields = null)
        {
            StringBuilder combined = new StringBuilder();
            if (token is JArray jArr)
            {
                foreach (var item in jArr)
                {
                    combined.AppendLine(IndexNestedJObject(item, targetedFields));
                }
            }
            if (token is JObject jObj)
            {
                foreach (var kvp in jObj)
                {
                    if (kvp.Value is JArray || kvp.Value is JObject)
                    {
                        combined.AppendLine(IndexNestedJObject(kvp.Value, targetedFields));
                    }
                    else if ((targetedFields != null && targetedFields.Contains(kvp.Key)) || targetedFields == null)
                    {
                        combined.AppendLine(kvp.Value.ToString());
                    }
                }
            }
            return combined.ToString();
        }

        private void ExtractAuthorData(IndexingNodeDataEventArgs e, IContentBase node)
        {
            // Munge the author for searching.
            foreach (var prop in node.PropertyTypes.Where(p => p.Alias == "author"))
            {
                var authorNode = ApplicationContext.Current.Services.ContentService.GetById(node.GetValue<Guid>(prop.Alias));
                if (authorNode != null)
                {
                    StringBuilder authorString = new StringBuilder();
                    authorString.AppendLine(authorNode.Name);
                    foreach (var authorProp in authorNode.Properties.AsEnumerable())
                    {
                        authorString.AppendLine($" {authorProp.Value}");
                    }
                    e.Fields.Add("_" + prop.Alias, authorString.ToString());
                }
            }
        }

        private void InjectTagsWithoutComma(IndexingNodeDataEventArgs e, IContentBase node)
        {
            foreach (var prop in node.PropertyTypes.Where(p => p.PropertyEditorAlias == Constants.PropertyEditors.TagsAlias))
            {
                var prevalues = prop.GetPrevalues();
                if (prevalues["storageType"].Value.ToLower() == "json")
                {
                    dynamic values = JsonConvert.DeserializeObject(node.GetValue<string>(prop.Alias));
                    StringBuilder newValues = new StringBuilder();
                    foreach(var value in values)
                    {
                        newValues.AppendLine(value.ToString());
                    }
                    e.Fields.Add("_" + prop.Alias, newValues.ToString());
                }
                else
                {
                    e.Fields.Add("_" + prop.Alias, node.GetValue<string>(prop.Alias).Replace(",", " "));
                }
            }
        }
    }
}
