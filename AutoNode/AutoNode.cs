using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

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
        /// Creates a new node under a given node, according to settings of the rule in effect
        /// </summary>
        /// <param name="node">The node to create a new node under</param>
        /// <param name="rule">The rule that will apply settings for the new node's creation</param>
        /// <param name="hasChildren">Indicates if the node has children</param>
        private void CreateNewNode(IContent node, AutoNodeRule rule, bool hasChildren)
        {

            //If rule says only if no children and there are children, abort process
            if (rule.OnlyCreateIfNoChildren && hasChildren) return;

            //If it exists already, abort process
            if
               (
                node.Children()
                .Where(x => 
                    x.ContentType.Alias.ToLower().Equals(rule.DocTypeAliasToCreate.ToLower()) && 
                    x.Name.ToLower().Equals(rule.NodeName.ToLower()))
                    .Any()
               ) return;

            var y = node.Children();
            foreach (var yy in y)
            {
                var dummy1 = yy.ContentType.Alias;
                var dummy2 = yy.ContentType.Name;
            }

            ///Get a content service reference
            IContentService cs = ApplicationContext.Current.Services.ContentService;

            try
            {
                ///Create and publish the new node
                IContent content = cs.CreateContent(rule.NodeName, node.Id, rule.DocTypeAliasToCreate);

                //Publish the new node
                cs.SaveAndPublishWithStatus(content, raiseEvents: false);
                
                ///Bring the new node first if rule dictates so
                if (rule.BringNewNodeFirst) { cs.Sort(BringLastNodeFirst(node)); }
            }
            catch (Exception ex)
            {
                Umbraco.Core.Logging.LogHelper.Error(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, "There was a problem with AutoNode new node creation. Please check that the doctype alias you have defined in rules actually exists", ex);
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

        #endregion
    }
}