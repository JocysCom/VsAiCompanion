2024-09-14 v1.12.85

- Fix: Morality formula and example in `AI - Evaluate Morality` template.
- Fix: OpenAI `o1` model max input tokens.
- Fix: AI Function tool calls when the response isn't streamed.
- New: [Send] button: Hold CTRL to add as a user message, ALT for an assistant message.
- New: Added an AI tool function to get PDF metadata.
- New: Save or copy all tool functions as JSON for AI self-improvement.
- New: Added an item status property to lists.

2024-08-28 v1.12.76

- Fix: The AI stops making direct function calls.
- Fix: Reduced the extension installer by removing Linux and macOS files.
- New: Added an AI tool function to count content and file tokens.

2024-08-26 v1.12.72

- Fix: Help documents embedded in the app.
- Fix: Display function calls as YAML; otherwise, the AI stops making direct function calls.
- Fix: Unable to stop message requests.
- Update: Enhance AI tool functions in Visual Studio.
- New: AI tool function to convert PDF to images. 
- New: AI tool function to convert PDF to structured JSON.

2024-08-18 v1.12.64

- Fix: Sometimes the tray notification icon wasn't removed after exiting the app.
- Fix: Crash when retrieving a list.
- Fix: The app removes models with the same name in different services.
- Fix: The saving and loading of size and position on monitors with different scaling percentages.
- New: Option to require the AI model to use a tool function. Requires `gpt-4o` 2024-08-06+.
- Update: Embedding processing groups and sub-groups data from the folder structure by default.
- Update: Updated `Prompts` list.

2024-08-07 v1.12.52

- New: Make Instructions checkbox bold when instructions are not empty.
- New: command line argument /SettingsPath="<settings_folder_path>".
- New: command line argument /SettingsFile="<settings_zip_file>".
- New: Edit list buttons on the Personalised Context panel.
- Update: Allow copying and pasting multiple items.
- Update: Allow pasting copied items as XML files.

2024-07-24 v1.12.45

- Fix: Resolve access token issues with Microsoft Azure.
- New: Add a "Require to Sign In" option for the Microsoft account.
- New: Hold the CTRL button to minimise app before taking the screenshot.
- Update: The extension now uses the Microsoft SQL client.
- Update: Update packages and templates.

2024-07-16 v1.12.35

- Fix: Error when sending a message.

2024-07-15 v1.12.34

- Fix: Crash when importing files into embedding database.
- Fix: Embedding subgroup names displayed from a different database.

2024-07-11 v1.12.32

- Fix: AI is not using speech if the avatar control is not focused.
- Update: Send personalized context data as a system message.
- Update: Allow sending more context/list items.
- Update: Upgraded OpenAI client to v2.0.

2024-07-10 v1.12.27

- New: Option "Max risk level when signed out" when the app is in the company domain.
- New: Use lists as a data source for prompts.
- New: Option to enable Microsoft account. Default: disabled.
- New: Option to specify the number of task items in the tray bar.
- Fix: Update maximum risk level limit when user signs in (corporate option).
- Fix: Prevent crash when the user pastes an invalid XML character into the chat.
- Fix: Rename the `AnalyseImage` function to `AnalysePicture` to make its purpose clearer to the AI.

2024-07-03 v1.12.19

- Fix: Key vault settings not being saved when updated. 
- New: File attachment button in Tasks and Templates chat panel.

2024-07-03 v1.12.17

- Fix: Pressing CTRL+C can crash the Visual Studio Extension.

2024-07-02 v1.12.15

- Fix: Error when AI calls the function inside Visual Studio.
- Fix: Some UI elements are not in the list of UI Preset choices.
- New: Allow overriding default settings with the `<ExeBaseName>.Settings.zip` file.

2024-07-01 v1.12.10

- Fix: Unable to hide or show some UI elements.
- New: Allow overriding the head and body message in the top info panel.

2024-06-30 v1.12.6

- Fix: List not expanding.
- Fix: Improved support for Azure SQL Server Databases.
- Fix: Unable to stop message requests.
- Fix: Embedding search group filter not working correctly.
- Fix: Allow CTRL+C copy in web browser from Visual Studio extension.
- Update: SQL now uses native SQL script AI search instead of C# Assembly.
- Update: UI, Templates and AI Avatar updated.
- New: Create UI presets, like "Simple" or "Advanced".

