using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Hompus.VideoInputDevices
{
	[ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface ICreateDevEnum
	{
		[PreserveSig]
		int CreateClassEnumerator([In] ref Guid deviceClass, [Out] out IEnumMoniker enumMoniker, [In] int flags);
	}
}
