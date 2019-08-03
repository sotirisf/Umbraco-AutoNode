using DotSee.AutoNode.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Services;

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
        //private static readonly Lazy<AutoNode> _instance = new Lazy<AutoNode>(() => new AutoNode());

        /// <summary>
        /// The list of rule objects
        /// </summary>
        private List<AutoNodeRule> _rules;

        private bool _rulesLoaded;

        private readonly ILogger _logger;
        private readonly IContentService _cs;
        private readonly IContentTypeService _cts;
        private readonly IRuleProvider _rp;

        #endregion Private Members

        #region Constructors

        /// <summary>
        /// Returns a (singleton) AutoNode instance
        /// </summary>
        //public static AutoNode Instance { get { return _instance.Value; } }

        /// <summary>
        /// Private constructor for Singleton
        /// </summary>
        public AutoNode(ILogger logger, IContentService cs, IContentTypeService cts, IRuleProvider rp)
        {
            _rules = new List<AutoNodeRule>();
            _rulesLoaded = false;
            _logger = logger;
            _cs = cs;
            _cts = cts;
            _rp = rp;
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
        public void Run(IContent node)
        {
            if (!_rulesLoaded)
            {
                foreach (AutoNodeRule r in (_rp.GetRules(_logger)))
                {
                    _rules.Add(r);
                }
                _rulesLoaded = true;
            }

            string createdDocTypeAlias = node.ContentType.Alias;

            bool hasChildren = _cs.HasChildren(node.Id);

            foreach (AutoNodeRule rule in _rules)
            {
                if (rule.CreatedDocTypeAlias.InvariantEquals(createdDocTypeAlias))
                {
                    CreateNewNode(node, rule, hasChildren);
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
        private void CreateNewNode(IContent node, AutoNodeRule rule, bool hasChildren)
        {
            _logger.Info<AutoNode>(Resources.InfoTryCreateNode, rule.DocTypeAliasToCreate, node.Id.ToString(), node.ContentType.Alias.ToString());

            //If rule says only if no children and there are children, abort process
            if (rule.OnlyCreateIfNoChildren && hasChildren)
            {
                _logger.Info<AutoNode>(Resources.InfoAbortCreateNodeRuleRestrictions);

                return;
            }

            if (node.AvailableCultures.Count() == 0)
            {
                CreateNewNodeCultureAware(node, rule, "");
            }
            else
            {
                foreach (string culture in node.AvailableCultures)
                {
                    CreateNewNodeCultureAware(node, rule, culture);
                }
            }
        }

        private void CreateNewNodeCultureAware(IContent node, AutoNodeRule rule, string culture)
        {
            //TODO: trycatch and exit if not found
            int typeIdToCreate = _cts.Get(rule.DocTypeAliasToCreate).Id;

            //Get the node name that is supposed to be given to the new node.
            string assignedNodeName = GetAssignedNodeName(node, rule, culture);

            long totalRecords;
            var query = new Query<IContent>(Current.SqlContext);

            //Find if an existing node is already there.
            //If we find an existing node a new one will NOT be created.
            //An existing node can be, depending on configuration, a node of the same type OR a node of the same type with the same name.
            IContent existingNode = null;
            if (_cs.HasChildren(node.Id))
            {
                IEnumerable<IContent> children = _cs.GetPagedChildren(node.Id, 0, 1, out totalRecords
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
                    _logger.Info<AutoNode>(Resources.InfoAbortCreateNodeNodeExists);
                    return;
                }

                //If it exists already but is not published and parent is published, republish
                if (!existingNode.Published && node.Published)
                {
                    _logger.Info<AutoNode>(Resources.InfoRepublishingExistingNode);

                    //Republish the node
                    var result = _cs.SaveAndPublish(existingNode, raiseEvents: true);
                    if (!result.Success)
                    {
                        _logger.Error<AutoNode>(String.Format(Resources.ErrorRepublishNoSuccess, existingNode.Name, node.Name));
                    }
                    return;
                }
            }

            //If it doesn't exist, then create it and publish it.
            try
            {
                ///Create and publish the new node
                //IContent content = cs.CreateContent(rule.NodeName, node.Id, rule.DocTypeAliasToCreate);
                IContent content = _cs.Create(assignedNodeName, node.GetUdi().Guid, rule.DocTypeAliasToCreate);

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
                        ? _cs.SaveAndPublish(content, raiseEvents: false, culture: null)
                        : _cs.SaveAndPublish(content, raiseEvents: false, culture: culture);

                    success = result.Success;
                }
                else
                {
                    var result = _cs.Save(content, raiseEvents: false);
                    success = result.Success;
                }

                if (!success)
                {
                    _logger.Error<AutoNode>(String.Format(Resources.ErrorCreateNode, assignedNodeName, node.Name));
                    return;
                }
                // Bring the new node first if rule dictates so
                if (rule.BringNewNodeFirst)
                {
                    _logger.Info<AutoNode>(Resources.InfoSortingNodes);
                    _cs.Sort(BringLastNodeFirst(node));
                }

                _logger.Info<AutoNode>(String.Format(Resources.InfoCreateNodeSuccess, assignedNodeName, node.Name));
            }
            catch (Exception ex)
            {
                _logger.Error<AutoNode>(ex, Resources.ErrorGeneric);
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
            int cnt = _cs.CountChildren(node.Id);
            if (cnt == 0) { yield break; }
            long totalRecords;
            yield return _cs.GetPagedChildren(node.Id, 0, cnt, out totalRecords).Last();

            foreach (IContent child in _cs.GetPagedChildren(node.Id, 0, cnt - 1, out totalRecords))
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
                    if (!string.IsNullOrEmpty(culture))
                    {
                        assignedNodeName = lsvc.GetDictionaryItemByKey(rule.DictionaryItemForName).Translations.First(t => t.Language.CultureInfo.Name == culture).Value;
                    }
                    else
                    {
                        assignedNodeName = lsvc.GetDictionaryItemByKey(rule.DictionaryItemForName).Translations.First().Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error<AutoNode>(ex, Resources.ErrorDictionaryKeyNotFound);
                }
            }

            //If no dictionary key has been found, fallback to the standard name setting
            if (string.IsNullOrEmpty(assignedNodeName)) { assignedNodeName = rule.NodeName; }

            return (assignedNodeName);
        }

        #endregion Private Methods
    }
}