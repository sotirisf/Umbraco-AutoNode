using Moq;
using NUnit.Framework;
using System;
using System.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;

namespace DotSee.AutoNode.Tests
{
    [TestFixture]
    internal class AutoNodeCreationTests
    {
        private Mock<IContentService> _cs;
        private Mock<IContentTypeService> _cts;
        private Mock<IContentType> _ct;
        private Mock<ISimpleContentType> _sct;
        private Mock<ISimpleContentType> _sctTarget;
        private Mock<IContentType> _ctTarget;
        private AutoNodeService _au;
        private AutoNodeUtils _auUtils;
        private Mock<PublishResult> _publishResult;

        private Mock<IContent> z;

        private string _childAlias = "testChild";
        private string _parentAlias = "testParent";

        [OneTimeSetUp]
        public void Setup()
        {
            ServiceContext.CreatePartial(
                contentService: Mock.Of<IContentService>()
                , contentTypeService: Mock.Of<IContentTypeService>()
                , localizationService: Mock.Of<ILocalizationService>()
               );

            _cs = new Mock<IContentService>();
            _cts = new Mock<IContentTypeService>();

            _ctTarget = new Mock<IContentType>();
            _ctTarget.SetupGet(x => x.Alias).Returns(_childAlias);
            _ctTarget.SetupGet(x => x.AllowedAsRoot).Returns(false);
            _ctTarget.SetupGet(x => x.Id).Returns(2);

            _ct = new Mock<IContentType>();
            _ct.SetupGet(x => x.Alias).Returns(_parentAlias);
            _ct.SetupGet(x => x.AllowedAsRoot).Returns(true);
            _ct.SetupGet(x => x.Id).Returns(1);

            _sct = new Mock<ISimpleContentType>();
            _sct.SetupGet(x => x.Alias).Returns(_parentAlias);
            _sct.SetupGet(x => x.AllowedAsRoot).Returns(true);
            _sct.SetupGet(x => x.Id).Returns(1);

            _sctTarget = new Mock<ISimpleContentType>();
            _sctTarget.SetupGet(x => x.Alias).Returns(_childAlias);
            _sctTarget.SetupGet(x => x.AllowedAsRoot).Returns(true);
            _sctTarget.SetupGet(x => x.Id).Returns(2);

            _auUtils = new AutoNodeUtils(
                 Mock.Of<Serilog.ILogger>()
                 , Mock.Of<ILocalizationService>());

            _au = new AutoNodeService(
                _cs.Object
                , _cts.Object
                , Mock.Of<Serilog.ILogger>()
                , Mock.Of<IRuleProviderService>()
                , Mock.Of<ISqlContext>()
                , _auUtils
                );

            var rule = new Rule(_parentAlias, _childAlias, "createdNode", dictionaryItemForName: "");
            _au.RegisterRule(rule);

            _cts.Setup(x => x.Get(rule.DocTypeAliasToCreate)).Returns(_ctTarget.Object);

            var parentGuid = Guid.NewGuid();
            z = new Mock<IContent>();
            z.SetupProperty(x => x.Id, 1000);
            z.SetupProperty(x => x.Name, "lalala");
            z.SetupGet(x => x.Key).Returns(parentGuid);
            z.SetupGet(x => x.ContentType).Returns(_sct.Object);

            var createdNodeMock = new Mock<IContent>();
            createdNodeMock.SetupProperty(x => x.Name, "createdNode");
            createdNodeMock.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            createdNodeMock.SetupGet(x => x.ContentType).Returns(_sctTarget.Object);

            _cs.Setup(x => x.Create("createdNode", parentGuid, _childAlias, -1)).Returns(createdNodeMock.Object);
            _publishResult = new Mock<PublishResult>();
            var publishResult = new PublishResult(null, createdNodeMock.Object);

            string nullstr = null;
            _cs.Setup(x => x.SaveAndPublish(createdNodeMock.Object, nullstr, -1)).Returns(publishResult);
        }

        [Test]
        public void CreateNodeTestRunMethod()
        {
            _cs.Setup(x => x.Create("blah", -1, _parentAlias, -1)).Returns(z.Object);

            _cs.Setup(x => x.SaveAndPublish(z.Object, "*", -1));
            Assert.IsTrue(_au.Run(z.Object, ""));
        }
    }
}