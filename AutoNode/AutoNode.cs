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
        /// The list of rule objects
        /// </summary>
        private List<AutoNodeRule> _rules;

        /// <summary>
        /// Flag to indicates where rules have been loaded by the rules provider
        /// </summary>
        private bool _rulesLoaded;

        /// <summary>
        /// Additional settings for autonode, presently only logLevel available
        /// </summary>
        private Dictionary<string, string> _settings;

        /// <summary>
        /// Switch to indicate verbose or default logging
        /// </summary>
        private bool _logVerbose;

        private readonly ILogger _logger;
        private readonly IContentService _cs;
        private readonly IContentTypeService _cts;
        private readonly IRuleProvider _rp;

        #endregion Private Members

        #region Constructors

        public AutoNode(ILogger logger, IContentService cs, IContentTypeService cts, IRuleProvider rp = null)
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
        /// <param name="culture">The culture name, or empty string for non-variants</param>
        public void Run(IContent node, string culture = "")
        {
            if (_rules != null && _rules.Count() > 0)
            {
                _rulesLoaded = true;
            }

            if (!_rulesLoaded && _rp != null)
            {
                foreach (AutoNodeRule r in (_rp.Rules))
                {
                    _rules.Add(r);
                }
                _rulesLoaded = true;
            }

            if (_rules == null || _rules.Count() == 0)
            {
                return;
            }

            _settings = _rp.Settings;
            _logVerbose = (_settings["logLevel"] != null && _settings["logLevel"] == "Verbose");

            string createdDocTypeAlias = node.ContentType.Alias;

            bool hasChildren = _cs.HasChildren(node.Id);

            foreach (AutoNodeRule rule in _rules)
            {
                if (rule.CreatedDocTypeAlias.InvariantEquals(createdDocTypeAlias))
                {
                    CreateOrPublishNode(node, rule, hasChildren, culture);
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
        /// <param name="culture">The culture name, or empty string for non-variants</param>
        private void CreateOrPublishNode(IContent node, AutoNodeRule rule, bool hasChildren, string culture = "")
        {
            if (_logVerbose)
            {
                _logger.Info<AutoNode>(Resources.InfoTryCreateNode, rule.DocTypeAliasToCreate, node.Id.ToString(), node.ContentType.Alias.ToString());
            }

            //If rule says only if no children and there are children, abort process
            if (rule.OnlyCreateIfNoChildren && hasChildren)
            {
                if (_logVerbose)
                {
                    _logger.Info<AutoNode>(Resources.InfoAbortCreateNodeRuleRestrictions);
                }
                return;
            }

            CreateNewNodeCultureAware(node, rule, culture);
        }

        /// <summary>
        /// Publishes an existing child node
        /// </summary>
        /// <param name="node">The parent node</param>
        /// <param name="existingNode">The node to be published</param>
        /// <param name="culture">The culture name, or empty string for non-variants</param>
        /// <param name="assignedNodeName">The name to be given to the new node according to rule settings</param>
        private void PublishExistingChildNode(IContent node, IContent existingNode, string culture = "", string assignedNodeName = "")
        {
            if (existingNode == null) { return; }

            //If the parent is NOT published, abort process.
            if (!node.Published)
            {
                if (_logVerbose)
                {
                    _logger.Info<AutoNode>(Resources.InfoAbortCreateNodeNodeExists);
                }
                return;
            }

            if (_logVerbose)
            {
                _logger.Info<AutoNode>(Resources.InfoRepublishingExistingNode);
            }

            if (!string.IsNullOrEmpty(culture) && !existingNode.AvailableCultures.Any(x => x.InvariantEquals(culture)))
            {
                ContentCultureInfos cinfo = new ContentCultureInfos(culture);
                cinfo.Name = string.IsNullOrEmpty(assignedNodeName) ? node.CultureInfos.Values.Where(x => !string.IsNullOrEmpty(x.Name)).FirstOrDefault().Name : assignedNodeName;
                existingNode.CultureInfos.Add(cinfo);
            }

            //Republish the node if there are no pending changes
            if (!existingNode.Edited)
            {
                var result = _cs.SaveAndPublish(existingNode, (string.IsNullOrEmpty(culture) ? "*": culture), raiseEvents: true);
                if (!result.Success)
                {
                    _logger.Error<AutoNode>(String.Format(Resources.ErrorRepublishNoSuccess, existingNode.Name, node.Name));
                }
                return;
            }
        }

        /// <summary>
        /// Creates a new node.
        /// </summary>
        /// <param name="node">The parent node.</param>
        /// <param name="rule">The rule being processed</param>
        /// <param name="culture">The culture name, or empty string for non-variants</param>
        private void CreateNewNodeCultureAware(IContent node, AutoNodeRule rule, string culture)
        {

            if (_cts.Get(rule.DocTypeAliasToCreate) == null)
            {
                _logger.Error<AutoNode>(string.Format(Resources.ErrorNodeAliasNotFound, rule.DocTypeAliasToCreate));
                return;
            }

            //Get the node name that is supposed to be given to the new node.
            string assignedNodeName = GetAssignedNodeName(node, rule, culture);

            //Get the first existing node of the type and name defined by the rule
            IContent existingNode = GetExistingChildNode(node, rule, assignedNodeName);

            IContent content = null;

            try
            {
                //If it exists already
                if (existingNode != null)
                {
                    content = node;
                    PublishExistingChildNode(content, existingNode, culture, assignedNodeName);
                }
                else
                {
                    //If it doesn't exist, then create it and publish it.
                    IContent bp = GetBlueprint(rule);

                    if (bp != null)
                    {
                        content = _cs.CreateContentFromBlueprint(bp, assignedNodeName);
                        content.SetParent(node);
                    }
                    else
                    {
                        content = _cs.Create(assignedNodeName, node.Key, rule.DocTypeAliasToCreate);
                        if (!string.IsNullOrEmpty(culture))
                        {
                            ContentCultureInfos cinfo = new ContentCultureInfos(culture);
                            cinfo.Name = assignedNodeName;
                            content.CultureInfos.Add(cinfo);
                        }
                    }

                    bool success = false;

                    //Keep new node unpublished only for non-variants. Variants come up with strange errors here!
                    if (rule.KeepNewNodeUnpublished && string.IsNullOrEmpty(culture))
                    {
                        var result = _cs.Save(content);
                        success = result.Success;
                    }
                    else
                    {
                        //Publish the new node
                        var result = (string.IsNullOrEmpty(culture))
                            ? _cs.SaveAndPublish(content, raiseEvents: true, culture: null)
                            : _cs.SaveAndPublish(content, raiseEvents: true, culture: culture);

                        success = result.Success;
                    }
                    if (!success)
                    {
                        _logger.Error<AutoNode>(String.Format(Resources.ErrorCreateNode, assignedNodeName, node.Name));
                        return;
                    }
                }

                // Bring the new node first if rule dictates so
                if (rule.BringNewNodeFirst)
                {
                    if (_logVerbose)
                    {
                        _logger.Info<AutoNode>(Resources.InfoSortingNodes);
                    }

                    IEnumerable<IContent> sortedNodes = Enumerable.Empty<IContent>();
                    if (existingNode == null)
                    {
                        sortedNodes = BringLastNodeFirst(node);
                    }
                    else
                    {
                        sortedNodes = BringExistingNodeFirst(node, existingNode);
                    }

                    //Only sort when more than 1
                    if (sortedNodes != Enumerable.Empty<IContent>())
                    {
                        var result = _cs.Sort(sortedNodes.Select(x => x.Id), raiseEvents: false);
                        if (!result.Success)
                        {
                            _logger.Error<AutoNode>(Resources.ErrorSortFailed);
                        }
                    }
                }

                if (_logVerbose)
                {
                    _logger.Info<AutoNode>(String.Format(Resources.InfoCreateNodeSuccess, assignedNodeName, node.Name));
                }
            }
            catch (Exception ex)
            {
                _logger.Error<AutoNode>(ex, Resources.ErrorGeneric);
                return;
            }
        }

        /// <summary>
        /// Gets a blueprint specified on a rule.
        /// </summary>
        /// <param name="rule">The rule in which the blueprint is specified</param>
        /// <returns>Null if the blueprint is not found</returns>
        private IContent GetBlueprint(AutoNodeRule rule)
        {
            if (string.IsNullOrEmpty(rule.Blueprint)) { return null; }
            var contentTypeId = _cts.GetAllContentTypeIds(new string[] { rule.DocTypeAliasToCreate }).FirstOrDefault();
            if (contentTypeId <= 0) { return null; }
            var bps = _cs.GetBlueprintsForContentTypes(contentTypeId);
            if (bps == null || bps.Count() == 0) { return null; }
            var bp = bps.Where(x => x.Name == rule.Blueprint).FirstOrDefault();
            return bp;
        }

        /// <summary>
        /// Gets an existing child node
        /// </summary>
        /// <param name="node">The parent node</param>
        /// <param name="rule">The rule being processed</param>
        /// <param name="assignedNodeName">The name the rule dictates for a new node.
        /// This will be used when checking whether to create a new node or not,
        /// depending on whether the rule's setting "createIfExistsWithDifferentName" is set to true</param>
        /// <returns>Null if there is no existing node fulfilling the critera or the node if it exists.</returns>
        private IContent GetExistingChildNode(IContent node, AutoNodeRule rule, string assignedNodeName = "")
        {
            //TODO: trycatch and exit if not found
            int typeIdToCreate = _cts.Get(rule.DocTypeAliasToCreate).Id;

            long totalRecords;
            var query = new Query<IContent>(Current.SqlContext);

            //Find if an existing node is already there.
            //If we find an existing node a new one will NOT be created.
            //An existing node can be, depending on configuration, a node of the same type OR a node of the same type with the same name.
            IContent existingNode = null;

            if (_cs.HasChildren(node.Id))
            {
                existingNode = _cs.GetPagedChildren(node.Id, 0, 1, out totalRecords
                    , filter: query.Where(
                        x => x.ContentTypeId == typeIdToCreate
                        && (x.Name.Equals(assignedNodeName, StringComparison.CurrentCultureIgnoreCase) || !rule.CreateIfExistsWithDifferentName)
                       )
                      ).FirstOrDefault();
            }

            return (existingNode);
        }


        private IEnumerable<IContent> BringExistingNodeFirst(IContent node, IContent existingNode)
        {
            int cnt = _cs.CountChildren(node.Id);
            if (cnt <= 1) { return Enumerable.Empty<IContent>(); }
            return BringExistingNodeFirstDo(node, existingNode, cnt);

        }

        private IEnumerable<IContent> BringExistingNodeFirstDo(IContent node, IContent existingNode, int cnt)
        {
            long totalRecords;
            yield return existingNode;
            var restOfNodes = _cs.GetPagedChildren(node.Id, 0, cnt - 1, out totalRecords).Where(x => x.Id != existingNode.Id).OrderBy(x => x.SortOrder);
            foreach (IContent child in restOfNodes)
            {
                yield return child;
            }
        }


        /// <summary>
        /// Sorts nodes so that our newly inserted node gets to be first in physical order
        /// </summary>
        /// <param name="node">The node to bring first</param>
        /// <returns>A list of nodes sorted in the desired way</returns>
        private IEnumerable<IContent> BringLastNodeFirst(IContent node)
        {
            int cnt = _cs.CountChildren(node.Id);
            if (cnt <= 1) { return Enumerable.Empty<IContent>(); }

            return BringLastNodeFirstDo(node, cnt);
        }

        /// <summary>
        /// Brings the last node first
        /// </summary>
        /// <param name="node">The node to be first</param>
        /// <param name="cnt">The total number of nodes to be sorted</param>
        /// <returns>A list of nodes sorted in the desired way</returns>
        private IEnumerable<IContent> BringLastNodeFirstDo(IContent node, int cnt)
        {
            long totalRecords;
            yield return _cs.GetPagedChildren(node.Id, 0, cnt, out totalRecords).OrderBy(x => x.SortOrder).Last();
            var restOfNodes = _cs.GetPagedChildren(node.Id, 0, cnt - 1, out totalRecords).OrderBy(x => x.SortOrder);
            foreach (IContent child in restOfNodes)
            {
                yield return child;
            }
        }

        /// <summary>
        /// Gets the predefined name for the newly created node. This can either be a dictionary entry for multilingual installations or a standard string
        /// </summary>
        /// <param name="node">The node under which the new node will be created</param>
        /// <param name="rule">The rule being processed</param>
        /// <param name="culture">The culture name, or empty string for non-variants</param>
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
                        assignedNodeName = lsvc.GetDictionaryItemByKey(rule.DictionaryItemForName).Translations.First(t => t.Language.CultureInfo.Name.InvariantEquals(culture)).Value;
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