2024-02-16 v1.4.8

- Fix: App crashes when clicking on header.
- Fix: Issue with plugins not scrolling.

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
