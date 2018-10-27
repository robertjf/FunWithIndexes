using Examine;
using Examine.Providers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web;
using UmbracoExamine;

namespace FunWithIndexes.Core.Examine
{
    public class SearchIndexer : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            foreach (var provider in ExamineManager.Instance.IndexProviderCollection.AsEnumerable())
            {
                if (provider.Name.StartsWith("Internal"))
                {
                    continue;
                }

                provider.GatheringNodeData += provider_GatheringNodeData;
            }
        }

        private void provider_GatheringNodeData(object sender, IndexingNodeDataEventArgs e)
        {
            if (e.IndexType == IndexTypes.Content)
            {
                var node = ApplicationContext.Current.Services.ContentService.GetById(e.NodeId);
                InjectTagsWithoutComma(e, node);
            }
            else if (e.IndexType == IndexTypes.Media)
            {
                var node = ApplicationContext.Current.Services.MediaService.GetById(e.NodeId);
                InjectTagsWithoutComma(e, node);
            }
        }


        private void InjectTagsWithoutComma(IndexingNodeDataEventArgs e, IContentBase node)
        {
            foreach (var prop in node.PropertyTypes.Where(p => p.PropertyEditorAlias == Constants.PropertyEditors.TagsAlias))
            {
                var prevalues = ApplicationContext.Current.Services.DataTypeService.GetPreValuesCollectionByDataTypeId(prop.DataTypeDefinitionId).FormatAsDictionary();

                if (prevalues["storageType"].Value.ToLower() == "json")
                {
                    dynamic values = JsonConvert.DeserializeObject(node.GetValue<string>(prop.Alias));
                    StringBuilder newValues = new StringBuilder();
                    foreach (var value in values)
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