2024-06-16 v1.11.25

- New: The tool will ask the AI to provide a reason for making function calls.

2024-06-12 v1.11.23

- Fix: App crashes when trying to log into the same file.
- Update: Improve the loading process in Visual Studio.

2024-06-11 v1.11.21

- Fix: Developer options are not visible.

2024-06-11 v1.11.20

- Fix: The app crashes if there are no video input devices.
- Fix: Crash on start in Visual Studio 2022 17.10.2.
- New: Crash message when crashing in Visual Studio.
- New: Developer option for the error panel.
- New: Option to log HTTP messages.
- New: AI function to get the current date, OS version, architecture, locale, and time zone.

2024-06-10 v1.11.12
 
- Fix: Assistant's reply message has the same date and time as the user's prompt.
- Fix: Lists not updating in comboboxes after renaming or moving items.
- Fix: The app sends voice instructions when the avatar is invisible and "Use voice" is unchecked.

2024-06-09 v1.11.8
 
- Fix: When "Use Voice" was checked, the app didn't send avatar voice instructions.
- Fix: Closing the Avatar window would freeze after switching to other tabs.

2024-06-09 v1.11.6

- Fix: App stopping itself from being dragged into negative X desktop coordinates.
- Fix: Reduce memory usage by 30%.
- Fix: Move app window within screen bounds if settings place it outside the screen.
- New: Add centralized API key management support with Azure Key Vault.
- Update: Add an optional AI avatar to the Tasks form.

2024-05-22 v1.10.41

- Fix: Crash when unable to decrypt API keys.
- Fix: Crash when resetting settings if the settings file is unavailable.

2024-05-21 v1.10.37

- Fix: Embeddings dropdown not updating when the embedding name is changed.
- Fix: Embeddings dropdown displayed the wrong selected value.
- Fix: Crash when the avatar tries to animate lips in a foreign language with non-standard letters.

2024-05-20 v1.10.32

- Fix: Format and generate feature, not using the specified services.
- Fix: Sometimes the order of sentences in voice speech would be incorrect.
- New: Allow users to provide function request denial comments to AI.
- New: Option to all global AI instructions.
- New: Save the chat as an HTML file.
- New: Copy the chat to the clipboard.
- New: Save avatar voice as MP3 files.

2024-05-15 v1.10.21

- Fix: Selected AI model sometimes resets when switching between different AI services.
- New: You can now take screenshots in the chat window.
- Update: More variations of partial XML SSML content are now supported.
- Update: Use a queue to play all incoming avatar speeches in sequence.

2024-05-14 v1.10.14

- Fix: Error with domain security code prevents the use of plugins.
- Fix: Sometimes SSML speech synthesis fails.
- Fix: GPT-4o's max input tokens.
- Fix: Editing a user message was sending the old message to the AI.
- Update: Hide the chat panel on large pages to make resizing smoother.
- Update: Switched all templates to GPT-4o.

2024-05-13 v1.10.6

- Fix: Crash when reading data from the embedding database.
- Fix: Crash when customized settings file not found.
- New: Added AI Avatar that can use Microsoft Azure Voices.
- New: Button to start and stop Microsoft Voice Typing.
- Update: OpenAI Client now supports the analysis of larger images.

2024-04-28 v1.9.41

- New: Timestamp column added to the Embedding database for synchronizations.
- New: Add a plugin function to allow the AI to search the AI embedding database.
- New: Option to restrict the maximum risk level in the config and in Windows domain groups.
- New: Analyzes image URLs as per given instructions with an AI model.
- Update: Use the name of the embedding to select the AI database.

- 2024-04-18 v1.9.32

- Fix: Combo-boxes not updating when `Lists` are updated.
- Fix: Unable to embed some data due to incorrect token count.
- Fix: Crash when creating a new AI embedding database.
- Fix: Updated token conversion to import more accurate data into AI database.
- Fix: Couple crashes fixed.
- New: Ability to read RTF and HTML documents as plain text.
- New: Ability to read web pages as as plain text.

- 2024-04-16 v1.9.21

- Fix: Mask connection password in Embedding screen.
- Fix: Task tile was autogenerating after task rename.
- New: AI Model List Control
- New: Allow changing Path (Task) of List Item.
- New: Allow to set group flag name in AI Database.
- New: Plugin for AI email sending.

