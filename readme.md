# Umbraco-AutoNode
This is a simple plugin that automatically creates new child nodes in the Umbraco back end upon publishing a node, based on a set of user-defined rules.

## Usage
On application start, you can register one or more rules for the package. A sample event handler is already included in the package, with a rule example (commented out). 

Below is an example of instantiating AutoNode (it's a Singleton) and registering a rule. 
In this example, the rule is expecting a document with doctype alias "TextPage" to be published. 
If this occurs, then a new child document of doctype "Dummy" will be automatically created, and the name of the node will be "Dummy Auto Inserted Node". 
The new node will be created regardless of whether the "TextPage" node has any children, and if there are other nodes, they will be sorted so that the "Dummy" node is brought first in natural order.

For more information, have a look at App_Code/AutoNodeEvents.cs

```csharp
  AutoNode au = AutoNode.Instance;
  
  au.RegisterRule(new AutoNodeRule(
    createdDocTypealias: "TextPage",
    docTypeAliasToCreate: "Dummy",
    nodeName: "Dummy Auto Inserted Node",
    bringNodeFirst: true,
    onlyCreateIfNoChildren: false));
```
## Limitations / Warnings
You should not specify circular rules (i.e. one rule for creating doc B when A is published and one rule for creating doc A when B is published - this will cause an endless loop and leave you with a huge tree if you don't stop it on time :). You can, however, create multiple rules for the same document type. That is, you may want docs B, C, and D to be automatically created when A is published, so you will have to create 3 rules. 

The plugin creates the subnode only if there isn't any child node of the same doctype.

The plugin works for new nodes as well as existing nodes (if they are published and there is a rule in place and no subnode of the given doctype already exists).

This plugin works only with doctypes, so it's not possible at the moment to have any other criteria for automatic document creation. IF you need this, I'll be happy to add more logic.

This plugin was made to cover very specific, simple needs, like having a "Files" grouping node created automatically under each new "Article" node. It doesn't do miracles, so don't expect any :)

