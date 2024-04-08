namespace Microsoft.Data.ConnectionUI
{
  public enum DataConnectionDialogContext
  {
    None = 0,
    Source = 16777216, // 0x01000000
    SourceListBox = 16777217, // 0x01000001
    SourceProviderComboBox = 16777218, // 0x01000002
    SourceOkButton = 16777219, // 0x01000003
    SourceCancelButton = 16777220, // 0x01000004
    Main = 33554432, // 0x02000000
    MainDataSourceTextBox = 34603009, // 0x02100001
    MainChangeDataSourceButton = 34603010, // 0x02100002
    MainConnectionUIControl = 35651584, // 0x02200000
    MainSqlConnectionUIControl = 35651585, // 0x02200001
    MainSqlFileConnectionUIControl = 35651586, // 0x02200002
    MainOracleConnectionUIControl = 35651587, // 0x02200003
    MainAccessConnectionUIControl = 35651588, // 0x02200004
    MainOleDBConnectionUIControl = 35651589, // 0x02200005
    MainOdbcConnectionUIControl = 35651590, // 0x02200006
    MainGenericConnectionUIControl = 36700159, // 0x022FFFFF
    MainAdvancedButton = 37748736, // 0x02400000
    MainTestConnectionButton = 41943041, // 0x02800001
    MainAcceptButton = 41943054, // 0x0280000E
    MainCancelButton = 41943055, // 0x0280000F
    Advanced = 67108864, // 0x04000000
    AdvancedPropertyGrid = 67108865, // 0x04000001
    AdvancedTextBox = 67108866, // 0x04000002
    AdvancedOkButton = 67108867, // 0x04000003
    AdvancedCancelButton = 67108868, // 0x04000004
    AddProperty = 134217728, // 0x08000000
    AddPropertyTextBox = 134217729, // 0x08000001
    AddPropertyOkButton = 134217742, // 0x0800000E
    AddPropertyCancelButton = 134217743, // 0x0800000F
  }
}
