#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public interface IDataConnectionUIControl
  {
    void Initialize(IDataConnectionProperties connectionProperties);

    void LoadProperties();
  }
}
