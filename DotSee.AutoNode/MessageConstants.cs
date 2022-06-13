namespace DotSee.AutoNode
{
    public class MessageConstants
    {
        public const string ErrorConfigNotFound = "AutoNode: Configuration file was not found.";
        public const string ErrorCreateNode = "AutoNode: There was a problem creating node '{0}' under node '{1}'.";
        public const string ErrorDictionaryKeyNotFound = "AutoNode: The dictionary key specified in autoNode.config was not found.";
        public const string ErrorGeneric = "AutoNode: There was a problem with new node creation. Please check that the doctype alias you have defined in rules actually exists";
        public const string ErrorLoadConfig = "AutoNode: There was a problem loading AutoNode configuration from the config file";
        public const string ErrorNodeAliasNotFound = "AutoNode: Document type '{0}' does not exist. Aborting.";
        public const string ErrorRepublishNoSuccess = "AutoNode: Node '{0}' was not republished successfully under node '{1}'.";
        public const string ErrorSortFailed = "AutoNode: Bring new node first failed.";
        public const string InfoAbortCreateNodeNodeExists = "AutoNode: Aborting node creation since node already exists";
        public const string InfoAbortCreateNodeRuleRestrictions = "AutoNode: Aborting node creation due to rule restrictions. Parent node already has children, rule indicates that parent node should not have children";
        public const string InfoCreateNodeSuccess = "AutoNode: Node '{0}' was created successfully under node '{1}'.";
        public const string InfoLoadConfigComplete = "AutoNode: Loading configuration complete";
        public const string InfoLoadingConfig = "AutoNode: Loading configuration...";
        public const string InfoRepublishingExistingNode = "AutoNode: Republishing already existing child node...";
        public const string InfoSortingNodes = "AutoNode: Bringing newly created node first...";
        public const string InfoTryCreateNode = "AutoNode: Trying to automatically create node of type {0} for node {1} of type {2}...";
        public const string InfoNotRepublishingExistingNode = "AutoNode: Skip republishing node {0} since it already exists and settings disallow republishing of existing nodes";
    }
}