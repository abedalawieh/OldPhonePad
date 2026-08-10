# Old Phone Pad

[![build](https://github.com/abedalawieh/OldPhonePad/actions/workflows/build.yml/badge.svg)](https://github.com/abedalawieh/OldPhonePad/actions/workflows/build.yml)

A small, reusable .NET library that decodes old mobile phone multi-tap keypad input into text, plus
an ASP.NET Core demo showing how a customer would expose it over HTTP.

```csharp
OldPhonePadConverter.Convert("4433555 555666#");   // "HELLO"
```

- **[docs/HOW-TO.md](docs/HOW-TO.md)** — the customer guide: install, call, run the API, read the errors.
- **[AI-PROMPT.md](AI-PROMPT.md)** — how AI was used on this project, and what remained my own work.

---

## The challenge

> Write an old phone key pad as a library. Assume customers want to use our lib in a REST API, so
> write a wrapper demo to show customers, with a short How-To document.

On an old phone, each key carries several letters and you pick one by pressing the key repeatedly.
The four worked examples given in the challenge are covered by dedicated acceptance tests:

| Input | Output |
| --- | --- |
| `33#` | `E` |
| `227*#` | `B` |
| `4433555 555666#` | `HELLO` |
| `8 88777444666*664#` | `TURING` |
| `222 2 22#` | `CAB` (given in the specification's prose) |

## Architecture

```
OldPhonePad.Api  ──▶  IronSoftware.OldPhonePad.DependencyInjection  ──▶  IronSoftware.OldPhonePad
(HTTP boundary)       (optional: AddOldPhonePad())                       (keypad behaviour,
                                                                          no dependencies)
```

The library is the product; the API is a demonstration of consuming it. The library references
nothing outside the .NET base class library — no ASP.NET Core, no HTTP, no DTOs, no logging, no
hosting. It is equally usable from a console app, a desktop app, a background service or another
library, and it packs as a standalone NuGet package with zero dependencies.

`AddOldPhonePad()` is shipped as a **separate, optional package**. A registration helper needs the
dependency injection abstractions, and putting them in the core package would hand that dependency
to every consumer — including the console app that has no container at all. Splitting it is the
pattern Serilog, Polly and FluentValidation use for the same reason.

Every keypad rule lives in the library, so calling it directly and calling it through the API
produce exactly the same results and exactly the same failures.

### Repository layout

```
src/
  OldPhonePad/                       the library: OldPhonePadConverter (public), Keypad (internal)
  OldPhonePad.DependencyInjection/   optional: AddOldPhonePad() for DI consumers
  OldPhonePad.Api/                   minimal API demo: contracts, one endpoint, one handler
tests/
  OldPhonePad.Tests/        106 unit tests covering keypad behaviour and registration
  OldPhonePad.Api.Tests/     30 integration tests over the real HTTP pipeline
docs/HOW-TO.md          customer guide
AI-PROMPT.md            AI usage disclosure
.github/workflows/      CI: format, build, test and pack on every push
global.json             pins the SDK to 10.0.204 so builds are reproducible
Directory.Build.props   shared target framework, nullable settings and warning policy
```

## Keypad behaviour

| Input | Meaning |
| --- | --- |
| `2`–`9` | Press a key. Repeating it cycles through that key's letters: `2`=A, `22`=B, `222`=C, `2222`=A again. |
| `0` | A space character. |
| `1` | Punctuation: `&`, `'`, `(`. |
| space | A pause. Ends the current key press run so two characters on one key can be typed: `22 2` = `BA`. |
| `*` | Backspace. Removes the last character produced. |
| `#` | Send. Ends the message, and must be the final character. |

The layout, including `0` and `1`, is taken from the keypad pictured in the challenge
specification. Cycling is the specification's own word: *"pressing a button multiple times will
cycle through the letters on it"*.

## Assumptions

The specification defines the keypad, the cycling rule and the send key. What it leaves open is
listed here. Rather than borrow behaviour from any particular handset, anything genuinely
undefined is rejected explicitly, and the reasoning is recorded.

| Case | Behaviour | Why |
| --- | --- | --- |
| `null` input | `ArgumentNullException` | A null argument is a caller bug, not data. |
| Empty input | `ArgumentException` | It has no send key, so the rule below already covers it. |
| Missing `#` | `ArgumentException` | The challenge states input is terminated by `#`. Decoding an unterminated message would invent a contract that was never granted. |
| `#` not final, e.g. `33#999` | `ArgumentException` | One call decodes one complete message. Silently discarding what follows would hide a caller's bug. |
| `#` alone | `""` | A valid, terminated message containing no key presses. |
| Any character not on the keypad | `ArgumentException` | The error names the character and its position. |
| Backspace with nothing typed | No effect | Backspace on an empty display does nothing, as on the handset. |
| Whitespace other than `U+0020` | `ArgumentException` | Only a space is the pause. Treating a stray tab or line ending as one would be a guess. |
| Input longer than 1024 characters | Rejected by the **API**, not the library | A bound on work per request for a public demo endpoint. It is an HTTP concern, so the library itself has no limit. |

### Backspace, precisely

`*` commits any key presses still pending and then removes the last character produced.

The alternative reading — that `*` discards the pending key presses — agrees with all four official
examples, so the examples cannot tell them apart. `"2 *#"` can: the pause has already committed `A`
before the backspace arrives, so discarding "pending" presses would leave `A` on screen and do
nothing at all. The committed-character rule is the only one that behaves sensibly there, and it is
covered by a dedicated test.

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). `global.json` pins version
`10.0.204`, so the build is identical on any machine with that SDK.

```bash
git clone https://github.com/abedalawieh/OldPhonePad.git
cd OldPhonePad

dotnet build                 # 0 warnings, 0 errors
dotnet test                  # 136 tests
dotnet run --project src/OldPhonePad.Api
```

Then open <http://localhost:5092/scalar/v1>.

### Using the library

```csharp
using IronSoftware.OldPhonePad;

string text = OldPhonePadConverter.Convert("4433555 555666#");   // "HELLO"
```

`OldPhonePadConverter.OldPhonePad(string)` is also available, matching the signature in the original
challenge. It delegates to `Convert` — there is one implementation, not two.

### Using the REST API

```bash
curl -X POST http://localhost:5092/api/keypad/decode \
  -H 'Content-Type: application/json' \
  -d '{"input":"4433555 555666#"}'
```

```json
{ "output": "HELLO" }
```

Full walkthrough, including error responses: **[docs/HOW-TO.md](docs/HOW-TO.md)**.

## Error handling

Invalid input is answered, never guessed at. A single `IExceptionHandler` translates the library's
exceptions into RFC 9457 `ProblemDetails`, so no endpoint contains a `try`/`catch` and every
endpoint added later behaves the same way.

| Situation | Status | Body |
| --- | --- | --- |
| Successful decode | `200` | `{ "output": "HELLO" }` |
| Invalid keypad input | `400` | `ProblemDetails`, titled *Invalid keypad input*, explaining exactly what was wrong |
| `input` missing or too long | `400` | `ValidationProblemDetails` naming the property |
| Unreadable request body | `400` | `ProblemDetails`, titled *Malformed request* |
| Wrong content type | `415` | `ProblemDetails` |
| Unknown route / wrong method | `404` / `405` | `ProblemDetails` |
| Anything unforeseen | `500` | Generic `ProblemDetails` — no message, type name or stack trace |

Errors say something useful. Sending `{"input":"2A#"}` returns:

```json
{
  "title": "Invalid keypad input",
  "status": 400,
  "detail": "Unsupported character 'A' at position 1. Valid input consists of the digits 0-9, a space for a pause, '*' for backspace and a trailing '#' to send.",
  "traceId": "00-1059ecd8c2be02b3e6e73e2a489ecff4-ffab1f50a1f60793-00"
}
```

Validation sits at the boundary that owns it: the API checks request shape and size, the library
checks keypad rules. That is not duplication — a customer using the library without the API must
still be protected, so the library validates its own input independently.

## Algorithm

A single forward pass holding two pieces of state: the key currently being pressed, and how many
times it has been pressed. A run ends when a different character is read, at which point it resolves
to one character through the keypad map.

The send key is validated up front. Once `#` is known to be the final and only terminator, the
decoding loop never has to consider it — which is why the loop has no dead branches and no
unreachable code.

- **Time: O(n)** — one pass, no backtracking. Backspace is O(1) by shortening the `StringBuilder`.
- **Space: O(m)** — one character of output per completed key press run, `m ≤ n`.

The keypad map is a single `static readonly string[]` indexed by `key - '0'`: one source of truth,
no per-call allocation, and the only place to change if another layout is ever needed.

## Testing

136 tests, all passing.

**106 unit tests** cover behaviour through the public API only — no `InternalsVisibleTo`, no testing
of private methods. The four official examples are dedicated tests because they are the challenge's
contract. Beyond those: every key, every press count, pauses, backspace in each position, and each
category of rejected input. Exception tests assert the facts the message must carry — the offending
key, character or position — but never whole sentences, so re-wording a message does not break them.

**30 integration tests** exercise the real HTTP pipeline through `WebApplicationFactory`: routing,
binding, validation, the exception handler, JSON shaping, OpenAPI. They deliberately do not repeat
the keypad matrix over HTTP; they prove the API is wired to the library and reports its failures
faithfully. Most run in Production, since that is what a deployed instance does, with a small set
in Development because ASP.NET Core reports unreadable request bodies differently there — a
difference that hid a real bug until it was tested.

## Design decisions

**The DI helper ships separately.** `AddOldPhonePad()` uses `TryAddSingleton`, so calling it twice
registers one converter and an application that has already registered its own — a decorator adding
logging or caching, say — keeps it. A library should add what is missing rather than override a
decision its consumer has already made. The extension is declared in the
`Microsoft.Extensions.DependencyInjection` namespace, the convention ASP.NET Core itself follows, so
the call needs no extra `using`.

**`IOldPhonePadConverter` for consumers, a static method for convenience.** The decoding never
varies, so the abstraction is not there to let the library swap implementations — it is there so a
consuming application can bind to a contract, resolve it from a container and substitute a
stand-in in its own tests. Callers who want none of that keep calling the static `Convert`. The
interface implementation is explicit and delegates to it, so there is exactly one algorithm
however it is reached.

**No custom exception type.** `ArgumentException` and `ArgumentNullException` already mean what is
meant here, and the API maps them centrally.

**Minimal API, not controllers.** The endpoint is one call to the library. A controller would add
ceremony without adding structure.

**Strict rather than forgiving, but only where the specification is silent.** Anything the
specification defines is implemented; every case it genuinely leaves open fails loudly. For a library a customer builds
on, a clear exception is worth more than a plausible guess, and the exception messages name the key,
character or position so a failure can be diagnosed from a log line alone.

**Deliberately not built:** authentication, a database, repositories, MediatR, CQRS, caching,
rate limiting, a state machine class, a keypad layout provider, or a factory. None of them has a
requirement behind it.

## Dependencies

The keypad library has **no runtime dependencies at all** — the packed `.nuspec` carries an empty
dependency group. The optional DI package has exactly two, and the demo API three, all
Microsoft-published or widely used, all stable:

| Package | Depends on |
| --- | --- |
| `IronSoftware.OldPhonePad` | *nothing* |
| `IronSoftware.OldPhonePad.DependencyInjection` | the core package, and `Microsoft.Extensions.DependencyInjection.Abstractions` |

The demo API's own dependencies:

| Package | Why |
| --- | --- |
| `Microsoft.AspNetCore.OpenApi` | Generates the OpenAPI document. Part of ASP.NET Core. |
| `Microsoft.OpenApi` | Pinned to `2.7.5` on purpose. ASP.NET Core 10 pulls in `2.0.0`, which carries a known high-severity advisory ([GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)); `2.7.5` is the first patched release. The pin can be removed once ASP.NET Core references a patched version itself. |
| `Scalar.AspNetCore` | Renders the OpenAPI document as an interactive page. ASP.NET Core 10 generates the document but ships no UI; Scalar consumes the built-in document directly rather than generating a second one. |

### Debugging into the library

The packages are built with [SourceLink](https://github.com/dotnet/sourcelink) and ship a `.snupkg`
symbol package, so a customer can step straight into this source from their own debugger rather
than decompiling the assembly. The built assemblies record the commit they came from.

## Known limitations

- Output is upper case only, because the keypad defines only upper case letters.
- Key `1` produces `&`, `'` and `(` in that order. The keypad image shows the three characters but
  not their press order; this is the order they are printed in.
- The demo serves the interactive documentation in every environment because demonstrating the
  library is its entire purpose. A production service would gate that.

### If this were to grow

A second keypad layout (lower case, punctuation, a different locale) is the most likely request, and
the map in `Keypad.cs` is the seam for it. That would be the point to introduce a layout abstraction
— when a second implementation actually exists, rather than in anticipation of one.

## Licence

MIT — see [LICENSE](LICENSE). Both packages carry the `MIT` licence expression, so a consumer's
licence tooling can resolve them without reading the repository.

## AI usage

AI (Claude) was used on this project as an engineering assistant and reviewer. The prompt and an
honest account of what it did and did not decide is in **[AI-PROMPT.md](AI-PROMPT.md)**.