2024-04-11 v1.9.12

- Fix: Embedding is not importing Word, Excel, and PDF files into the database.
- Fix: Paste copied items into the list.
- New: Add item groups to the list.
- Update: Make list names case-insensitive when the AI is searching.

2024-04-09 v1.9.6

- Fix: Fixed multiple issues and bugs.
- New: Added AI Embedding/Context Database, supports portable SQLite and MSSQL.
- New: Ability to read Word, Excel, and PDF documents as plain text.
- New: Improved AI's capability to read multiple files in one action.
- Update: Updated shared libraries.

2024-03-17 v1.8.6

- Fix: The AI was unable to update the List instructions.
- Fix: Saving scroll position.
- Fix: The Options window is now scrollable.
- New: Role and Profile templates.
- New: List sort and Insert List item methods.
- New: Option to convert the fine-tuning files to and from Markdown format.
- New: Update feature for the application.
- Update: Settings now save when application is deactivated.

2024-03-13 v1.7.19

- Fix: Unable create the first item on the list manually.

2024-03-12 v1.7.18

- Fix: Database function "SetDescription."
- Fix: AI was unable to create or update lists.
- New: Now AI can request API specifications and call web services.
- New: "API - Demo" list with API web service example.

2024-03-11 v1.7.9

- Fix: Default lists are not loaded if settings already exist.
- Fix: Automatically add missing templates on load.
- Fix: Sort order of templates.

2024-03-10 v1.7.6

- Fix: The standalone app lists Visual Studio functions that are not available for AI.
- Fix: Tooltip text cropping issue in InfoPanel.
- Fix: A confirmation dialog pops up to save settings when multiple list items are selected.
- New: Functions that enable AI to manage different types of lists, such as task, to-do, progress, or environment properties.
- New: Personalize the chat by supplying personalized context.

2024-03-04 v1.6.25

- Fix: Enable medium-risk level plugins by default. Low-risk level is selected for tasks by default.
- Fix: Not all settings would load properly for the first start.

- 2024-03-02 v1.6.20

- Fix: AI Functions with enums failing.
- New: Web content and download functions with default credentials.
- New: Added function category descriptions.
- New: TTS support. Can be used with Jocys.com TTS Monitor. Ask to create a very short funny dialog involving the dragon and voice it.
- Update: The function approval form will now display only the supplied parameters.
- Update: Text file read and modify functions improved to be more understandable for AI.
- Update: Updated some tooltips to improve clarity.
 
2024-02-29 v1.6.13

- Fix: Issue with inability to cancel message requests.
- New: Plugin for searching Windows Index.

2024-02-28 v1.6.11

- Fix: Extension license file.
- Update: The function approval process has been updated.
- Update: Approval control is now within the app instead of in a popup message box.

2024-02-27 v1.6.6

- Fix: 3rd attempt to fix the "Method not found: ...Generic.IAsyncEnumerable" issue.
- Fix: Settings.CompanyName.zip example file.
- Fix: Auto clean up AI models without services on service delete.
- Update: Don't deselect the list when making mass changes to properties.
- Update: Changed to the less restrictive "Mozilla Public License v2.0".

2024-02-26 v1.5.16

- Fix: Update to address the issue of "Method not found: ...Generic.IAsyncEnumerable"
- Fix: Enable chat history when enabling Plugins.
- Fix: Redirect to options when API key is missing for Microsoft API.
- New: Added `ReadTextFile` and `WriteTextFile` functions.

2024-02-25 v1.5.12

- Fix: The app is overwriting newer settings.
- New: Customizable setting options for enterprises.

2024-02-25 v1.5.6

- New: Added new Basic and Visual Studio functions.
- New: Added option for maximum risk level.
- Fix: Downgraded some packages so that Extension can run on older versions of VS 2022.

2024-02-22 v1.4.24

- Update: Added a "context type" property to documents to improve AI responses.
- Update: Updated the item template control.
- New: Added splash screen to the standalone app.

2024-02-20 v1.4.21

- New: Added Database Functions.
- New: Added the Ability to Read and Write Files.
- Fix: Content Retrieval from Visual Studio.

2024-02-19 v1.4.18

- Fix: Wrap text issue in Plugins' descriptions.
- Fix: Error report. Add more details.
- Fix: Issue where some Visual Studio functions would fail.

