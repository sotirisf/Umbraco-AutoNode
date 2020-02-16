using System.Collections.Generic;
using System.Xml;
using Umbraco.Core.Logging;

namespace DotSee.AutoNode
{
    public interface IRuleProvider
    {
        Dictionary<string, string> Settings { get; }
        IEnumerable<AutoNodeRule> Rules { get; }
        XmlDocument XmlConfig { get; }
        void ReloadData();
    }
}