using Serilog;
using System;
using System.Linq;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace DotSee.AutoNode
{
    public class AutoNodeUtils
    {
        private readonly ILogger _logger;
        private readonly ILocalizationService _localizationService;

        public AutoNodeUtils(ILogger logger, ILocalizationService localizationService)
        {
            _logger = logger;
            _localizationService = localizationService;
        }

        /// <summary>
        /// Gets the predefined name for the newly created node. This can either be a dictionary entry for multilingual installations or a standard string
        /// </summary>
        /// <param name="node">The node under which the new node will be created</param>
        /// <param name="rule">The rule being processed</param>
        /// <param name="culture">The culture name, or empty string for non-variants</param>
        /// <returns></returns>
        public string GetAssignedNodeName(Rule rule, string culture)
        {
            string assignedNodeName = null;

            //Get the dictionary item if a dictionary key has been specified in config
            if (rule.DictionaryItemForName != "")
            {
                try
                {
                    if (!string.IsNullOrEmpty(culture))
                    {
                        assignedNodeName = _localizationService.GetDictionaryItemByKey(rule.DictionaryItemForName).Translations.First(t => t.Language.CultureInfo.Name.InvariantEquals(culture)).Value;
                    }
                    else
                    {
                        assignedNodeName = _localizationService.GetDictionaryItemByKey(rule.DictionaryItemForName).Translations.First().Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, MessageConstants.ErrorDictionaryKeyNotFound);
                }
            }

            //If no dictionary key has been found, fallback to the standard name setting
            if (string.IsNullOrEmpty(assignedNodeName)) { assignedNodeName = rule.NodeName; }

            return (assignedNodeName);
        }
    }
}