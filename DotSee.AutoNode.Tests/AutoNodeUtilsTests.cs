using Moq;
using NUnit.Framework;
using Serilog;
using System.Collections.Generic;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace DotSee.AutoNode.Tests
{
    [TestFixture]
    internal class AutoNodeUtilsTests
    {
        private Mock<ILocalizationService> _localizationService;
        private Mock<GlobalSettings> _globalSettings;
        private DictionaryItem _dictItem;

        [OneTimeSetUp]
        public void Setup()
        {
            _localizationService = new Mock<ILocalizationService>();
            _globalSettings = new Mock<GlobalSettings>();
            _dictItem = new DictionaryItem("nodeDictionaryItemName");
            var enTranslation = new DictionaryTranslation(new Language(_globalSettings.Object, "en"), "enDictionaryNodeName");
            var frTranslation = new DictionaryTranslation(new Language(_globalSettings.Object, "fr"), "frDictionaryNodeName");
            var translationList = new List<IDictionaryTranslation>();
            translationList.Add(enTranslation);
            translationList.Add(frTranslation);
            _dictItem.Translations = translationList;
        }

        [Test]
        public void DefaultSettings()
        {
            var ruleNodeName = "defaultNodeName";

            var _autoNodeUtils = new AutoNodeUtils(Mock.Of<ILogger>(), Mock.Of<ILocalizationService>());
            Rule rule = new Rule("createdDocTypeAlias", "docTypeAliasToCreate", ruleNodeName);
            string culture = "";
            Assert.IsTrue(_autoNodeUtils.GetAssignedNodeName(rule, culture) == ruleNodeName);
        }

        [Test]
        [TestCase("", "enDictionaryNodeName")]
        [TestCase("en", "enDictionaryNodeName")]
        [TestCase("fr", "frDictionaryNodeName")]
        public void NameFromDictionaryNoCulture(string culture, string dictionaryItemName)
        {
            var ruleNodeName = "defaultNodeName";

            _localizationService.Setup(x => x.GetDictionaryItemByKey("nodeDictionaryItemName")).Returns(_dictItem);
            var _autoNodeUtils = new AutoNodeUtils(Mock.Of<ILogger>(), _localizationService.Object);
            Rule rule = new Rule("createdDocTypeAlias", "docTypeAliasToCreate", ruleNodeName, dictionaryItemForName: "nodeDictionaryItemName");

            Assert.IsTrue(_autoNodeUtils.GetAssignedNodeName(rule, culture) == dictionaryItemName);
        }
    }
}