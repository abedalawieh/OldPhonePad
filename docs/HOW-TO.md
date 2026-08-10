# How to use Old Phone Pad

A guide for developers evaluating the library. Every example is copy-pasteable, and the build, test,
package, library and API commands below were run against this repository to confirm their output.

- [What it does](#what-it-does)
- [Prerequisites](#prerequisites)
- [Use the library from C#](#use-the-library-from-c)
- [Keypad syntax](#keypad-syntax)
- [Run the REST API demo](#run-the-rest-api-demo)
- [Call the endpoint](#call-the-endpoint)
- [Interactive API documentation](#interactive-api-documentation)
- [How errors are reported](#how-errors-are-reported)
- [Assumptions worth knowing](#assumptions-worth-knowing)
- [Where to look next](#where-to-look-next)

## What it does

It decodes the multi-tap keypad input of an old mobile phone into the text it represents.

```csharp
OldPhonePadConverter.Convert("4433555 555666#");   // "HELLO"
```

The keypad logic is a self-contained library with no dependencies. The REST API in this repository
is a demonstration of wrapping it — your application does not need it.

## Prerequisites

The [.NET 10 SDK](https://dotnet.microsoft.com/download). Check with:

```bash
dotnet --version
```

Then get the code and confirm it builds:

```bash
git clone https://github.com/abedalawieh/OldPhonePad.git
cd OldPhonePad
dotnet build
dotnet test
```

## Use the library from C#

Reference the project directly:

```bash
dotnet add <your-project> reference src/OldPhonePad/OldPhonePad.csproj
```

Or build the NuGet package and reference that:

```bash
dotnet pack src/OldPhonePad/OldPhonePad.csproj -c Release -o ./artifacts
```

Then call it:

```csharp
using IronSoftware.OldPhonePad;

Console.WriteLine(OldPhonePadConverter.Convert("33#"));                  // E
Console.WriteLine(OldPhonePadConverter.Convert("227*#"));                // B
Console.WriteLine(OldPhonePadConverter.Convert("4433555 555666#"));      // HELLO
Console.WriteLine(OldPhonePadConverter.Convert("8 88777444666*664#"));   // TURING
```

Invalid input throws rather than guessing, so handle it where you accept it from a user:

```csharp
try
{
    string text = OldPhonePadConverter.Convert(userInput);
}
catch (ArgumentException ex)
{
    // ex.Message names the offending character and its position, for example:
    // "Unsupported character 'A' at position 1. Valid input consists of the digits 0-9, ..."
    Console.Error.WriteLine(ex.Message);
}
```

The type is stateless and thread-safe, and the static method needs nothing constructed or
configured.

If your application uses dependency injection, register the converter by its contract instead and
inject it. It is stateless, so a singleton is safe:

```csharp
builder.Services.AddSingleton<IOldPhonePadConverter, OldPhonePadConverter>();
```

```csharp
public sealed class MessageService(IOldPhonePadConverter converter)
{
    public string Decode(string keyPresses) => converter.Convert(keyPresses);
}
```

Both routes run the same code, so pick whichever suits your application.

If you have existing code written against the original challenge signature, that also works and runs
the same implementation:

```csharp
OldPhonePadConverter.OldPhonePad("4433555 555666#");   // "HELLO"
```

## Keypad syntax

| You send | It means | Example |
| --- | --- | --- |
| `2`–`9` | Press a key. Press again for the next letter; presses cycle. | `2`→`A`, `22`→`B`, `222`→`C`, `2222`→`A` |
| `0` | A space character | `20 2` → `A A` |
| `1` | Punctuation | `1`→`&`, `11`→`'`, `111`→`(` |
| space | Pause, so two characters on the same key can be typed | `22 2` → `BA` |
| `*` | Backspace | `227*` → `B` |
| `#` | Send. Must be the last character. | `33#` → `E` |

Full key map:

| Key | Characters | | Key | Characters |
| --- | --- | --- | --- | --- |
| `0` | *space* | | `5` | J K L |
| `1` | & ' ( | | `6` | M N O |
| `2` | A B C | | `7` | P Q R S |
| `3` | D E F | | `8` | T U V |
| `4` | G H I | | `9` | W X Y Z |

This is the keypad pictured in the challenge specification.

## Run the REST API demo

```bash
dotnet run --project src/OldPhonePad.Api
```

```
Now listening on: http://localhost:5092
```

## Call the endpoint

`POST /api/keypad/decode`

```bash
curl -X POST http://localhost:5092/api/keypad/decode \
  -H 'Content-Type: application/json' \
  -d '{"input":"4433555 555666#"}'
```

Request:

```json
{ "input": "4433555 555666#" }
```

Response — `200 OK`:

```json
{ "output": "HELLO" }
```

All four challenge examples, verbatim:

```bash
curl -s -X POST http://localhost:5092/api/keypad/decode -H 'Content-Type: application/json' -d '{"input":"33#"}'
# {"output":"E"}

curl -s -X POST http://localhost:5092/api/keypad/decode -H 'Content-Type: application/json' -d '{"input":"227*#"}'
# {"output":"B"}

curl -s -X POST http://localhost:5092/api/keypad/decode -H 'Content-Type: application/json' -d '{"input":"4433555 555666#"}'
# {"output":"HELLO"}

curl -s -X POST http://localhost:5092/api/keypad/decode -H 'Content-Type: application/json' -d '{"input":"8 88777444666*664#"}'
# {"output":"TURING"}
```

> On Windows `cmd` use double quotes and escape the inner ones:
> `curl -X POST http://localhost:5092/api/keypad/decode -H "Content-Type: application/json" -d "{\"input\":\"33#\"}"`

## Interactive API documentation

With the API running, open:

**<http://localhost:5092/scalar/v1>**

It documents the endpoint, its request and response schemas, the example payload and every status
code it can return, and gives you a ready-to-copy `curl` command. A built-in API client lets you
send requests from the page itself. The raw OpenAPI document is at
[http://localhost:5092/openapi/v1.json](http://localhost:5092/openapi/v1.json) if you would rather
generate a client.

## How errors are reported

Every failure is a standard [`ProblemDetails`](https://www.rfc-editor.org/rfc/rfc9457) document, so
you can parse them all the same way. Errors explain what was actually wrong.

**A character that is not on the keypad** — `400`:

```bash
curl -s -X POST http://localhost:5092/api/keypad/decode \
  -H 'Content-Type: application/json' -d '{"input":"2A#"}'
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid keypad input",
  "status": 400,
  "detail": "Unsupported character 'A' at position 1. Valid input consists of the digits 0-9, a space for a pause, '*' for backspace and a trailing '#' to send.",
  "traceId": "00-1059ecd8c2be02b3e6e73e2a489ecff4-ffab1f50a1f60793-00"
}
```

**Missing the send key** — `400`, `"Input must be terminated with the send key '#'."`

**Content after the send key** — `400`, `"The send key '#' must be the final character..."`

**`input` not supplied** — `400`, naming the property:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "input": ["The 'input' property is required."] }
}
```

Summary:

| What happened | Status |
| --- | --- |
| Decoded successfully | `200` |
| Keypad input the library rejects | `400` |
| `input` missing, or longer than 1024 characters | `400` |
| Request body not readable as JSON | `400` |
| `Content-Type` other than `application/json` | `415` |
| Unknown route, or wrong HTTP method | `404` / `405` |
| Something unforeseen on the server | `500`, with no internal detail |

Every response carries a `traceId` you can quote to support.

## Assumptions worth knowing

The keypad above, and the rule that presses cycle, both come from the original specification.
What it leaves open is rejected rather than guessed at, so you always know what you are getting:

- **The message must end with `#`**, and `#` cannot appear anywhere else. One call decodes one
  complete message; `"33#999"` is rejected rather than silently truncated.
- **Only a plain space is a pause.** Tabs and line breaks are rejected.
- **Backspace with nothing typed does nothing**, exactly as on the handset.
- **Key `1` produces `&`, `'`, `(` in that order.** The keypad shows the three characters but not
  their press order; this is the order they are printed in.

## Where to look next

| You want | Go to |
| --- | --- |
| Architecture, design decisions, complexity | [../README.md](../README.md) |
| The full list of assumptions and why | [README — Assumptions](../README.md#assumptions) |
| Behaviour expressed as tests | `tests/OldPhonePad.Tests/` |
| How the API wraps the library | `src/OldPhonePad.Api/Endpoints/KeypadEndpoints.cs` |
| How AI was used on this project | [../AI-PROMPT.md](../AI-PROMPT.md) |
