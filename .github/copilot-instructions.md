# GitHub Copilot Instructions

## Project Overview

This project is called **Jocys.com VS AI Companion**. It is a tool designed to answer questions, analyze project files, and enhance productivity. The tool can function as a standalone portable application or as a Visual Studio extension. It supports .NET Framework 4.8 and .NET 8.

## Key Features
- **AI Integration**: Utilizes OpenAI API (GPT-3/4, Davinci) and can run on local machines, on-premises, or on Azure Cloud.
- **Visual Studio Extension**: Can be installed and updated via Visual Studio Extension Manager.
- **Advanced Prompt Templates**: Allows creation of advanced prompt templates that include data from various sources such as Clipboard, Selection, Active Document, etc.
- **Data Safety**: Ensures data submitted via the OpenAI API is not used for model training unless explicitly shared by the user.
- **Security**: Utilizes a comprehensive suite of security scan tools to ensure the codebase is secure.

## Workflow
1. **Development Environment**: Visual Studio 2022.
2. **Target Frameworks**: .NET Framework 4.8, .NET 8.
3. **Source Control**: GitHub.
4. **Continuous Integration**: GitHub Actions.
5. **Package Management**: NuGet for .NET packages.

## Preferred Tools
- **Visual Studio 2022**: Primary IDE for development.
- **GitHub**: For source control and project management.
- **GitHub Actions**: For continuous integration and deployment.
- **NuGet**: For managing .NET packages.
- **OpenAI API**: For AI functionalities.

## Project Specifics
- **Solution Structure**:
  - **Engine**: Core logic and AI integration.
  - **Plugins**: Additional functionalities and extensions.
  - **Resources**: Static resources and assets.
  - **Data**: Data handling and management.
- **Key Dependencies**:
  - `Azure.AI.OpenAI`
  - `Azure.Identity`
  - `Microsoft.CognitiveServices.Speech`
  - `Microsoft.Graph`
  - `System.Data.OleDb`
  - `System.Data.Odbc`
  - `System.Drawing.Common`
  - `System.Windows.Forms`
- **Security Measures**:
  - `Dependabot` for dependency vulnerability detection.
  - `Code Scanning` with tools like Microsoft BinSkim, GitHub CodeQL, and SonarSource SonarCloud.
  - `Secret Scanning` to detect any secrets accidentally pushed to the repository.

## Coding Standards
- **C# Version**: 7.3
- **Code Style**: Follow .NET coding conventions.
- **Documentation**: XML comments for public APIs.
- **Testing**: Unit tests using xUnit or NUnit.

## Common Tasks
- **Adding a New Feature**: Create a new branch, implement the feature, write tests, and open a pull request.
- **Fixing a Bug**: Create a new branch, fix the bug, write tests to cover the fix, and open a pull request.
- **Updating Dependencies**: Use `Dependabot` to automatically update dependencies and ensure compatibility.

## Additional Information
- **Licensing**: The source code is licensed under MPL-2.0 (Mozilla Public License 2.0).
- **Documentation**: Detailed documentation is available in the `README.md` and `wiki` section of the GitHub repository.

By providing this context, GitHub Copilot can generate more accurate and relevant responses tailored to your project's specifics.