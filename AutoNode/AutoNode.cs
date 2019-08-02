using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using System.Xml;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Services;
using DotSee.AutoNode.Properties;

namespace DotSee.AutoNode
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
        private static readonly Lazy<AutoNode> _instance = new Lazy<AutoNode>(() => new AutoNode());

        /// <summary>
        /// The list of rule objects
        /// </summary>
        private List<AutoNodeRule> _rules;

        private bool _rulesLoaded;

        #endregion Private Members

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
            _rulesLoaded = false;

        }

        #endregion Constructors

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
        /// Removes all rules from the AutoNode instance
        /// </summary>
        public void ClearRules()
        {
            _rules.RemoveAll<AutoNodeRule>(x => true);
            _rulesLoaded = false;
        }

        /// <summary>
        /// Applies all rules on creation of a node.
        /// </summary>
        /// <param name="node">The newly created node we need to apply rules for</param>
        public void Run(IContent node, ILogger logger, IContentService cs, IContentTypeService cts)
        {

            if (!_rulesLoaded)
            {
                foreach (AutoNodeRule r in (new ConfigFileRuleProvider().GetRules(logger)))
                {
                    _rules.Add(r);
                }
                _rulesLoaded = true;
            }

            string createdDocTypeAlias = node.ContentType.Alias;

            bool hasChildren = cs.HasChildren(node.Id);

            foreach (AutoNodeRule rule in _rules)
            {
                if (rule.CreatedDocTypeAlias.Equals(createdDocTypeAlias))
                {
                    CreateNewNode(node, rule, hasChildren, logger, cs, cts);
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

  
        /// <summary>
        /// Creates a new node under a given node, according to settings of the rule in effect
        /// </summary>
        /// <param name="node">The node to create a new node under</param>
        /// <param name="rule">The rule that will apply settings for the new node's creation</param>
        /// <param name="hasChildren">Indicates if the node has children</param>
        private void CreateNewNode(IContent node, AutoNodeRule rule, bool hasChildren, ILogger logger, IContentService cs, IContentTypeService cts)
        {
           

            logger.Info<AutoNode>(Resources.InfoTryCreateNode, rule.DocTypeAliasToCreate, node.Id.ToString(), node.ContentType.Alias.ToString());

            //If rule says only if no children and there are children, abort process
            if (rule.OnlyCreateIfNoChildren && hasChildren)
            {
                logger.Info<AutoNode>(Resources.InfoAbortCreateNodeRuleRestrictions);

                return;
            }

            if (node.AvailableCultures.Count() == 0)
            {
                CreateNewNodeCultureAware(node, rule, "", logger, cs, cts);
            }
            else
            {
                foreach (string culture in node.AvailableCultures)
                {
                    CreateNewNodeCultureAware(node, rule, culture, logger, cs, cts);
                }
            }
          
        }

        private void CreateNewNodeCultureAware(IContent node, AutoNodeRule rule, string culture, ILogger logger, IContentService cs, IContentTypeService cts)
        {
            //TODO: trycatch and exit if not found
            int typeIdToCreate = cts.Get(rule.DocTypeAliasToCreate).Id;

            //Get the node name that is supposed to be given to the new node.
            string assignedNodeName = GetAssignedNodeName(node, rule, culture);

            long totalRecords;
            var query = new Query<IContent>(Current.SqlContext);

            //Find if an existing node is already there.
            //If we find an existing node a new one will NOT be created.
            //An existing node can be, depending on configuration, a node of the same type OR a node of the same type with the same name.
            IContent existingNode = null;
            if (cs.HasChildren(node.Id))
            {
                IEnumerable<IContent> children = cs.GetPagedChildren(node.Id, 0, 1, out totalRecords
                    , filter: query.Where(
                        x => x.ContentTypeId == typeIdToCreate
                        && (x.Name.Equals(assignedNodeName, StringComparison.CurrentCultureIgnoreCase) || !rule.CreateIfExistsWithDifferentName)
                       )
                      );
                existingNode = children.FirstOrDefault();
            }

            //If it exists already
            if (existingNode != null)
            {
                //If it is already published or if the parent is NOT published, abort process.
                if (existingNode.Published || !node.Published)
                {
                    logger.Info<AutoNode>(Resources.InfoAbortCreateNodeNodeExists);
                    return;
                }

                //If it exists already but is not published and parent is published, republish
                if (!existingNode.Published && node.Published)
                {
                    logger.Info<AutoNode>(Resources.InfoRepublishingExistingNode);

                    //Republish the node
                    var result = cs.SaveAndPublish(existingNode, raiseEvents: true);
                    if (!result.Success)
                    {
                        logger.Error<AutoNode>(String.Format(Resources.ErrorRepublishNoSuccess, existingNode.Name, node.Name));
                    }
                    return;
                }
            }

            //If it doesn't exist, then create it and publish it.
            try
            {
                ///Create and publish the new node
                //IContent content = cs.CreateContent(rule.NodeName, node.Id, rule.DocTypeAliasToCreate);
                IContent content = cs.Create(assignedNodeName, node.GetUdi().Guid, rule.DocTypeAliasToCreate);

                if (!string.IsNullOrEmpty(culture))
                {
                    ContentCultureInfos cinfo = new ContentCultureInfos(culture);
                    cinfo.Name = assignedNodeName;
                    content.CultureInfos.Add(cinfo);
                }

                bool success = false;

                if (!rule.KeepNewNodeUnpublished)
                {
                    //Publish the new node
                    var result = (string.IsNullOrEmpty(culture))
                        ? cs.SaveAndPublish(content, raiseEvents: false, culture: null)
                        : cs.SaveAndPublish(content, raiseEvents: false, culture: culture);

                    success = result.Success;
                }
                else
                {
                    var result = cs.Save(content, raiseEvents: false);
                    success = result.Success;
                }

                if (!success)
                {
                    logger.Error<AutoNode>(String.Format(Resources.ErrorCreateNode, assignedNodeName, node.Name));
                    return;
                }
                // Bring the new node first if rule dictates so
                if (rule.BringNewNodeFirst)
                {
                    logger.Info<AutoNode>(Resources.InfoSortingNodes);
                    cs.Sort(BringLastNodeFirst(node));
                }

                logger.Info<AutoNode>(String.Format(Resources.InfoCreateNodeSuccess, assignedNodeName, node.Name));
            }
            catch (Exception ex)
            {
                logger.Error<AutoNode>(ex, Resources.ErrorGeneric);
                return;
            }
        }

        /// <summary>
        /// Sorts nodes so that our newly inserted node gets to be first in physical order
        /// </summary>
        /// <param name="node">The node to bring first</param>
        /// <returns></returns>
        private IEnumerable<IContent> BringLastNodeFirst(IContent node)
        {
            IContentService cs = Current.Services.ContentService;
            int cnt = cs.CountChildren(node.Id);
            if (cnt == 0) { yield break; }
            long totalRecords;
            yield return cs.GetPagedChildren(node.Id, 0, cnt, out totalRecords).Last();

            foreach (IContent child in cs.GetPagedChildren(node.Id, 0, cnt-1, out totalRecords))
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
        private string GetAssignedNodeName(IContent node, AutoNodeRule rule, string culture)
        {
            string assignedNodeName = null;

            //Get the dictionary item if a dictionary key has been specified in config
            if (rule.DictionaryItemForName != "")
            {
                try
                {
                    var lsvc = Current.Services.LocalizationService;
                    
                    assignedNodeName = lsvc.GetDictionaryItemByKey(rule.DictionaryItemForName).Translations.First(t => t.Language.CultureInfo.Name == culture).Value;
                }
                catch (Exception ex)
                {
                    Current.Logger.Error<AutoNode>(ex, Resources.ErrorDictionaryKeyNotFound);
                }
            }

            //If no dictionary key has been found, fallback to the standard name setting
            if (string.IsNullOrEmpty(assignedNodeName)) { assignedNodeName = rule.NodeName; }

            return (assignedNodeName);
        }

        #endregion Private Methods
    }
}