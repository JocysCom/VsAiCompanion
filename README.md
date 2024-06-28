# Jocys.com VS AI Companion
This is a free, open-source project for people who have an [OpenAI API](https://platform.openai.com/) (GPT-3/4, Davinci...) subscription or/and run Open AI on their local machine ([GPT4All](https://gpt4all.io/)), on-premises, or on Azure Cloud. AI Companion answers questions, analyzes project files, and works alongside, streamlining development and boosting productivity. Application allows easy creation of custom, fine-tuned AI models as chatbots or virtual employees. AI Companion can run as a standalone portable application or as a Visual Studio extension. Extension version can be installed and updated via Visual Studio Extension Manager.

### Why Use Tools Utilizing API Instead of Web Chat GPT?

- Data submitted via the API isn't used for model training unless users choose to share it.
- API provides access to more recent and smarter AI models.
- Extensive customization and configuration.

### DOWNLOAD -  v1.11.25 (2024-06-25)  

[JocysCom.VS.AiCompanion.App.zip](https://github.com/JocysCom/VsAiCompanion/releases/download/1.11.25/JocysCom.VS.AiCompanion.App.zip) - digitally signed standalone/portable application.

[AI Companion as Visual Studio Extension on Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=JocysCom.JocysComVsAiCompanion) - install via Visual Studio Extension Manager.

### Requirements

[Microsoft .NET 8.0](https://dotnet.microsoft.com/download/dotnet) (included in Windows 11 by default) for app to work on Windows 8/10.

[OpenAI account (API Key)](https://platform.openai.com/account/) for ChatGPT models to work in AI Companion app:  
AI Companion app ► Options ► AI Services ► Open AI ► enter API Key.

[OpenAI account (Usage Tier 1)](https://platform.openai.com/docs/guides/rate-limits/usage-tiers?context=tier-one) to access GPT-4 models.

[Microsoft Azure account (API Key)](https://github.com/JocysCom/VsAiCompanion/wiki/Feature-%E2%80%90-AI-Avatar) for ChatGPT to answer with voice and mouth animation:  
AI Companion app ► Options ► AI Services ► Speech Service ► enter API Key.

**For AI Companion Visual Studio extension version only:** Visual Studio 2022 17.9+  
App extension version can be installed and updated via Visual Studio Extension Manager.

### Why was this extension created?
Existing tools like GitHub CoPilot have limitations when it comes to interacting with AI. I wanted to create an app that could handle common tasks with more in-depth AI guidance and information. This tool does not replace GitHub CoPilot, but rather offers new features. My goal was to create a tool that would assist AI in responding to inquiries about an entire project or in revamping a Visual Studio solution using a different framework. Right now, you have the ability to request a project rewrite, as the extension can send an entire project or solution. However, there are some necessary updates that need to be made in order to save the outcomes.

### Limitations
[Token Limits](help.openai.com/en/articles/4936856-what-are-tokens-and-how-to-count-them) - depending on the [model](platform.openai.com/docs/models) used, requests can use up to 128,000 tokens shared between prompt and completion. Some models can have different limits on input and output. 
You can ask questions about an entire solution or project using the "Code - Smart Search" template. However, the size of the solution or project is limited by the maximum amount of tokens the AI can process. People who have access to the GPT-4 API can upload projects or solutions for AI analysis, up to sizes of 384KB (128K tokens, about 3 bytes per token). These limitations could potentially be removed if AI is hosted on-premises.

[Rate Limits](platform.openai.com/docs/guides/rate-limits) - restrictions that OpenAI API imposes on the number of times a user or client can access its services within a specified period of time.

### How it works
This application (or extension) allows you to create advanced prompt templates and include data from various sources such as the Clipboard, Selection, Active Document, Selected Documents, Active Project, Selected Project, Solution, Selected Error, Exception with relevant code files, or Chat History. You can execute this template with just one button press, and the data will be sent to your preferred AI model for results at https://api.openai.com.

### Data Safety Concerns
OpenAI will not use the data submitted by customers via the OpenAI API to train or improve its models, unless customers explicitly decide to share their data for this purpose. Customers have the option to opt-in to share data. Please note that this data policy does not apply to OpenAI's Non-API consumer services like ChatGPT or DALL·E. More information can be found at: https://openai.com/policies/api-data-usage-policies."

### Code Security Assurance

AI Tool by utilizing a comprehensive suite of security scan tools to make sure that codebase is safeguarded and free from unresolved issues.

**[Dependabot Security](https://github.com/JocysCom/VsAiCompanion/security/dependabot)**: Detects vulnerabilities in dependencies.
  
**[Code Scanning](https://github.com/JocysCom/VsAiCompanion/security/code-scanning)**: Identifies common vulnerabilities and coding errors.
  - **PyCQA Bandit**: A security linter for Python code.
  - **Microsoft BinSkim**: A tool for security-oriented binary analysis.
  - **GitHub CodeQL**: A powerful semantic code analysis engine for discovering vulnerabilities.
  - **ESLint**: A pluggable JavaScript/TypeScript linting utility.
  - **SonarSource SonarCloud**: A cloud-based service for code quality and security scanning. [Report](https://sonarcloud.io/summary/new_code?id=JocysCom_VsAiCompanion)
  - **Microsoft Antimalware**: A tool to scan and mitigate against malware threats.
  - **Bridgecrew Checkov**: A static analysis tool for security policy compliance in infrastructure as code.
  - **Azure TemplateAnalyzer**: Analyzes infrastructure as code templates.
  - **Accurics Terrascan**: A static code analyzer for Terraform to detect compliance and security violations.

**[Secret Scanning](https://github.com/JocysCom/VsAiCompanion/security/secret-scanning)**: Detects secrets accidentally pushed to the repository.

### Licensing

The source code is licensed under MPL-2.0 (Mozilla Public License 2.0), permitting corporations to integrate and enhance this application with proprietary code, without the requirement to disclose their modifications. Specifically, this license facilitates the use of the application for purposes such as enabling AI to access corporate resources or to automate task creation for AI, while still keeping any proprietary additions private.

This tool acts as an intermediary, facilitating data exchange between the user and the AI. Consequently, the licensing of this tool itself does not directly impact the rights to the code or content generated by the AI. Instead, the generated content is subject to the license of the AI model used (e.g., GPT-3/4, Davinci...). Typically, the initiator of the AI-generated code process is granted copyright. Both OpenAI and Microsoft offer a legal safeguard, known as a "Copyright Commitment," to protect customers who face lawsuits for copyright infringement related to content generated by the companies' AI systems.

### Wiki
- [Home - Rethinking AI Integration](https://github.com/JocysCom/VsAiCompanion/wiki)
- [AI mastering tips](https://github.com/JocysCom/VsAiCompanion/wiki/Tips)
- [How-To examples](https://github.com/JocysCom/VsAiCompanion/blob/main/HOWTO.md)

## Screenshots
Visual Studio Extension: Open the extension in Visual Studio:

<img alt="Extension Menu" src="Documents/Images/JocysComVsAiCompanion_ExtensionMenu.png" width="414" height="118">

Standalone Application: Quickly access tasks from the notification icon in the tray:

<img alt="Tray Notification" src="Documents/Images/JocysComVsAiCompanion_TrayIcon.png" width="248" height="186">

Code - Smart Search: The AI can help you find the specific location of code features in the project or solution.

<img alt="Options" src="Documents/Images/JocysComVsAiCompanion_VisualStudio.png" width="600" height="338">

Select Errors or Warnings reported by Visual Studio and ask AI to fix it:

<img alt="Options" src="Documents/Images/JocysComVsAiCompanion_Task_Fix_SelectedIssue.png" width="600" height="338">

Ask AI to fix the exception by either copying and pasting the exception info as a message or clicking the [Send] button when Visual Studio throws it during debugging:

<img alt="Options" src="Documents/Images/JocysComVsAiCompanion_Task_Fix_Exception.png" width="600" height="473">

Application Options:

<img alt="Options" src="Documents/Images/JocysComVsAiCompanion_Options.png" width="600" height="600">

Task and template settings are saved in separate files for easy exchange and sharing:  
```C:\Users\<UserName>\AppData\Roaming\Jocys.com\VS AI Companion\```

<img alt="Templates" src="Documents/Images/JocysComVsAiCompanion_SettingFiles.png"  width="600" height="153">

Tasks:

<img alt="Tasks" src="Documents/Images/JocysComVsAiCompanion_Tasks.png" width="600" height="600">

Various Templates:

<img alt="Templates" src="Documents/Images/JocysComVsAiCompanion_Templates.png" width="600" height="600">

AI Avatar:

<img alt="AI Avatar" src="Documents/Images/JocysComVsAiCompanion_Options_AiAvatar.png" width="600" height="600">

Template: Code - Document

<img alt="Code Document" src="Documents/Images/JocysComVsAiCompanion_Task_CodeDocument.png" width="600" height="338">

Template: Custom - Historical Events

<img alt="Historical Events" src="Documents/Images/JocysComVsAiCompanion_Task_HistoricalEvents.png"  width="600" height="600">

Template: Translate - English to Klingon:

<img alt="Translate" src="Documents/Images/JocysComVsAiCompanion_Task_Translate.png"  width="600" height="600">

Fine-Tuning: Create Custom Model

<img alt="Translate" src="Documents/Images/JocysComVsAiCompanion_FineTuning.png"  width="600" height="600">

Fine-Tuning: Create Assistant (Virtual Employee)

<img alt="Translate" src="Documents/Images/JocysComVsAiCompanion_FineTuning_Assistant.png"  width="600" height="416">

Plugins: Allow AI to run applications and scripts on your machine.

<img alt="Plugins" src="Documents/Images/JocysComVsAiCompanion_Plugins.png"  width="600" height="600">

Plugins: Ask AI about solution, changed files or the code.

<img alt="Plugins" src="Documents/Images/JocysComVsAiCompanion_Plugins_Chat.png"  width="600" height="680">

