using DotSee.AutoNode.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using System.Xml;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;

namespace DotSee.AutoNode
{
    public class ConfigFileRuleProvider : IRuleProvider
    {

        private XmlDocument _xmlConfig = null;
        private ILogger _logger;
        private IEnumerable<AutoNodeRule> _rules;
        private Dictionary<string, string> _settings;

        public ConfigFileRuleProvider(ILogger logger)
        {
            _logger = logger;
            _settings = new Dictionary<string, string>();
        }

        public XmlDocument XmlConfig
        {
            get
            {
                return (_xmlConfig == null) ? GetConfigFromXml() : _xmlConfig;
            }
        }

        public IEnumerable<AutoNodeRule> Rules
        {
            get { 
            return (_rules == null || !_rules.Any()) ? GetRules() : _rules;
            }
        }

        public Dictionary<string,string> Settings
        {
            get
            {
                return (_settings == null || !_settings.Any()) ? GetSettings() : _settings;
            }
        }

        public void ReloadData()
        {
            _rules = null;
            _settings = new Dictionary<string,string>();
            _xmlConfig = null;
        }

        private XmlDocument GetConfigFromXml()
        {
            XmlDocument retVal = new XmlDocument();
            try
            {
                IGlobalSettings gs = new GlobalSettings();
                retVal.Load(HostingEnvironment.MapPath(gs.Path + "/../config/autoNode.config"));
            }
            catch (FileNotFoundException ex)
            {
                _logger.Error<AutoNode>(ex, Resources.ErrorConfigNotFound);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error<AutoNode>(ex, Resources.ErrorLoadConfig);
                return null;
            }
            _logger.Info<AutoNode>(Resources.InfoLoadingConfig);
            _xmlConfig = retVal;
            return retVal;
        }
        
        private Dictionary<string, string> GetSettings()
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            foreach (XmlAttribute attr in XmlConfig.SelectSingleNode("/autoNode").Attributes)
            {
                retVal.Add(attr.Name, attr.Value);
            }

            _settings = retVal;

            return retVal;
        }

        private List<AutoNodeRule> GetRules()
        {
            List<AutoNodeRule> retVal = new List<AutoNodeRule>();

            foreach (XmlNode xmlConfigEntry in XmlConfig.SelectNodes("/autoNode/rule"))
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

                    var rule = new AutoNodeRule(
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
            _logger.Info<AutoNode>(Resources.InfoLoadConfigComplete);

            _rules = retVal;

            return retVal;
        }
    }
}