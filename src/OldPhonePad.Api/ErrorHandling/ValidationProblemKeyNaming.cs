using System.Text.Json;

namespace OldPhonePad.Api.ErrorHandling;

/// <summary>
/// Renames validation error keys from CLR property names to the JSON names the client actually
/// sent.
/// </summary>
/// <remarks>
/// <para>
/// Data annotations report failures against the property name, so a missing <c>input</c> arrives
/// keyed as <c>Input</c> while the request body, the OpenAPI schema and every documented example
/// call it <c>input</c>. A client reading <c>errors.input</c> would find nothing.
/// </para>
/// <para>
/// Applying the serializer's own naming policy fixes the mismatch at its source rather than
/// hard-coding one property name, so a contract added later is correct without further work.
/// </para>
/// </remarks>
internal static class ValidationProblemKeyNaming
{
    /// <summary>Separates the segments of a nested member path, for example <c>Outer.Inner</c>.</summary>
    private const char PathSeparator = '.';

    /// <summary>
    /// Rewrites the keys of a validation problem, leaving every other kind of problem untouched.
    /// </summary>
    internal static void UseJsonPropertyNames(ProblemDetailsContext context)
    {
        if (context.ProblemDetails is not HttpValidationProblemDetails validation)
        {
            return;
        }

        // Materialised first: the dictionary cannot be rewritten while it is being read.
        List<KeyValuePair<string, string[]>> original = [.. validation.Errors];

        validation.Errors.Clear();

        foreach ((string member, string[] messages) in original)
        {
            // The indexer rather than Add: two CLR names could in principle converge on one JSON
            // name, and a duplicate key must not turn a validation failure into a 500.
            validation.Errors[ToJsonName(member)] = messages;
        }
    }

    /// <summary>
    /// Converts a member path to the name the serializer would produce, segment by segment.
    /// </summary>
    private static string ToJsonName(string member) =>
        member.Contains(PathSeparator, StringComparison.Ordinal)
            ? string.Join(PathSeparator, member.Split(PathSeparator).Select(ToCamelCase))
            : ToCamelCase(member);

    private static string ToCamelCase(string segment) =>
        JsonNamingPolicy.CamelCase.ConvertName(segment);
}
