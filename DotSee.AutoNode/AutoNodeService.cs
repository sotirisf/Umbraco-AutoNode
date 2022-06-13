using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Extensions;

namespace DotSee.AutoNode
{
    /// <summary>
    /// Creates new nodes under a newly created node, according to a set of rules
    /// </summary>
    public class AutoNodeService
    {
        #region Private Members

        /// <summary>
        /// The list of rule objects
        /// </summary>
        private List<Rule> _rules;

        /// <summary>
        /// Flag to indicate whether have been loaded by the rules provider
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

        /// <summary>
        /// Switch to prevent republishing of existing nodes
        /// </summary>
        private bool _republishExistingNodes;

        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;
        private readonly ILogger _logger;
        private readonly ISqlContext _sqlContext;
        private readonly AutoNodeUtils _autoNodeUtils;
        
        private readonly IRuleProviderService _ruleProviderService;

        #endregion Private Members

        #region Public Members
        public List<Rule> Rules
        {
            get => _rules; 
        }
        #endregion Public Members

        #region Constructors

        /// <summary>
        /// Private constructor for Singleton
        /// </summary>
        public AutoNodeService(
              IContentService contentService
            , IContentTypeService contentTypeService
            , ILogger logger
            , IRuleProviderService ruleProviderService
            , ISqlContext sqlContext
            , AutoNodeUtils autoNodeUtils)
        {
            _contentService = contentService;
            _rules = new List<Rule>();
            _logger = logger;
            _autoNodeUtils = autoNodeUtils;
            _contentTypeService = contentTypeService;
            _ruleProviderService = ruleProviderService;
            _sqlContext = sqlContext;
        }

        #endregion Constructors

        #region Public Methods

        /// <summary>
        /// Registers a new rule object
        /// </summary>
        /// <param name="rule">The rule object</param>
        public void RegisterRule(Rule rule)
        {
            _rules.Add(rule);
        }

        /// <summary>
        /// Removes all rules from the AutoNode instance
        /// </summary>
        public void ClearRules()
        {
            _rules.RemoveAll<Rule>(x => true);
            _rulesLoaded = false;
        }

