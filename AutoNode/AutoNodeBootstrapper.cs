using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;

namespace DotSee
{
    public class AutoNodeBootstrapper : ApplicationEventHandler
    {

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarted(umbracoApplication, applicationContext);

            AutoNode au = AutoNode.Instance;

            ContentService.Saved += ContentService_Saved;
            ContentService.Published += ContentServicePublished;

        }

        private void ContentService_Saved(IContentService sender, SaveEventArgs<IContent> e)
        {
            foreach (var node in e.SavedEntities)
            {
                AutoNode.Instance.Run(node);
            }
        }

        private void ContentServicePublished(IPublishingStrategy sender, PublishEventArgs<IContent> e)
        {            
            foreach (var node in e.PublishedEntities)
            {
                AutoNode.Instance.RunPublish(node);
            }            
        }


    }
}
