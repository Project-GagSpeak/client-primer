## This code is very complex and weaves everywhere, so read below if you want to understand it.
#### It can be confusing to understand at first, but in my honest opinion, given some direction and pointers, you can make sense out of it.



## Major Changes (Notes to self)
- Use the new Mediator system instead of events. These are cleared now entirely

- Remove the entire message encryption system. This is now irrelevant.

- Will need to create a "configurationMigrator" to handle the migration from our 
  old config files to the new ones. (new config system will be a lot more complex, 
  but it will be a lot more organized.)

- Pattern, Wardrobe, Gag Storage Managers are now all interacted with via 
  the ClientConfigurationManager class.

- If ClientConfigurationManager ever gets too messy, use Handler classes for components.

- Internal Logger may be removed entirely

- Logic for various components is now stored under "ModuleLogic" section.

- Image handling is a wait and find out. It messes with my head too much.

- All forms of tracking (Typically done in the hardcore manager) for data, is now handled in a
 "ActivityTracking" folder. (key tracking, other functions, maybe rename to monitored updates?)

- Chat will remain its folder, however its purpose will fall under the Monitored
