using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Json;

[StructLayout(LayoutKind.Sequential, Size = 200)]
[NativeCppClass]
[UnsafeValueType]
internal struct OurReader
{
	[NativeCppClass]
	internal enum TokenType
	{

	}

	[StructLayout(LayoutKind.Sequential, Size = 24)]
	[NativeCppClass]
	internal struct Token
	{
		private long _003Calignment_0020member_003E;
	}

	[StructLayout(LayoutKind.Sequential, Size = 64)]
	[NativeCppClass]
	[UnsafeValueType]
	internal struct ErrorInfo
	{
		private long _003Calignment_0020member_003E;
	}

	private long _003Calignment_0020member_003E;
}
