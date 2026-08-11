using System.Collections.Concurrent;

namespace IronSoftware.OldPhonePad.Tests;

/// <summary>
/// Promises the documentation makes to a customer, asserted rather than assumed.
/// </summary>
/// <remarks>
/// The XML documentation calls the converter stateless and thread-safe, and the README says the
/// length limit is the API's concern rather than the library's. Both are claims a customer will
/// build on, and neither was covered by a test that would notice if it stopped being true.
/// </remarks>
public class DocumentedGuaranteeTests
{
    [Fact]
    public void Convert_CalledConcurrentlyFromManyThreads_ReturnsTheSameResultEveryTime()
    {
        // The converter is registered as a singleton, so in any real web application it is
        // already being called concurrently. Any hidden shared state would show up as interleaved
        // output rather than as an exception, which is exactly the kind of fault that reaches
        // production. Distinct inputs per worker make interference visible.
        (string Input, string Expected)[] messages =
        [
            ("4433555 555666#", "HELLO"),
            ("8 88777444666*664#", "TURING"),
            ("222 2 22#", "CAB"),
            ("33#", "E"),
            ("227*#", "B"),
        ];

        ConcurrentBag<string> mismatches = [];

        Parallel.For(0, 2_000, iteration =>
        {
            (string input, string expected) = messages[iteration % messages.Length];
            string actual = OldPhonePadConverter.Convert(input);

            if (actual != expected)
            {
                mismatches.Add($"{input} produced \"{actual}\", expected \"{expected}\".");
            }
        });

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Convert_WithAVeryLongMessage_DecodesItRatherThanImposingALimit()
    {
        // The 1024 character limit belongs to the API, which bounds the work one public request
        // can ask for. A customer calling the library directly has made no such request, so the
        // library must not quietly enforce a limit of its own.
        const int keyPresses = 50_000;
        string message = new string('2', keyPresses) + "#";

        string decoded = OldPhonePadConverter.Convert(message);

        // One uninterrupted run, so it yields exactly one character however long it is. Key 2
        // carries "ABC", and 49,999 is 1 more than a multiple of 3, so the cycle lands on 'B'.
        Assert.Equal("B", decoded);
    }

    [Fact]
    public void Convert_WithManySeparateRuns_ProducesOneCharacterPerRunWithoutRecursing()
    {
        // A single forward pass, so a long message costs time but not stack. A recursive decoder
        // would fail here instead.
        const int runs = 50_000;
        string message = string.Concat(Enumerable.Repeat("2 ", runs)) + "#";

        string decoded = OldPhonePadConverter.Convert(message);

        Assert.Equal(runs, decoded.Length);
        Assert.Equal(new string('A', runs), decoded);
    }

    [Fact]
    public void TryConvert_CalledConcurrentlyWithValidAndInvalidInput_KeepsResultsAndMessagesTogether()
    {
        // Output and error message are separate out parameters, so a shared buffer between them
        // would show up as a decoded value arriving with an error message attached, or the reverse.
        ConcurrentBag<string> faults = [];

        Parallel.For(0, 2_000, iteration =>
        {
            bool valid = iteration % 2 == 0;
            string input = valid ? "33#" : "33";

            bool decoded = OldPhonePadConverter.TryConvert(input, out string? output, out string? errorMessage);

            if (decoded != valid)
            {
                faults.Add($"\"{input}\" returned {decoded}.");
            }
            else if (valid && (output != "E" || errorMessage is not null))
            {
                faults.Add($"Valid input gave output \"{output}\" and message \"{errorMessage}\".");
            }
            else if (!valid && (output is not null || string.IsNullOrWhiteSpace(errorMessage)))
            {
                faults.Add($"Invalid input gave output \"{output}\" and message \"{errorMessage}\".");
            }
        });

        Assert.Empty(faults);
    }
}
