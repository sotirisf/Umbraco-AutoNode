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
            var hasBeenExcute = false;
            foreach (IContent node in args.SavedEntities)
            {
                hasBeenExcute = AutoNode.Instance.Run(node);
            }
            if (hasBeenExcute)
            {
                umbraco.content.Instance.RefreshContentFromDatabase();
            }
        }


    }
}
