using DotSee.AutoNode.Properties;
using System;
using System.Collections.Generic;
using System.IO;
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

        public ConfigFileRuleProvider(ILogger logger)
        {
            _logger = logger;
        }

        private void GetOrUpdateXmlConfig()
        {
            _xmlConfig = (_xmlConfig == null) ? LoadXmlConfig() : _xmlConfig;
        }

        private XmlDocument LoadXmlConfig()
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
            return retVal;
        }
        
        public Dictionary<string, string> GetSettings()
        {
            GetOrUpdateXmlConfig();

            Dictionary<string, string> retVal = new Dictionary<string, string>();
            foreach (XmlAttribute attr in _xmlConfig.SelectSingleNode("/autoNode").Attributes)
            {
                retVal.Add(attr.Name, attr.Value);
            }
            return retVal;
        }

        public List<AutoNodeRule> GetRules()
        {
            GetOrUpdateXmlConfig();

            List<AutoNodeRule> retVal = new List<AutoNodeRule>();

            foreach (XmlNode xmlConfigEntry in _xmlConfig.SelectNodes("/autoNode/rule"))
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

            return retVal;
        }
    }
}