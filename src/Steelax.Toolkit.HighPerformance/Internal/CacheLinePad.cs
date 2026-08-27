using System.Runtime.InteropServices;

namespace Steelax.Toolkit.HighPerformance.Internal;

/// <summary>
/// A full 64-byte padding block that forces the following field into a distinct cache line.
/// </summary>
/// <remarks>
/// Uses sequential layout (explicit layout is not allowed for types nested in a generic class);
/// <see cref="System.Runtime.InteropServices.StructLayoutAttribute.Size"/> pads the empty struct to exactly 64 bytes.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 64)]
internal struct CacheLinePad;