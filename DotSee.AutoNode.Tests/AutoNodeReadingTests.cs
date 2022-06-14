using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;


namespace DotSee.AutoNode.Tests
{
    [TestFixture]
    internal class AutoNodeReadingTests
    {
       

        public AutoNodeReadingTests()
        {
          
            
        }

        private IRuleProviderService ruleProviderService { get; set; }
        

       [Test]
       public void ReadXMLRules()
        {
            this.ruleProviderService = new XmlFileRuleProviderService(Mock.Of<Serilog.ILogger>(), new ConfigSource { SourcePath = (@".\App_Plugins\DotSee.AutoNode\autoNode.config") });
            Assert.IsTrue(this.ruleProviderService.Rules.Any());
        }
        [Test]
        public void ReadJSONRules()
        {
            this.ruleProviderService = new JsonFileRuleProviderService(Mock.Of<Serilog.ILogger>(), new ConfigSource { SourcePath = (@".\App_Plugins\DotSee.AutoNode\autoNode.json") });
            var rules = this.ruleProviderService.Rules;
            Assert.IsTrue(rules.Any());
        }
    }
}
