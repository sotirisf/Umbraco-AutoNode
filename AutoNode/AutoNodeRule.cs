namespace DotSee
{
    /// <summary>
    /// Holds rules for automatically creating nodes.
    /// </summary>
    public class AutoNodeRule
    {
        public string CreatedDocTypeAlias { get; set; }
        public string DocTypeAliasToCreate { get; set; }
        public string NodeName { get; set; }
        public bool BringNewNodeFirst { get; set; }
        public bool OnlyCreateIfNoChildren { get; set; }
        public bool CreateIfExistsWithDifferentName { get; set; }
        public string DictionaryItemForName { get; set; }

        /// <summary>
        /// Creates a new rule for automatically creating nodes.
        /// </summary>
        /// <param name="createdDocTypealias">The document type Alias to look for. The rule will be applied when a document of this type is published.</param>
        /// <param name="docTypeAliasToCreate">The document type to automatically create</param>
        /// <param name="nodeName">The name of the newly created document</param>
        /// <param name="bringNodeFirst">If this is set, then the node will be sorted in order to be first (as opposed to the default last position)</param>
        /// <param name="onlyCreateIfNoChildren">If this is set to true, then the new node will only be created if the node published hasn't already got any child nodes.</param>
        /// <param name="createIfExistsWithDifferentName">By default the rule does not create a node automatically only if another node of the same type with the same name already exists. By setting this parameter to false you can allow the creation of a node when one of the same type (but with a different name) already exists.</param>
        public AutoNodeRule( 
            string createdDocTypealias, 
            string docTypeAliasToCreate, 
            string nodeName, 
            bool bringNodeFirst = false, 
            bool onlyCreateIfNoChildren = false, 
            bool createIfExistsWithDifferentName = true,
            string dictionaryItemForName ="AutoNode.Name"
            )
        {
            CreatedDocTypeAlias = createdDocTypealias;
            DocTypeAliasToCreate = docTypeAliasToCreate;
            NodeName = nodeName;
            BringNewNodeFirst = bringNodeFirst;
            OnlyCreateIfNoChildren = onlyCreateIfNoChildren;
            CreateIfExistsWithDifferentName = createIfExistsWithDifferentName;
            DictionaryItemForName = dictionaryItemForName;
        }
    }
}