- 2024-02-18 v1.4.15

- Fix: App crashes when clicking on header.
- Fix: Issue with plugins not scrolling.
- Fix: Make sure system templates are recreated if missing.
- New: VS plugins. Mention "selection" or "open document" and AI will know how to get the code.

2024-02-14 v1.4.6

- New: Plugin Feature. You can allow AI to run apps and scripts. Disabled by default.
- New: "System - Plugin Approval" template. Assess action safety with a secondary AI.
- Update: Upgraded the standalone app to .NET 8.0.
- Update: Updated NuGet Packages.

2024-01-28 v1.3.28

- Fix: Maximum token value for preview models.
- New: "Maximize" and "Restore" buttons to the message and instructions textbox.
- New: Added new "Code - Localize XAML" template.
- New: Visual Studio "Open Documents" added to attachment types.

2024-01-02 v1.3.24

- New: Option to set model names manually.
- New: Allow settings to be stored in the program folder.
- New: Enable bulk changes to the list items when more than one is selected.

2023-11-26 v1.3.20

- Fix: Crash when refreshing models.
- Update: Updated some templates.

2023-11-12 v1.3.17

- Update: Updated some templates.

2023-11-12 v1.3.16

- Fix: Resolved several potential crashes during file conversions.
- New: Added the option to use the maximum auto-detected context size.
- New: Introduced new templates for "Writing" and "AI - Evaluate Morality".
- Update: Now tool autofocus on the Tasks tab when a new task is created.

2023-11-05 v1.3.6

- New: Added a new feature to fine-tune custom models.
- New: Added new templates for "Writing".

2023-09-29 v1.2.13

- Update: Hide the prompting bar by default.
- Update: Require hold CTRL key to paste a markdown block from the clipboard.

2023-09-28 v1.2.11

- Fix: The letter 's' will be trimmed in the messages window.
- Fix: Invalid maximum token size for models.
- New: Added new "Language" prompting template.
- New: Button to insert custom and "PowerShell" markdown language block.

2023-09-26 v1.2.6

- Fix: Crash when attempting to send a preview message.
- New: Button toolbar for pasting and inserting markdown language blocks.
- New: Added "Code - Simplify" template.
- Update: Minor user interface enhancements and theme updates.

- 2023-09-25 v1.1.18

- Fix: Crashed when sending the message.
- Fix: Incorrect service being called for instruct models.

2023-09-24 v1.1.16

- New: Option to toggle between user and system message instructions.
- Update: Minor user interface enhancements and theme update.

2023-09-17 v1.1.12

- New: Prompting feature that helps to customize the AI's output.

2023-09-07 v1.1.6

- Fix: Task cloning, stop sending.
- New: Added support for streaming replies.
- New: Added service timeout option.
- New: Warning when API URL is insecure and not local.
- Update: Improved support for Azure OpenAI.
- Update: Improved support for GPT4All.

2023-08-31 v1.0.115

- Fix: Unable to hide template or task list panel.

2023-08-29 v1.0.114

- Fix: Unable to add new messages after using the Edit and Remove buttons.
- New: Added [Regenerate] button to the last user message.

2023-08-28 v1.0.112

- Fix: Loading of grid splitter settings.

2023-08-28 v1.0.111

- Fix: Reset value of AI Service dropdown box.
- Fix: Improved list navigation for smoother operation.
- Fix: TrayNotifyIcon visible after app shutdown.
- New: Zoom slider for chat window.
- Update: Resolved some UI and theme issues.

2023-08-23 v1.0.103

- New: "Enable Spell Check" option.
- Fix: AI Service dropdown box value reset.

2023-08-21 v1.0.101

- Fix: AI service setting in templates.
- Fix: Copy AI service value from template.
- Fix: Initial app start position.
- Fix: Dropdown box theme.

2023-08-20 v1.0.97

- Fix: Corrected dropdown box theme.
- Update: Information on About control.

2023-08-17 v1.0.95

- Fix: In message edit mode, following messages won't be deleted if sent using ENTER key.

2023-08-16 v1.0.94

- New: [Edit] button has been added. Edit chat message and delete any messages that follow it.
- Update: Grammar instruction template updated to make it easier to copy the corrected sentence.
- Update: Updated some default settings.

2023-08-14 v1.0.91

