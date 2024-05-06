using System;
using System.Linq;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine.Resources
{
    public static class MainResources
    {

        private static Uri _currentLoadedResourceUri;

        public static void Load(string resourceFileName)
        {
            var resourcePath = $"pack://application:,,,/{resourceFileName}";
            var newResourceUri = new Uri(resourcePath, UriKind.RelativeOrAbsolute);
            if (_currentLoadedResourceUri?.ToString().Equals(newResourceUri.ToString(), StringComparison.OrdinalIgnoreCase) ?? false)
                return;
            Unload();
            var resourceDictionary = new ResourceDictionary { Source = newResourceUri };
            Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
            _currentLoadedResourceUri = newResourceUri;
        }

        public static void Unload()
        {
            if (_currentLoadedResourceUri == null)
                return;
            var oldDictionary = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.Equals(_currentLoadedResourceUri) ?? false);
            if (oldDictionary == null)
                return;
            Application.Current.Resources.MergedDictionaries.Remove(oldDictionary);
            _currentLoadedResourceUri = null;
        }

        public static string FindResource(string key)
        {
            var resource = Application.Current.TryFindResource(key);
            if (resource == null && _currentLoadedResourceUri == null)
            {
                var assembly = typeof(MainResources).Assembly;
                var assemblyName = assembly.GetName().Name;
                Load($"{assemblyName};component/Resources/MainResources.xaml");
                resource = Application.Current.TryFindResource(key);
            }
            return resource as string;
        }

        public static string main_TasksTab_Help => FindResource(nameof(main_TasksTab_Help));
        public static string main_Region => FindResource(nameof(main_Region));
        public static string main_ServiceType => FindResource(nameof(main_ServiceType));
        public static string main_TasksTab_Name => FindResource(nameof(main_TasksTab_Name));
        public static string main__1_Assistants => FindResource(nameof(main__1_Assistants));
        public static string main__1_Source_Files => FindResource(nameof(main__1_Source_Files));
        public static string main__2_Tuning_Files => FindResource(nameof(main__2_Tuning_Files));
        public static string main__3_Remote_Files => FindResource(nameof(main__3_Remote_Files));
        public static string main__4_Tuning_Jobs => FindResource(nameof(main__4_Tuning_Jobs));
        public static string main__5_Models => FindResource(nameof(main__5_Models));
        public static string main_Add => FindResource(nameof(main_Add));
        public static string main_Add_New => FindResource(nameof(main_Add_New));
        public static string main_AI_Models => FindResource(nameof(main_AI_Models));
        public static string main_AI_Avatar => FindResource(nameof(main_AI_Avatar));
        public static string main_AI_Services => FindResource(nameof(main_AI_Services));
        public static string main_Allow_Only_One_Copy => FindResource(nameof(main_Allow_Only_One_Copy));
        public static string main_Allowed_Recipients => FindResource(nameof(main_Allowed_Recipients));
        public static string main_Allowed_Senders => FindResource(nameof(main_Allowed_Senders));
        public static string main_API_Base_URL => FindResource(nameof(main_API_Base_URL));
        public static string main_API_Key => FindResource(nameof(main_API_Key));
        public static string main_API_Organization_ID => FindResource(nameof(main_API_Organization_ID));
        public static string main_App_Always_on_Top => FindResource(nameof(main_App_Always_on_Top));
        public static string main_Approval_Template => FindResource(nameof(main_Approval_Template));
        public static string main_Approval_Type => FindResource(nameof(main_Approval_Type));
        public static string main_Attachments => FindResource(nameof(main_Attachments));
        public static string main_Auto_Generate_Title => FindResource(nameof(main_Auto_Generate_Title));
        public static string main_Auto_Markdown => FindResource(nameof(main_Auto_Markdown));
        public static string main_Automation => FindResource(nameof(main_Automation));
        public static string main_Axialis_Icon_Set_Licenses => FindResource(nameof(main_Axialis_Icon_Set_Licenses));
        public static string main_Bar_Always_on_Top => FindResource(nameof(main_Bar_Always_on_Top));
        public static string main_Browse => FindResource(nameof(main_Browse));
        public static string main_Bytes => FindResource(nameof(main_Bytes));
        public static string main_Call_function_request_denied => FindResource(nameof(main_Call_function_request_denied));
        public static string main_Cancel => FindResource(nameof(main_Cancel));
        public static string main_Cancel_Job => FindResource(nameof(main_Cancel_Job));
        public static string main_Changes => FindResource(nameof(main_Changes));
        public static string main_Chat => FindResource(nameof(main_Chat));
        public static string main_Chat_Instructions => FindResource(nameof(main_Chat_Instructions));
        public static string main_Comment => FindResource(nameof(main_Comment));
        public static string main_Company_Product => FindResource(nameof(main_Company_Product));
        public static string main_Context_Options => FindResource(nameof(main_Context_Options));
        public static string main_Copy => FindResource(nameof(main_Copy));
        public static string main_Copy_Id_File_Name => FindResource(nameof(main_Copy_Id_File_Name));
        public static string main_Copy_with_Headers => FindResource(nameof(main_Copy_with_Headers));
        public static string main_Create => FindResource(nameof(main_Create));
        public static string main_Create_a_Job_and_Model_from_File => FindResource(nameof(main_Create_a_Job_and_Model_from_File));
        public static string main_Create_an_Assistant_Task_from_Model => FindResource(nameof(main_Create_an_Assistant_Task_from_Model));
        public static string main_Created => FindResource(nameof(main_Created));
        public static string main_Creativity => FindResource(nameof(main_Creativity));
        public static string main_Data => FindResource(nameof(main_Data));
        public static string main_Data_Folder => FindResource(nameof(main_Data_Folder));
        public static string main_Database => FindResource(nameof(main_Database));
        public static string main_Date => FindResource(nameof(main_Date));
        public static string main_Default_AI_Model => FindResource(nameof(main_Default_AI_Model));
        public static string main_Delete => FindResource(nameof(main_Delete));
        public static string main_Description => FindResource(nameof(main_Description));
        public static string main_Details => FindResource(nameof(main_Details));
        public static string main_Disable_All => FindResource(nameof(main_Disable_All));
        public static string main_Document => FindResource(nameof(main_Document));
        public static string main_Edit => FindResource(nameof(main_Edit));
        public static string main_Email_Address => FindResource(nameof(main_Email_Address));
        public static string main_Email_Name => FindResource(nameof(main_Email_Name));
        public static string main_Embedding_Default_Instructions => FindResource(nameof(main_Embedding_Default_Instructions));
        public static string main_Embeddings_Feature_Description => FindResource(nameof(main_Embeddings_Feature_Description));
        public static string main_Enable_All => FindResource(nameof(main_Enable_All));
        public static string main_Enable_Low_Risk => FindResource(nameof(main_Enable_Low_Risk));
        public static string main_Enable_Medium_Risk => FindResource(nameof(main_Enable_Medium_Risk));
        public static string main_Enable_spell_check_for_the_chat_textbox => FindResource(nameof(main_Enable_spell_check_for_the_chat_textbox));
        public static string main_Enabled => FindResource(nameof(main_Enabled));
        public static string main_Environment => FindResource(nameof(main_Environment));
        public static string main_Exclude_Patterns => FindResource(nameof(main_Exclude_Patterns));
        public static string main_File_Name => FindResource(nameof(main_File_Name));
        public static string main_Files => FindResource(nameof(main_Files));
        public static string main_Format_Code => FindResource(nameof(main_Format_Code));
        public static string main_GitHub_Project => FindResource(nameof(main_GitHub_Project));
        public static string main_Help => FindResource(nameof(main_Help));
        public static string main_IconExperience_License => FindResource(nameof(main_IconExperience_License));
        public static string main_Id => FindResource(nameof(main_Id));
        public static string main_IMAP_Host => FindResource(nameof(main_IMAP_Host));
        public static string main_Include_Patterns => FindResource(nameof(main_Include_Patterns));
        public static string main_Instructions => FindResource(nameof(main_Instructions));
        public static string main_Is_Azure_OpenAI => FindResource(nameof(main_Is_Azure_OpenAI));
        public static string main_Is_Default_AI_Service => FindResource(nameof(main_Is_Default_AI_Service));
        public static string main_Jocys_com_Label => FindResource(nameof(main_Jocys_com_Label));
        public static string main_Key => FindResource(nameof(main_Key));
        public static string main_License => FindResource(nameof(main_License));
        public static string main_List_Description => FindResource(nameof(main_List_Description));
        public static string main_List_Instructions => FindResource(nameof(main_List_Instructions));
        public static string main_List_IsEnabled => FindResource(nameof(main_List_IsEnabled));
        public static string main_List_IsReadOnly => FindResource(nameof(main_List_IsReadOnly));
        public static string main_Lists_Feature_Description => FindResource(nameof(main_Lists_Feature_Description));
        public static string main_Log => FindResource(nameof(main_Log));
        public static string main_MaiAccounts_Feature_Description => FindResource(nameof(main_MaiAccounts_Feature_Description));
        public static string main_Mail => FindResource(nameof(main_Mail));
        public static string main_Mail_Accounts => FindResource(nameof(main_Mail_Accounts));
        public static string main_Main => FindResource(nameof(main_Main));
        public static string main_Market_Place => FindResource(nameof(main_Market_Place));
        public static string main_Max_Input_Tokens => FindResource(nameof(main_Max_Input_Tokens));
        public static string main_Maximum_Risk_Level => FindResource(nameof(main_Maximum_Risk_Level));
        public static string main_Message => FindResource(nameof(main_Message));
        public static string main_Minimize_on_Close => FindResource(nameof(main_Minimize_on_Close));
        public static string main_Minimize_to_Tray => FindResource(nameof(main_Minimize_to_Tray));
        public static string main_Model => FindResource(nameof(main_Model));
        public static string main_Model_Filter => FindResource(nameof(main_Model_Filter));
        public static string main_ModelRefreshButton_ToolTip => FindResource(nameof(main_ModelRefreshButton_ToolTip));
        public static string main_Multimedia => FindResource(nameof(main_Multimedia));
        public static string main_Name => FindResource(nameof(main_Name));
        public static string main_Object => FindResource(nameof(main_Object));
        public static string main_Open => FindResource(nameof(main_Open));
        public static string main_Open_File => FindResource(nameof(main_Open_File));
        public static string main_Other => FindResource(nameof(main_Other));
        public static string main_Owner => FindResource(nameof(main_Owner));
        public static string main_Password => FindResource(nameof(main_Password));
        public static string main_Play => FindResource(nameof(main_Play));
        public static string main_Paste => FindResource(nameof(main_Paste));
        public static string main_Path => FindResource(nameof(main_Path));
        public static string main_Personalize_the_chat => FindResource(nameof(main_Personalize_the_chat));
        public static string main_Preview => FindResource(nameof(main_Preview));
        public static string main_Preview_Mode_Message => FindResource(nameof(main_Preview_Mode_Message));
        public static string main_Prompting => FindResource(nameof(main_Prompting));
        public static string main_Purpose => FindResource(nameof(main_Purpose));
        public static string main_ReadOnly => FindResource(nameof(main_ReadOnly));
        public static string main_Refresh => FindResource(nameof(main_Refresh));
        public static string main_Remove_on_Complete => FindResource(nameof(main_Remove_on_Complete));
        public static string main_Reset_ServicesAndModels => FindResource(nameof(main_Reset_ServicesAndModels));
        public static string main_Reset_Lists => FindResource(nameof(main_Reset_Lists));
        public static string main_Reset_Application_Settings => FindResource(nameof(main_Reset_Application_Settings));
        public static string main_Reset_Embeddings => FindResource(nameof(main_Reset_Embeddings));
        public static string main_Reset_Prompting_Settings => FindResource(nameof(main_Reset_Prompting_Settings));
        public static string main_Reset_Settings => FindResource(nameof(main_Reset_Settings));
        public static string main_Reset_Templates => FindResource(nameof(main_Reset_Templates));
        public static string main_Reset_to_Default => FindResource(nameof(main_Reset_to_Default));
        public static string main_Reset_UI => FindResource(nameof(main_Reset_UI));
        public static string main_Reset_UI_Settings => FindResource(nameof(main_Reset_UI_Settings));
        public static string main_Reset_UI_Settings_ToolTip => FindResource(nameof(main_Reset_UI_Settings_ToolTip));
        public static string main_Response_Streaming => FindResource(nameof(main_Response_Streaming));
        public static string main_Response_Timeout => FindResource(nameof(main_Response_Timeout));
        public static string main_Search => FindResource(nameof(main_Search));
        public static string main_Search_the_list => FindResource(nameof(main_Search_the_list));
        public static string main_Security => FindResource(nameof(main_Security));
        public static string main_Selection => FindResource(nameof(main_Selection));
        public static string main_Send_as_System_Messages => FindResource(nameof(main_Send_as_System_Messages));
        public static string main_Send_Chat_History => FindResource(nameof(main_Send_Chat_History));
        public static string main_Send_on_Create => FindResource(nameof(main_Send_on_Create));
        public static string main_Sending_Message => FindResource(nameof(main_Sending_Message));
        public static string main_Server_Host => FindResource(nameof(main_Server_Host));
        public static string main_Server_IMAP_Port => FindResource(nameof(main_Server_IMAP_Port));
        public static string main_Server_Port => FindResource(nameof(main_Server_Port));
        public static string main_Server_SMTP_Port => FindResource(nameof(main_Server_SMTP_Port));
        public static string main_Service => FindResource(nameof(main_Service));
        public static string main_Settings_Folder => FindResource(nameof(main_Settings_Folder));
        public static string main_Show_Documents_Attached_Warning => FindResource(nameof(main_Show_Documents_Attached_Warning));
        public static string main_Show_Icon_in_Toolbar => FindResource(nameof(main_Show_Icon_in_Toolbar));
        public static string main_Show_Instructions => FindResource(nameof(main_Show_Instructions));
        public static string main_Show_Prompting => FindResource(nameof(main_Show_Prompting));
        public static string main_Show_Sensitive_Data_Warning => FindResource(nameof(main_Show_Sensitive_Data_Warning));
        public static string main_SMTP_Host => FindResource(nameof(main_SMTP_Host));
        public static string main_Source => FindResource(nameof(main_Source));
        public static string main_Spell_Check => FindResource(nameof(main_Spell_Check));
        public static string main_Standalone_Program => FindResource(nameof(main_Standalone_Program));
        public static string main_Start_with_Windows => FindResource(nameof(main_Start_with_Windows));
        public static string main_Stop => FindResource(nameof(main_Stop));
        public static string main_Status => FindResource(nameof(main_Status));
        public static string main_System_Message => FindResource(nameof(main_System_Message));
        public static string main_Target => FindResource(nameof(main_Target));
        public static string main_Task => FindResource(nameof(main_Task));
        public static string main_Task_Path => FindResource(nameof(main_Task_Path));
        public static string main_Templates => FindResource(nameof(main_Templates));
        public static string main_Test => FindResource(nameof(main_Test));
        public static string main_Test_Authentication => FindResource(nameof(main_Test_Authentication));
        public static string main_TextBox_Drop_Files_Instructions => FindResource(nameof(main_TextBox_Drop_Files_Instructions));
        public static string main_Title_for_Attached_Context => FindResource(nameof(main_Title_for_Attached_Context));
        public static string main_Trust_Server_Certificate => FindResource(nameof(main_Trust_Server_Certificate));
        public static string main_Upload => FindResource(nameof(main_Upload));
        public static string main_Use_Enter_to_send_the_message => FindResource(nameof(main_Use_Enter_to_send_the_message));
        public static string main_Use_Macros => FindResource(nameof(main_Use_Macros));
        public static string main_Use_Maximum_Context => FindResource(nameof(main_Use_Maximum_Context));
        public static string main_Username => FindResource(nameof(main_Username));
        public static string main_Validate => FindResource(nameof(main_Validate));
        public static string main_Validate_Digital_Signature => FindResource(nameof(main_Validate_Digital_Signature));
        public static string main_Validate_DKIM => FindResource(nameof(main_Validate_DKIM));
        public static string main_Validate_Recipients => FindResource(nameof(main_Validate_Recipients));
        public static string main_Validate_Senders => FindResource(nameof(main_Validate_Senders));
        public static string main_Value => FindResource(nameof(main_Value));
        public static string main_Vision => FindResource(nameof(main_Vision));
        public static string main_Visual_Studio_Extension_Options => FindResource(nameof(main_Visual_Studio_Extension_Options));
        public static string main_Voice => FindResource(nameof(main_Voice));
        public static string main_VsExtensionFeatureMessage => FindResource(nameof(main_VsExtensionFeatureMessage));
        public static string main_VsExtensionVersionMessage => FindResource(nameof(main_VsExtensionVersionMessage));
    }
}