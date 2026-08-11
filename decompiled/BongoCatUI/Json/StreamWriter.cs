using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Json;

[StructLayout(LayoutKind.Sequential, Size = 16)]
[NativeCppClass]
internal static struct StreamWriter
{
	[StructLayout(LayoutKind.Sequential, Size = 8)]
	[CLSCompliant(false)]
	[NativeCppClass]
	public static struct Factory
	{
		private long _003Calignment_0020member_003E;
	}

	private long _003Calignment_0020member_003E;
}
