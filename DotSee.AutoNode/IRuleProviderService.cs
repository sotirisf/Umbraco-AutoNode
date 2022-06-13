using System.Collections.Generic;
using System.Xml;

namespace DotSee.AutoNode
{
    public interface IRuleProviderService
    {
        Dictionary<string, string> Settings { get; }
        IEnumerable<Rule> Rules { get; }
        //XmlDocument XmlConfig { get; }
        void ReloadData();

        IConfigSource ConfigSource { get; }
    }

    public interface IRuleProviderService<T>:IRuleProviderService where T : class
    {
        T ConfigType { get; }
    }
    public interface IConfigSource
    {
        string SourcePath { get; set; }
    }
  

   
}