- New: Added support for multiple API services, including local machine, on-premise, or cloud.
- New: Option to send a message with ENTER. Use SHIFT+ENTER for a new line.
- Update: UI style updated.

2023-08-04 v1.0.82

- Fix: Remove duplicate option to attach chat logs.
- Fix: The visibility of the "Show Icon in Toolbar" checkbox.
- Fix: The URL for Markdown help is not opening.
- Fix: Default icon is missing when creating a new task.
- Update: UI style updated.

2023-07-31 v1.0.77

- Fix: Crash when no error is selected while fixing the selected error.
- Fix: Crash when no document is open while replacing the selection.

2023-07-16 v1.0.75

- Fix: Add missing title and markdown format templates.
- Update: Theme has been updated and more tooltips have been added.

2023-07-15 v1.0.73

- New: Option to use AI to auto-format your message with markdown.
- New: Option to use AI to auto-generate chat title.
- New: Button to re-generate chat title.
- New: Separated Visual Studio options for clarity.
- Fix: URLs open inside the message control instead of a new browser window.
- Fix: The scroll does not stay at the bottom when the message box is resized. 

2023-07-12 v1.0.67

- Fix: Template bar does not display button to access all templates.
- Fix: Code box expands outside of the message window.
- Fix: The scroll does not stay at the bottom when resizing.
- New: Stop button for cancelling prompt requests.

2023-07-11 v1.0.61

- Fix: HTML and markdown text weren't being displayed properly.
- New: Code is displayed in a code box now.
- New: A copy button has been added to each code block.

2023-07-09 v1.0.57

- Fix: Preview messages are excluded from the chat log now.
- Fix: Chat log markdown display.
- Update: The chat log is now submitted to the API correctly.
- Update: AI model in templates.
- Update: Improved instructions in the grammar template to ensure more consistent results.

2023-07-07 v1.0.52

- Fix: Error with the limit of reply tokens.
- Fix: AI models not refreshing in the select box when [Refresh] is clicked.
- Fix: Maximum height for the message and instructions text box.
- Fix: The scroll does not stay at the bottom when resizing.
- Fix: The HTML code display in the message box was sometimes incorrect.
- Fix: Settings not saving when exiting from Tray menu.

2023-07-05 v1.0.47

- Fix: The entire list refreshes when a task is removed or updated on the disk.
- Fix: Display XML elements within the message.
- Fix: App not restoring the last Task selection upon restart.

2023-07-02 v1.0.42

- Fix: Issue when attempting to paste an item with the same name.
- Fix: Corrected message output display.
- New: Now allows for attachment of multiple item types to AI with the same message.
- New: Added the option to send selected errors.
- New: Added the capability to send debugging exception errors.
- New: Feature to send relevant files/documents for exceptions.
- New: Show a warning when files are sent to the AI.
- New: Show a warning when files sent to the AI contains possible sensitive data.

2023-06-27 v1.0.35

- Fix: Send button opacity when a message is changed.
- Update: Reduce the amount of space that attachments and data occupy in the chat.

2023-06-26 v1.0.33

- Fix: AI response added to a different task.
- Fix: Use Macros Template property not copying to the Task.
- New: Clear all messages button.
- New: Scroll to the bottom button.

2023-06-25 v1.0.28

- New: Include macros for the Environment.
- New: Easily access tasks from the Tray Notification icon.
- Fix: Correct rendering of template list when switching tabs.
- Fix: Automatically enable macros if they are used.

2023-06-22 v1.0.26

- Fix: Send button opacity when a message is changed.

2023-06-22 v1.0.25

- Fix: Some Template properties not copying to the Task.
- New: Separate the AI instructions from the main message.
- Update: Updated all templates.

2023-06-21 v1.0.23

- New: Added an "Allow Only One Copy" option for standalone application.
- Update: The "Minimize On Close" option is now enabled by default.

2023-06-20 v1.0.22

- Fix: Changed how the chat log is sent to allow for better AI interpretation.
- Fix: Updated the API requests spinning indicator.
- New: Added feature to show the count of active API requests on the spinning indicator.
- New: Introduced ability to customize the base URL.
- New: Standalone program can now be restored, focused, or minimized to the tray by clicking on the notification icon.
- Update: Renamed AI creativity setting from "Normal" to "Balanced".

2023-06-18 v1.0.19

- New: Public release.

2023-04-11 v1.0.6

- New: Internal release.
