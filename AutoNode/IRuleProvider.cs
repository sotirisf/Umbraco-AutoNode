using System.Collections.Generic;
using Umbraco.Core.Logging;

namespace DotSee.AutoNode
{
    public interface IRuleProvider
    {
        Dictionary<string, string> GetSettings();
        List<AutoNodeRule> GetRules();
    }
}