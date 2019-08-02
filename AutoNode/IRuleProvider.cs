using System.Collections.Generic;
using Umbraco.Core.Logging;

namespace DotSee.AutoNode
{
    public interface IRuleProvider
    {
        List<AutoNodeRule> GetRules(ILogger logger);
    }
}