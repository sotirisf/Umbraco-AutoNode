using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotSee.AutoNode
{
    public class JsonFileRuleProviderService : IRuleProviderService<AutoNodeJsonRules>
    {
        private Dictionary<string, string> _settings;
        private IEnumerable<Rule> _rules;
        private AutoNodeJsonRules _configType = null;
        private readonly IConfigSource _configSource;
        private readonly ILogger _logger;

        public JsonFileRuleProviderService(ILogger logger, IConfigSource configSource)
        {
            _logger = logger;
            _configSource = configSource;
            _settings = new Dictionary<string, string>();
        }

        public Dictionary<string, string> Settings
        {
            get
            {
                return (_settings == null || !_settings.Any()) ? this.ConfigType.Settings : _settings;
            }
        }

        public IEnumerable<Rule> Rules
        {
            get
            {
                return (_rules == null || !_rules.Any()) ? ProcessRules() : _rules;
            }
        }

        IConfigSource IRuleProviderService.ConfigSource { get => _configSource; }

        public AutoNodeJsonRules ConfigType => _configType ?? GetConfigFromJson();

        public void ReloadData()
        {
            _rules = null;
            _settings = new Dictionary<string, string>();
            _configType = null;
        }

        private AutoNodeJsonRules GetConfigFromJson()
        {
            var jsonConfig = new AutoNodeJsonRules();

            try
            {
                var jsonContent = System.IO.File.ReadAllText(_configSource.SourcePath);

                jsonConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<AutoNodeJsonRules>(jsonContent);
            }
            catch (FileNotFoundException ex)
            {
                _logger.Error(ex, MessageConstants.ErrorConfigNotFound);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, MessageConstants.ErrorLoadConfig);
                return null;
            }

            _logger.Information("AutoNode: Loading configuration complete");
            return jsonConfig;
        }

        private List<Rule> ProcessRules()
        {
            List<Rule> retVal = new List<Rule>();
            foreach (Rule ruleEntry in ConfigType.Rule)
            {
                var rule = new Rule(
                       ruleEntry.CreatedDocTypeAlias
                        , ruleEntry.DocTypeAliasToCreate
                        , ruleEntry.NodeName
                        , ((ruleEntry.BringNewNodeFirst != null) ? ruleEntry.BringNewNodeFirst : false)
                        , ((ruleEntry.OnlyCreateIfNoChildren != null) ? ruleEntry.OnlyCreateIfNoChildren : false)
                        , ((ruleEntry.CreateIfExistsWithDifferentName != null) ? ruleEntry.CreateIfExistsWithDifferentName : true)
                        , ruleEntry.DictionaryItemForName
                        , ((ruleEntry.KeepNewNodeUnpublished != null) ? ruleEntry.KeepNewNodeUnpublished : false)
                        , ruleEntry.Blueprint);

                retVal.Add(rule);
            }
            _logger.Information(MessageConstants.InfoLoadConfigComplete);

            _rules = retVal;
            return retVal;
        }
    }
}