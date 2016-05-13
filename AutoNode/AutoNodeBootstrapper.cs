using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace DotSee
{
    public class AutoNodeBootstrapper : ApplicationEventHandler
    {

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarted(umbracoApplication, applicationContext);

            AutoNode au = AutoNode.Instance;

            ContentService.Saved += ContentServiceSaved;

        }

        private void ContentServiceSaved(IContentService sender, SaveEventArgs<IContent> args)
        {

            foreach (IContent node in args.SavedEntities)
            {
                AutoNode.Instance.Run(node);
            }
        }


    }
}
