using System.Drawing;
using System.Windows.Forms;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class ContextHelpEventArgs : HelpEventArgs
  {
    private DataConnectionDialogContext _context;

    public ContextHelpEventArgs(DataConnectionDialogContext context, Point mousePos)
      : base(mousePos)
    {
      this._context = context;
    }

    public DataConnectionDialogContext Context => this._context;
  }
}
