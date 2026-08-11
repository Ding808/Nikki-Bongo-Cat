using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Json;

[StructLayout(LayoutKind.Sequential, Size = 40)]
[NativeCppClass]
internal struct Value
{
	[StructLayout(LayoutKind.Sequential, Size = 16)]
	[NativeCppClass]
	internal struct CZString
	{
		[NativeCppClass]
		[CLSCompliant(false)]
		public enum DuplicationPolicy
		{

		}

		private long _003Calignment_0020member_003E;
	}

	[StructLayout(LayoutKind.Sequential, Size = 8)]
	[NativeCppClass]
	internal struct CommentInfo
	{
		private long _003Calignment_0020member_003E;
	}

	[StructLayout(LayoutKind.Explicit, Size = 8)]
	[NativeCppClass]
	internal struct ValueHolder
	{
		[FieldOffset(0)]
		private long _003Calignment_0020member_003E;
	}

	private long _003Calignment_0020member_003E;

	[SpecialName]
	public unsafe static void _003CMarshalCopy_003E(Value* A_0, Value* A_1)
	{
		global::_003CModule_003E.Json_002EValue_002E_007Bctor_007D(A_0, A_1);
	}

	[SpecialName]
	public unsafe static void _003CMarshalDestroy_003E(Value* A_0)
	{
		global::_003CModule_003E.Json_002EValue_002E_007Bdtor_007D(A_0);
	}
}