        /// <summary>
        /// Applies all rules on creation of a node.
        /// </summary>
        /// <param name="node">The newly created node we need to apply rules for</param>
        public virtual bool Run(IContent node, string culture = "")
        {

            if (_rules != null && _rules.Count() > 0)
            {
                _rulesLoaded = true;
            }

            if (!_rulesLoaded && _ruleProviderService != null)
            {
                foreach (Rule r in (_ruleProviderService.Rules))
                {
                    _rules.Add(r);
                }
                _rulesLoaded = true;
            }

            if (_rules == null || _rules.Count() == 0)
            {
                return false;
            }

            _settings = _ruleProviderService.Settings;
            _logVerbose = (_settings != null && _settings["logLevel"] != null && _settings["logLevel"] == "Verbose");
            _republishExistingNodes = (_settings != null && _settings["republishExistingNodes"] != null && _settings["republishExistingNodes"] == "true");
            string createdDocTypeAlias = node.ContentType.Alias;

            bool hasChildren = _contentService.GetPagedChildren(node.Id, 0, 1, out long totalRecords).Any();

            var result = true;

            foreach (Rule rule in _rules)
            {
                if (rule.CreatedDocTypeAlias.InvariantEquals(createdDocTypeAlias))
                {
                    var partialResult = CreateOrPublishNode(node, rule, hasChildren, culture);
                    result = result == false ? false : partialResult; 
                }
            }
            return result;

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
        private bool CreateOrPublishNode(IContent node, Rule rule, bool hasChildren, string culture = "")
        {
            string message = string.Format(MessageConstants.InfoTryCreateNode, rule.DocTypeAliasToCreate, node.Id.ToString(), node.ContentType.Alias.ToString());
            LogVerboseInfo(message);

            //If rule says only if no children and there are children, abort process
            if ((bool)rule.OnlyCreateIfNoChildren && hasChildren)
            {
                LogVerboseInfo(MessageConstants.InfoAbortCreateNodeRuleRestrictions);
                return false;
            }

            return CreateNewNodeCultureAware(node, rule, culture);
        }

        /// <summary>
        /// Publishes an existing child node
        /// </summary>
        /// <param name="node">The parent node</param>
        /// <param name="existingNode">The node to be published</param>
        /// <param name="culture">The culture name, or empty string for non-variants</param>
        /// <param name="assignedNodeName">The name to be given to the new node according to rule settings</param>
        private bool PublishExistingChildNode(IContent node, IContent existingNode, string culture = "", string assignedNodeName = "")
        {
            if (existingNode == null) { return false; }

            //If the parent is NOT published, abort process.
            if (!node.Published)
            {
                LogVerboseInfo(MessageConstants.InfoAbortCreateNodeNodeExists);
                return false;
            }

            LogVerboseInfo(MessageConstants.InfoRepublishingExistingNode);

            if (!string.IsNullOrEmpty(culture) && !existingNode.AvailableCultures.Any(x => x.InvariantEquals(culture)))
            {
                ContentCultureInfos cinfo = new ContentCultureInfos(culture);
                cinfo.Name = string.IsNullOrEmpty(assignedNodeName) ? node.CultureInfos.Values.Where(x => !string.IsNullOrEmpty(x.Name)).FirstOrDefault().Name : assignedNodeName;
                existingNode.CultureInfos.Add(cinfo);
            }

            //Republish the node if there are no pending changes
            if (!existingNode.Edited)
            {
                var result = _contentService.SaveAndPublish(existingNode, (string.IsNullOrEmpty(culture) ? "*" : culture));
                if (!result.Success)
                {
                    _logger.Error(String.Format(MessageConstants.ErrorRepublishNoSuccess, existingNode.Name, node.Name));
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Creates a new node.
        /// </summary>
        /// <param name="node">The parent node.</param>
        /// <param name="rule">The rule being processed</param>
        /// <param name="culture">The culture name, or empty string for non-variants</param>
        private bool CreateNewNodeCultureAware(IContent node, Rule rule, string culture)
        {
            if (_contentTypeService.Get(rule.DocTypeAliasToCreate) == null)
            {
                _logger.Error(string.Format(MessageConstants.ErrorNodeAliasNotFound, rule.DocTypeAliasToCreate));
                return false;
            }

            //Get the node name that is supposed to be given to the new node.
            string assignedNodeName = _autoNodeUtils.GetAssignedNodeName(rule, culture);

            //Get the first existing node of the type and name defined by the rule
            IContent existingNode = GetExistingChildNode(node, rule, assignedNodeName);

            IContent content = null;

            try
            {
                //If the node exists already, decide whether to republish or skip depending on settings
                if (existingNode != null)
                {
                    if (_republishExistingNodes)
                    {
                        content = node;
                        PublishExistingChildNode(content, existingNode, culture, assignedNodeName);
                    }
                    else
                    {
                        //Stop here, we don't want any other action like sort or logging to take place. 
                        LogVerboseInfo(String.Format(MessageConstants.InfoNotRepublishingExistingNode, existingNode.Name));
                        return true;
                    }
                }
                else
                {
                    //If it doesn't exist, then create it and publish it.
                    IContent bp = GetBlueprint(rule);

                    if (bp != null)
                    {
                        content = _contentService.CreateContentFromBlueprint(bp, assignedNodeName);
                        content.SetParent(node);
                    }
                    else
                    {
                        content = _contentService.Create(assignedNodeName, node.Key, rule.DocTypeAliasToCreate);
                        if (!string.IsNullOrEmpty(culture))
                        {
                            ContentCultureInfos cinfo = new ContentCultureInfos(culture);
                            cinfo.Name = assignedNodeName;
                            content.CultureInfos.Add(cinfo);
                        }
                    }

                    bool success = false;

                    //Keep new node unpublished only for non-variants. Variants come up with strange errors here!
                    if ((bool)rule.KeepNewNodeUnpublished && string.IsNullOrEmpty(culture))
                    {
                        var result = _contentService.Save(content);
                        success = result.Success;
                    }
                    else
                    {
                        //Publish the new node
                        var result = (string.IsNullOrEmpty(culture))
                            ? _contentService.SaveAndPublish(content, culture: null)
                            : _contentService.SaveAndPublish(content, culture: culture);

                        success = result.Success;
                    }

                    if (!success)
                    {
                        _logger.Error(String.Format(MessageConstants.ErrorCreateNode, assignedNodeName, node.Name));
                        return false;
                    }
                }

                // Bring the new node first if rule dictates so
                if ((bool)rule.BringNewNodeFirst)
                {
                    BringNodeFirst(node, existingNode);
                }

                LogVerboseInfo(String.Format(MessageConstants.InfoCreateNodeSuccess, assignedNodeName, node.Name));
                
            }
            catch (Exception ex)
            {
                _logger.Error(ex, MessageConstants.ErrorGeneric);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Sorts nodes by either bringing the last node first or bringing a specific node first
        /// </summary>
        /// <param name="parentNode">The parent node</param>
        /// <param name="existingNode">The node to bring first, if left null it will bring the last node first</param>
        private void BringNodeFirst(IContent parentNode, IContent existingNode)
        {
            LogVerboseInfo(MessageConstants.InfoSortingNodes);

            IEnumerable<IContent> sortedNodes = Enumerable.Empty<IContent>();
            if (existingNode == null)
            {
                sortedNodes = BringLastNodeFirst(parentNode);
            }
            else
            {
                sortedNodes = BringExistingNodeFirst(parentNode, existingNode);
            }

            //Only sort when more than 1
            if (sortedNodes != Enumerable.Empty<IContent>())
            {
                var result = _contentService.Sort(sortedNodes.Select(x => x.Id));
                if (!result.Success)
                {
                    _logger.Error(MessageConstants.ErrorSortFailed);
                }
            }
        }

        /// <summary>
        /// Logs an informational message only if the "log verbose" setting is enabled
        /// </summary>
        /// <param name="messageTemplate"></param>
        private void LogVerboseInfo(string messageTemplate)
        {
            if (_logVerbose)
            {
                _logger.Information(messageTemplate);
            }
        }

        /// <summary>
        /// Gets a blueprint specified on a rule.
        /// </summary>
        /// <param name="rule">The rule in which the blueprint is specified</param>
        /// <returns>Null if the blueprint is not found</returns>
        private IContent GetBlueprint(Rule rule)
        {
            if (string.IsNullOrEmpty(rule.Blueprint)) { return null; }
            var contentTypeId = _contentTypeService.GetAllContentTypeIds(new string[] { rule.DocTypeAliasToCreate }).FirstOrDefault();
            if (contentTypeId <= 0) { return null; }
            var bps = _contentService.GetBlueprintsForContentTypes(contentTypeId);
            if (bps == null || bps.Count() == 0) { return null; }
            var bp = bps.Where(x => x.Name == rule.Blueprint).FirstOrDefault();
            return bp;
        }

        /// <summary>
        /// Gets an existing child node
        /// </summary>
        /// <param name="parentNode">The parent node</param>
        /// <param name="rule">The rule being processed</param>
        /// <param name="assignedNodeName">The name the rule dictates for a new node.
        /// This will be used when checking whether to create a new node or not,
        /// depending on whether the rule's setting "createIfExistsWithDifferentName" is set to true</param>
        /// <returns>Null if there is no existing node fulfilling the critera or the node if it exists.</returns>
        private IContent GetExistingChildNode(IContent parentNode, Rule rule, string assignedNodeName = "")
        {
            //TODO: trycatch and exit if not found
            int typeIdToCreate = _contentTypeService.Get(rule.DocTypeAliasToCreate).Id;

            long totalRecords;
            var query = new Query<IContent>(_sqlContext);

            //Find if an existing node is already there.
            //If we find an existing node a new one will NOT be created.
            //An existing node can be, depending on configuration, a node of the same type OR a node of the same type with the same name.
            IContent existingNode = null;

            if (_contentService.HasChildren(parentNode.Id))
            {
                var dontCreateIfExistsWithDifferentName = !(bool)rule.CreateIfExistsWithDifferentName;

                existingNode = _contentService.GetPagedChildren(parentNode.Id, 0, 1, out totalRecords
                    , filter: query.Where
                    (
                        x => 
                        x.ContentTypeId == typeIdToCreate
                        && (x.Name.Equals(assignedNodeName, StringComparison.CurrentCultureIgnoreCase) || dontCreateIfExistsWithDifferentName)
                    )
                      ).FirstOrDefault();
            }

            return (existingNode);
        }

        /// <summary>
        /// Sort nodes so the node passed to the function is first
        /// </summary>
        /// <param name="parentNode">The parent node</param>
        /// <param name="existingNode">The node to bring first</param>
        /// <returns></returns>
        private IEnumerable<IContent> BringExistingNodeFirst(IContent parentNode, IContent existingNode)
        {
            int cnt = _contentService.CountChildren(parentNode.Id);
            if (cnt <= 1) { return Enumerable.Empty<IContent>(); }
            return BringExistingNodeFirstDo(parentNode, existingNode, cnt);
        }

        /// <summary>
        /// Brings an existing node first in order
        /// </summary>
        /// <param name="parentNode">The parent node</param>
        /// <param name="existingNode">The node to bring first</param>
        /// <param name="cnt">Count of nodes to sort (for paging)</param>
        /// <returns></returns>
        private IEnumerable<IContent> BringExistingNodeFirstDo(IContent parentNode, IContent existingNode, int cnt)
        {
            long totalRecords;
            yield return existingNode;
            var restOfNodes = _contentService.GetPagedChildren(parentNode.Id, 0, cnt - 1, out totalRecords).Where(x => x.Id != existingNode.Id).OrderBy(x => x.SortOrder);
            foreach (IContent child in restOfNodes)
            {
                yield return child;
            }
        }

        /// <summary>
        /// Sorts nodes so that our newly inserted node gets to be first in physical order
        /// </summary>
        /// <param name="parentNode">The parent node</param>
        /// <returns>A list of nodes sorted in the desired way</returns>
        private IEnumerable<IContent> BringLastNodeFirst(IContent parentNode)
        {
            int cnt = _contentService.CountChildren(parentNode.Id);
            if (cnt <= 1) { return Enumerable.Empty<IContent>(); }

            return BringLastNodeFirstDo(parentNode, cnt);
        }

        /// <summary>
        /// Brings the last node first
        /// </summary>
        /// <param name="node">The parent node</param>
        /// <param name="cnt">The total number of nodes to be sorted</param>
        /// <returns>A list of nodes sorted in the desired way</returns>
        private IEnumerable<IContent> BringLastNodeFirstDo(IContent parentNode, int cnt)
        {
            long totalRecords;
            yield return _contentService.GetPagedChildren(parentNode.Id, 0, cnt, out totalRecords).OrderBy(x => x.SortOrder).Last();
            var restOfNodes = _contentService.GetPagedChildren(parentNode.Id, 0, cnt - 1, out totalRecords).OrderBy(x => x.SortOrder);
            foreach (IContent child in restOfNodes)
            {
                yield return child;
            }
        }

        #endregion Private Methods
    }
}