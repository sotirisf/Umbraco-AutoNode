using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using System.Xml;
using umbraco;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web;

namespace DotSee
{
    /// <summary>
    /// Creates new nodes under a newly created node, according to a set of rules
    /// </summary>
    public sealed class AutoNode 
    {

        #region Private Members
        /// <summary>
        /// Lazy singleton instance member
        /// </summary>
        private static readonly Lazy<AutoNode> _instance = new Lazy<AutoNode>(()=>new AutoNode());

        /// <summary>
        /// The list of rule objects
        /// </summary>
        private List<AutoNodeRule> _rules;

        #endregion

        #region Constructors

        /// <summary>
        /// Returns a (singleton) AutoNode instance
        /// </summary>
        public static AutoNode Instance { get { return _instance.Value; } }


        /// <summary>
        /// Private constructor for Singleton
        /// </summary>
        private AutoNode()
        {
            _rules = new List<AutoNodeRule>();

            ///Get rules from the config file. Any rules programmatically declared later on will be added too.
            GetRulesFromConfigFile();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers a new rule object 
        /// </summary>
        /// <param name="rule">The rule object</param>
        public void RegisterRule(AutoNodeRule rule)
        {
            _rules.Add(rule);
        }

        /// <summary>
        /// Applies all rules on creation of a node. 
        /// </summary>
        /// <param name="node">The newly created node we need to apply rules for</param>
        public void Run(IContent node)
        {
            string createdDocTypeAlias = node.ContentType.Alias;

            bool hasChildren = node.Children().Any();
            
            foreach (AutoNodeRule rule in _rules)
            {
                if (rule.CreatedDocTypeAlias.Equals(createdDocTypeAlias))
                {
                    CreateNewNode(node, rule, hasChildren);                    
                }

            }            
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets rules from /config/autoNode.config file (if it exists)
        /// </summary>
        private void GetRulesFromConfigFile()
        {
            XmlDocument xmlConfig = new XmlDocument();

            try
            {
                xmlConfig.Load(HostingEnvironment.MapPath(GlobalSettings.Path + "/../config/autoNode.config"));
            }
            catch (FileNotFoundException ex) {
                LogHelper.Error(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: Configuration file was not found.", ex);
                return;
            }
            catch (Exception ex)
            {
                LogHelper.Error(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: There was a problem loading AutoNode configuration from the config file", ex);
                return;
            }
            LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: Loading configuration...");
            
            foreach (XmlNode xmlConfigEntry in xmlConfig.SelectNodes("/autoNode/rule"))
            {
                if (xmlConfigEntry.NodeType == XmlNodeType.Element)
                {
                    string CreatedDocTypeAlias = xmlConfigEntry.Attributes["createdDocTypeAlias"].Value;
                    string DocTypeAliasToCreate = xmlConfigEntry.Attributes["docTypeAliasToCreate"].Value;
                    string NodeName = xmlConfigEntry.Attributes["nodeName"].Value;

                    bool BringNewNodeFirst = (xmlConfigEntry.Attributes["bringNewNodeFirst"]!=null) 
                        ? bool.Parse(xmlConfigEntry.Attributes["bringNewNodeFirst"].Value)
                        : false;

                    bool OnlyCreateIfNoChildren = (xmlConfigEntry.Attributes["onlyCreateIfNoChildren"]!=null) 
                        ? bool.Parse(xmlConfigEntry.Attributes["onlyCreateIfNoChildren"].Value) 
                        : false;

                    bool CreateIfExistsWithDifferentName = (xmlConfigEntry.Attributes["createIfExistsWithDifferentName"]!=null) 
                        ? bool.Parse(xmlConfigEntry.Attributes["createIfExistsWithDifferentName"].Value)
                        : true;

                    string DictionaryItemForName = (xmlConfigEntry.Attributes["dictionaryItemForName"] != null)
                      ? xmlConfigEntry.Attributes["dictionaryItemForName"].Value
                      : "";

                    var rule = new AutoNodeRule(CreatedDocTypeAlias, DocTypeAliasToCreate, NodeName, BringNewNodeFirst, OnlyCreateIfNoChildren, CreateIfExistsWithDifferentName, DictionaryItemForName);
                    _rules.Add(rule);

                }
            }
            LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: Loading configuration complete");
        }


        /// <summary>
        /// Creates a new node under a given node, according to settings of the rule in effect
        /// </summary>
        /// <param name="node">The node to create a new node under</param>
        /// <param name="rule">The rule that will apply settings for the new node's creation</param>
        /// <param name="hasChildren">Indicates if the node has children</param>
        private void CreateNewNode(IContent node, AutoNodeRule rule, bool hasChildren)
        {
            //Get the node name that is supposed to be given to the new node.
            string assignedNodeName = GetAssignedNodeName(node, rule);

            LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, string.Format("AutoNode: Trying to automatically create node of type {2} for node {0} of type {1}...", node.Id.ToString(), node.ContentType.Alias.ToString(), rule.DocTypeAliasToCreate));

            //If rule says only if no children and there are children, abort process
            if (rule.OnlyCreateIfNoChildren && hasChildren)
            {
                LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: Aborting node creation due to rule restrictions. Parent node already has children, rule indicates that parent node should not have children");
                return;
            }

            //Find if an existing node is already there.
            //If we find an existing node a new one will NOT be created.
            //An existing node can be, depending on configuration, a node of the same type OR a node of the same type with the same name.
            var existingNode = node.Children()
            .Where(x =>
                x.ContentType.Alias.ToLower().Equals(rule.DocTypeAliasToCreate.ToLower()))  //Same doctype
             .Where(y=>
                (rule.CreateIfExistsWithDifferentName)
                 ? y.Name.ToLower().Equals(assignedNodeName.ToLower()) //Same name. If found and createIfExistsWithDifferentName is true then a node will NOT be created.
                 : true                                             //Broader, it is enough to find a node with the same doctype.
                ).FirstOrDefault();

            ///Get a content service reference
            IContentService cs = ApplicationContext.Current.Services.ContentService;

            //If it exists already
            if (existingNode != null)
            {
                //If it is already published or if the parent is NOT published, abort process.
                if (existingNode.Published || !node.Published)
                {
                    LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: Aborting node creation since node already exists");
                    return;
                }

                //If it exists already but is not published and parent is published, republish
                if (!existingNode.Published && node.Published)
                {
                    LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: Republishing already existing child node...");

                    //Republish the node
                    cs.SaveAndPublishWithStatus(existingNode, raiseEvents: true);
                    return;
                }
            }

            //If it doesn't exist, then create it and publish it.
            try
            {
                ///Create and publish the new node
                //IContent content = cs.CreateContent(rule.NodeName, node.Id, rule.DocTypeAliasToCreate);
                IContent content = cs.CreateContent(assignedNodeName, node.Id, rule.DocTypeAliasToCreate);

                //Publish the new node
                cs.SaveAndPublishWithStatus(content, raiseEvents: false);                   
                
                ///Bring the new node first if rule dictates so
                if (rule.BringNewNodeFirst)
                {
                    LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: Bringing newly created node first...");
                    cs.Sort(BringLastNodeFirst(node));
                }

                LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: Node created succesfully.");
            }
            catch (Exception ex)
            {
                LogHelper.Error(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: There was a problem with new node creation. Please check that the doctype alias you have defined in rules actually exists", ex);
            }
           
        }

        /// <summary>
        /// Sorts nodes so that our newly inserted node gets to be first in physical order
        /// </summary>
        /// <param name="node">The node to bring first</param>
        /// <returns></returns>
        private IEnumerable<IContent> BringLastNodeFirst(IContent node)
        {
            int cnt = node.Children().Count();
            if (cnt == 0) { yield break; }

            yield return node.Children().Last();

            foreach (IContent child in node.Children().Take(cnt - 1))
            {

                yield return child;
            }
        }

        /// <summary>
        /// Gets the predefined name for the newly created node. This can either be a dictionary entry for multilingual installations or a standard string
        /// </summary>
        /// <param name="node">The node under which the new node will be created</param>
        /// <param name="rule">The rule being processed</param>
        /// <returns></returns>
        private string GetAssignedNodeName(IContent node, AutoNodeRule rule)
        {
            string assignedNodeName = null;

            //Get the dictionary item if a dictionary key has been specified in config
            if (rule.DictionaryItemForName != "")
            {
                try
                {
                    var lsvc = ApplicationContext.Current.Services.LocalizationService;
                    var lang = new UmbracoHelper(UmbracoContext.Current).TypedContent(node.Id).GetCulture().Name;
                    assignedNodeName = lsvc.GetDictionaryItemByKey(rule.DictionaryItemForName).Translations.First(t => t.Language.CultureInfo.Name == lang).Value;
                }
                catch (Exception ex)
                {
                    LogHelper.Info(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "AutoNode: The dictionary key specified in autoNode.config was not found.");
                }
            }
            
            //If no dictionary key has been found, fallback to the standard name setting
            if (string.IsNullOrEmpty(assignedNodeName)) { assignedNodeName = rule.NodeName; }

            return (assignedNodeName);
        }

        #endregion
    }
}