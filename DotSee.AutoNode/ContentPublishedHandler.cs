using System.Linq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace DotSee.AutoNode
{
    public class ContentPublishedHandler : INotificationHandler<ContentPublishedNotification>
    {
        private readonly AutoNodeService _autoNodeService;

        public ContentPublishedHandler(AutoNodeService autoNodeService)
        {
            _autoNodeService = autoNodeService;
        }

        public void Handle(ContentPublishedNotification notification)
        {
            foreach (IContent node in notification.PublishedEntities)
            {
                if (!node.PublishedCultures.Any())
                {
                    _autoNodeService.Run(node);
                }
                else
                {
                    foreach (var culture in node.PublishedCultures)
                    {
                        _autoNodeService.Run(node, culture);
                    }
                }
            }
        }
    }
}