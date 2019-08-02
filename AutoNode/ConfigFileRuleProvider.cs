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
        public List<AutoNodeRule> GetRules(ILogger logger)
        {
            XmlDocument xmlConfig = new XmlDocument();
            List<AutoNodeRule> retVal = new List<AutoNodeRule>();

            try
            {
                IGlobalSettings gs = new GlobalSettings();
                xmlConfig.Load(HostingEnvironment.MapPath(gs.Path + "/../config/autoNode.config"));
            }
            catch (FileNotFoundException ex)
            {
                logger.Error<AutoNode>(ex, Resources.ErrorConfigNotFound);

                return null;
            }
            catch (Exception ex)
            {
                logger.Error<AutoNode>(ex, Resources.ErrorLoadConfig);
                return null;
            }
            logger.Info<AutoNode>(Resources.InfoLoadingConfig);

            foreach (XmlNode xmlConfigEntry in xmlConfig.SelectNodes("/autoNode/rule"))
            {
                if (xmlConfigEntry.NodeType == XmlNodeType.Element)
                {
                    string CreatedDocTypeAlias = xmlConfigEntry.Attributes["createdDocTypeAlias"].Value;
                    string DocTypeAliasToCreate = xmlConfigEntry.Attributes["docTypeAliasToCreate"].Value;
                    string NodeName = xmlConfigEntry.Attributes["nodeName"].Value;

                    bool BringNewNodeFirst = (xmlConfigEntry.Attributes["bringNewNodeFirst"] != null)
                        ? bool.Parse(xmlConfigEntry.Attributes["bringNewNodeFirst"].Value)
                        : false;

                    bool OnlyCreateIfNoChildren = (xmlConfigEntry.Attributes["onlyCreateIfNoChildren"] != null)
                        ? bool.Parse(xmlConfigEntry.Attributes["onlyCreateIfNoChildren"].Value)
                        : false;

                    bool CreateIfExistsWithDifferentName = (xmlConfigEntry.Attributes["createIfExistsWithDifferentName"] != null)
                        ? bool.Parse(xmlConfigEntry.Attributes["createIfExistsWithDifferentName"].Value)
                        : true;

                    string DictionaryItemForName = (xmlConfigEntry.Attributes["dictionaryItemForName"] != null)
                      ? xmlConfigEntry.Attributes["dictionaryItemForName"].Value
                      : "";

                    bool KeepNewNodeUnpublished = (xmlConfigEntry.Attributes["keepNewNodeUnpublished"] != null)
                      ? bool.Parse(xmlConfigEntry.Attributes["keepNewNodeUnpublished"].Value)
                      : false;

                    var rule = new AutoNodeRule(CreatedDocTypeAlias, DocTypeAliasToCreate, NodeName, BringNewNodeFirst, OnlyCreateIfNoChildren, CreateIfExistsWithDifferentName, DictionaryItemForName, KeepNewNodeUnpublished);
                    retVal.Add(rule);
                }
            }
            logger.Info<AutoNode>(Resources.InfoLoadConfigComplete);
            return retVal;
        }
    }
}