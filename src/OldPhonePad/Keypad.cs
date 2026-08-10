namespace IronSoftware.OldPhonePad;

/// <summary>
/// The keypad layout: the single place where the mapping between a numeric key and
/// the characters it produces is defined.
/// </summary>
/// <remarks>
/// This type is intentionally <see langword="internal"/>. Customers decode input through
/// <see cref="OldPhonePadConverter"/> and never need to know how keys are mapped.
/// Should a second layout ever be required, this class is the seam to change.
/// </remarks>
internal static class Keypad
{
    /// <summary>
    /// Characters produced by each key, indexed by the key's numeric value.
    /// </summary>
    /// <remarks>
    /// Taken from the keypad shown in the challenge specification, including <c>0</c> for a
    /// space and <c>1</c> for punctuation. Every key produces at least one character, so any
    /// digit is valid input.
    /// </remarks>
    private static readonly string[] CharactersByKey =
    [
        " ",     // 0 - space
        "&'(",   // 1
        "ABC",   // 2
        "DEF",   // 3
        "GHI",   // 4
        "JKL",   // 5
        "MNO",   // 6
        "PQRS",  // 7
        "TUV",   // 8
        "WXYZ",  // 9
    ];

    /// <summary>Determines whether <paramref name="character"/> is a numeric key.</summary>
    internal static bool IsKey(char character) => character is >= '0' and <= '9';

    /// <summary>
    /// Returns the characters produced by the given key, in press order.
    /// </summary>
    /// <param name="key">A numeric key. Callers must have verified this with <see cref="IsKey"/>.</param>
    internal static string CharactersFor(char key) => CharactersByKey[key - '0'];
}
