# Umbraco-AutoNode
This is a simple plugin that automatically creates new child nodes in the Umbraco back end upon publishing a node, based on a set of user-defined rules.

## Usage (using configuration file in /config folder)
In your /config folder, you will find autoNode.config with a commented example of a rule.

Here's an example rule:

```xml
 <rule 
    createdDocTypeAlias="TextPage" 
    docTypeAliasToCreate="Dummy" 
    nodeName="Dummy Name" 
    bringNewNodeFirst="true" 
    onlyCreateIfNoChildren="false" 
    createIfExistsWithDifferentName="false" 
    dictionaryItemForName =""
  >
  </rule>
 ```

* **createdDocTypeAlias**: The document type alias of the document being published. IF the document being published has the specified doctype alias, then the rule will execute.
* **docTypeAliasToCreate**: The document type alias of the document that will be automatically created as a child document.
* **nodeName**: The name of the newly created node.
* **bringNodeFirst**: If set to true, will bring the newly created node first in the Umbraco back-end.
* **onlyCreateIfNoChildren (optional)**: This, naturally, regards republishing. If set to true, then republishing a node that already has child nodes (including any already automatically created nodes) will NOT run the rule. If set to false, the rule will run even if the node being published already has children. **Note**: If this setting is set to false and there are already automatically created nodes under the node being published, then they won't be created again. (The check is performed on both doctype and node name as defined in rules - if such a node is found, it will not be created again)
* **createIfExistsWithDifferentName (optional)**: This is true by default - it means that if you rename the automatically created node and republish its parent, a new node will be created. If you need to restrict node creation even more, then you can set this to False and it will not create a new node when a node of the same doctype is found.
* **dictionaryItemForName (optional)**: The key for a dictionary item which will specify what the name of the new node will be in a multilingual Umbraco installation. This means that new nodes will take their names according to the value of this dictionary entry and names will be different for each language. (The createIfExistsWithDifferentName setting also takes multilingual names under consideration).If the dictionary key is not found or the corresponding dictionary entry contains no value, then it falls back to the default new node name as defined in the rule.

## Usage (using code)
You can also register one or more rules for the package on application start. 
All you need to do is get the AutoNode instance (it's a Singleton) and use the RegisterRule method. 
You can use both a configuration file and code if you like. All rules will be added.

```csharp
  AutoNode au = AutoNode.Instance;
  
  au.RegisterRule(new AutoNodeRule(
    createdDocTypealias: "TextPage",
    docTypeAliasToCreate: "Dummy",
    nodeName: "Dummy Auto Inserted Node",
    bringNodeFirst: true,
    onlyCreateIfNoChildren: false,
    createIfExistsWithDifferentName: false,
    dictionaryItemForName: "MyDictionaryKey"
    ));
```

You should use this call in the ApplicationStarted event handler,
in a new class you can create in your App_Code folder like below:

```csharp

using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

    public class AutoNodeEvents : ApplicationEventHandler
    {

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarted(umbracoApplication, applicationContext);

            AutoNode au = AutoNode.Instance;

			//This is an example rule, put your own here
 			au.RegisterRule(new AutoNodeRule(
    			createdDocTypealias: "TextPage",
    			docTypeAliasToCreate: "Dummy",
    			nodeName: "Dummy Auto Inserted Node",
    			bringNodeFirst: true,
    			onlyCreateIfNoChildren: false,
			createIfExistsWithDifferentName: false,
    			dictionaryItemForName: "MyDictionaryKey"
			));
        }
    }
```

## Limitations / Warnings
You should not specify circular rules (i.e. one rule for creating doc B when A is published and one rule for creating doc A when B is published - this will cause an endless loop and leave you with a huge tree if you don't stop it on time :). You can, however, create multiple rules for the same document type. That is, you may want docs B, C, and D to be automatically created when A is published, so you will have to create 3 rules. 

The plugin creates the subnode only if there isn't any child node of the same doctype (by default it checks whether the existing node has the same name as defined in the rule, but you can override that and check only for doctype). 

The plugin works for new nodes as well as existing nodes (if they are published and there is a rule in place and no subnode of the given doctype already exists).

This plugin works only with doctypes, so it's not possible at the moment to have any other criteria for automatic document creation. IF you need this, I'll be happy to add more logic.

This plugin was made to cover very specific, simple needs, like having a "Files" grouping node created automatically under each new "Article" node. It doesn't do miracles, so don't expect any :)

