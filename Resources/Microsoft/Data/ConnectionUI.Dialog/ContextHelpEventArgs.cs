using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Microsoft.Data.ConnectionUI
{
#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

	public class ContextHelpEventArgs : HelpEventArgs
	{
		private DataConnectionDialogContext _context;

		public ContextHelpEventArgs(DataConnectionDialogContext context, Point mousePos)
		  : base(mousePos)
		{
			_context = context;
		}

		public DataConnectionDialogContext Context => _context;
	}
}
