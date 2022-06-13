using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace DotSee.AutoNode
{
    public class AutoNodeServiceComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.AddSingleton<IConfigSource,ConfigSource>(
                x => {
                    return new ConfigSource { SourcePath = builder.BuilderHostingEnvironment.MapPathContentRoot(@"\App_Plugins\DotSee.AutoNode\autoNode.json") };
                });

            builder.Services.AddSingleton<IRuleProviderService, JsonFileRuleProviderService>();
            builder.Services.AddSingleton<AutoNodeService>();
            builder.Services.AddSingleton<AutoNodeUtils>();
            builder.AddNotificationHandler<ContentPublishedNotification, ContentPublishedHandler>();
        }      
    }
}