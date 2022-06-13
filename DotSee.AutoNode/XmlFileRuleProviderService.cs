using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace DotSee.AutoNode
{
    public class XmlFileRuleProviderService : IRuleProviderService<XmlDocument>
    {
        private readonly ILogger _logger;
        private readonly IConfigSource _configSource;
        private XmlDocument _xmlConfig = null;
        private IEnumerable<Rule> _rules;
        private Dictionary<string, string> _settings;
        public XmlDocument ConfigType => _xmlConfig ?? GetConfigFromXml();

        public XmlFileRuleProviderService(ILogger logger, IConfigSource configSource)
        {
            _logger = logger;

            _configSource = configSource;
            _settings = new Dictionary<string, string>();
        }

        public IEnumerable<Rule> Rules
        {
            get
            {
                return (_rules == null || !_rules.Any()) ? GetRules() : _rules;
            }
        }

        public Dictionary<string, string> Settings
        {
            get
            {
                return (_settings == null || !_settings.Any()) ? GetSettings() : _settings;
            }
        }

        IConfigSource IRuleProviderService.ConfigSource { get => _configSource; }

        public void ReloadData()
        {
            _rules = null;
            _settings = new Dictionary<string, string>();
            _xmlConfig = null;
        }

        private XmlDocument GetConfigFromXml()
        {
            XmlDocument xmlConfig = new XmlDocument();

            try
            {
                xmlConfig.Load(_configSource.SourcePath);
            }
            catch (FileNotFoundException ex)
            {
                _logger.Error(ex, MessageConstants.ErrorConfigNotFound);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, MessageConstants.ErrorLoadConfig);
                return null;
            }

            _logger.Information("AutoNode: Loading configuration complete");
            return xmlConfig;
        }

        private Dictionary<string, string> GetSettings()
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            foreach (XmlAttribute attr in ConfigType.SelectSingleNode("/autoNode").Attributes)
            {
                retVal.Add(attr.Name, attr.Value);
            }

            _settings = retVal;

            return retVal;
        }

        private List<Rule> GetRules()
        {
            List<Rule> retVal = new List<Rule>();

            foreach (XmlNode xmlConfigEntry in ConfigType.SelectNodes("/autoNode/rule"))
            {
                if (xmlConfigEntry.NodeType == XmlNodeType.Element)
                {
                    string createdDocTypeAlias = xmlConfigEntry.Attributes["createdDocTypeAlias"].Value;
                    string docTypeAliasToCreate = xmlConfigEntry.Attributes["docTypeAliasToCreate"].Value;
                    string nodeName = xmlConfigEntry.Attributes["nodeName"].Value;

                    bool bringNewNodeFirst = (xmlConfigEntry.Attributes["bringNewNodeFirst"] != null)
                        ? bool.Parse(xmlConfigEntry.Attributes["bringNewNodeFirst"].Value)
                        : false;

                    bool onlyCreateIfNoChildren = (xmlConfigEntry.Attributes["onlyCreateIfNoChildren"] != null)
                        ? bool.Parse(xmlConfigEntry.Attributes["onlyCreateIfNoChildren"].Value)
                        : false;

                    bool createIfExistsWithDifferentName = (xmlConfigEntry.Attributes["createIfExistsWithDifferentName"] != null)
                        ? bool.Parse(xmlConfigEntry.Attributes["createIfExistsWithDifferentName"].Value)
                        : true;

                    string dictionaryItemForName = (xmlConfigEntry.Attributes["dictionaryItemForName"] != null)
                      ? xmlConfigEntry.Attributes["dictionaryItemForName"].Value
                      : "";

                    bool keepNewNodeUnpublished = (xmlConfigEntry.Attributes["keepNewNodeUnpublished"] != null)
                      ? bool.Parse(xmlConfigEntry.Attributes["keepNewNodeUnpublished"].Value)
                      : false;

                    string blueprint = (xmlConfigEntry.Attributes["blueprint"] != null)
                      ? xmlConfigEntry.Attributes["blueprint"].Value
                      : "";

                    var rule = new Rule(
                            createdDocTypeAlias
                            , docTypeAliasToCreate
                            , nodeName
                            , bringNewNodeFirst
                            , onlyCreateIfNoChildren
                            , createIfExistsWithDifferentName
                            , dictionaryItemForName
                            , keepNewNodeUnpublished
                            , blueprint);

                    retVal.Add(rule);
                }
            }
            _logger.Information(MessageConstants.InfoLoadConfigComplete);

            _rules = retVal;

            return retVal;
        }
    }
}