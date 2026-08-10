# AI usage on this project

Iron Software asked which AI tool was used and for the prompt to be published. This document is that
disclosure, and it is written to be accurate rather than flattering in either direction.

**Tool used:** [Claude](https://claude.ai) (Claude Code), as an engineering assistant and reviewer.

## How I worked

I did not ask an AI to solve the challenge and submit the result. I ran the project in phases and
kept the decisions with me:

1. **Requirements and architecture.** I asked for an analysis of the specification, the ambiguities
   and the options — explicitly with no code — and then made the calls myself.
2. **Core library.** Implemented only after I had approved the architecture, the public API and the
   behaviour for every ambiguous case.
3. **Unit tests**, then a review pass, then the REST API demo, then integration tests, then
   documentation. Each stage was reviewed before the next began.

The prompt I used to set this up is reproduced at the end of this document, condensed for
readability — the working prompt also carried the phase-by-phase review process and a written
decision contract I issued before implementation began.

## What I decided

Every behavioural and architectural decision in this repository is mine. The ones that mattered:

- **The specification is the authority, and I checked it against the source document.** This is the
  decision I am proudest of and the one I got wrong first. Working from a text transcription of the
  challenge, I ruled that the specification said nothing about keys `0` and `1` or about what
  happens when a key is over-pressed, and I rejected all three as undefined. When I went back to
  the original PDF, the keypad illustration defines `0` as a space and `1` as `&'(`, and the prompt
  itself says presses "cycle through the letters". My reasoning had been sound; my source had not
  been. I changed the implementation, the tests and this document to match the specification.
  The lesson I take from it: a requirement you have only read second-hand is not a requirement you
  have read.
- **`#` must be the final character and appear once.** `"33#999"` is rejected instead of truncated,
  because one call represents one complete message and silent truncation hides caller bugs. The
  specification guarantees a trailing `#`; it says nothing about content after one, so this is a
  documented assumption rather than a rule I was given.
- **`IOldPhonePadConverter` exists for the consumer, not the library.** My first instinct was to
  leave the converter static — decoding never varies, so an interface buys the library nothing, and
  I did not want to add an abstraction just to display a pattern. I changed my mind for a different
  reason: an application consuming this library should be able to bind to a contract and resolve it
  from a container. The static method is still there for callers who want neither.
- **The library stays free of ASP.NET Core, logging and hosting**, so it is genuinely reusable.
  When I wanted an `AddOldPhonePad()` helper, I did not put it in the library: a registration
  extension needs the dependency injection abstractions, and that would hand a dependency to every
  consumer, including those with no container. It ships as a separate optional package instead,
  which is how Serilog and Polly solve the same problem. The keypad package still has an empty
  dependency list.
- **Strict validation over lenient guessing**, wherever the specification is genuinely silent.

The guiding rule I set was: requirements first, documented assumptions second, historical handset
behaviour only where explicitly chosen and written down. Applying that rule honestly is what forced
the correction above.

## What Claude did

- Analysed the specification and produced the list of ambiguities for me to rule on.
- Argued the case for and against options — including cases where I disagreed and overruled it.
- Wrote implementation, test and documentation code to the decisions I had already made.
- Reviewed code for cohesion, naming, duplication and unnecessary abstraction.
- Identified edge cases I then chose behaviour for.
- Re-read the original specification with me and helped rework the implementation, tests and
  documentation once it was clear that three of my rulings contradicted it.
- Ran the builds, tests, analysers, formatter and the API itself, and reported the results.

Two examples of that review being worth something:

- A code analyser flagged that a private helper was throwing an exception naming a parameter it did
  not own. Rather than suppress it, we moved the over-press check to the moment the key is pressed.
  That removed a piece of state, made the append step unable to fail, and improved the error message.
- Running the demo the way the documentation tells a customer to run it revealed that a malformed
  JSON body returned `500` instead of `400` in the Development environment — the integration tests
  ran in Production and could never have caught it. It is now fixed and covered by tests that run in
  Development specifically.

## What Claude did not do

- It did not choose the behaviour for any ambiguous case.
- It did not decide the architecture, the public API or the dependencies.
- It did not decide what to commit, or write the commit history.
- It did not have the final word anywhere. Where its recommendation conflicted with my reading of
  the specification, my reading won. Where the specification itself contradicted my reading, the
  specification won — see the first decision above.

I can explain every line of this repository, every assumption in the README and the reasoning behind
every decision listed here, because I made them.

---

## The prompt

The engineering-assistant prompt used to run this project, condensed to its substance. Alongside it
I worked phase by phase, reviewing and approving each stage before the next began, and issued a
written decision contract — the rulings listed above — before any implementation was written.

````text
# Context

I am completing the C# Coding Challenge for the Software Sales Engineer position at Iron Software.

I am the engineer responsible for the solution, architecture, implementation decisions, testing
strategy, documentation, and final submission.

You are acting as my senior engineering assistant and reviewer.

Your role is to help me:
- challenge my engineering decisions
- identify edge cases
- review architecture
- review code quality
- suggest refactoring where justified
- help design comprehensive tests
- verify requirements
- review documentation
- identify bugs and weaknesses
- help me maintain a professional Git history

Do not treat this as a request to blindly generate a coding challenge solution for me.
I need to understand and be able to explain every important decision in the final repository.

# Challenge

Write an old phone key pad as a library. Assume customers want to use our lib in a REST API, so
write a wrapper demo to show customers. This demo should show a real demo app, and a short How-To
document for customers to follow.

Iron Software is looking for: production-ready code, polished implementation rather than a quick
algorithm, clear and organized structure, maintainability, debuggability, extensibility, robustness,
and professional engineering standards.

# Engineering philosophy

Apply SOLID, DRY, KISS, YAGNI, Clean Code, separation of concerns, encapsulation, high cohesion,
low coupling, defensive programming, explicit validation, clear naming, testability and modern
idiomatic C# — intelligently rather than mechanically.

However: do NOT overengineer this challenge simply to demonstrate patterns. A five-class
architecture is not automatically better than a clean two-class architecture. An interface with one
implementation is not automatically good SOLID. I want the smallest architecture that remains
production-quality, maintainable, testable and extensible. When SOLID and architectural abstraction
conflict with KISS/YAGNI, explain the trade-off and recommend the simplest defensible solution.

# Architecture boundary

The Old Phone Pad must be a genuine reusable .NET library. The REST API is only a customer
integration demonstration.

The library MUST NOT depend on ASP.NET Core, HTTP, controllers, Swagger, API DTOs or hosting
infrastructure. It must be usable from a console app, desktop app, background service, test project
or another commercial application. Treat it as if Iron Software were going to ship it as a NuGet
package.

# Requirements and assumptions

The challenge guarantees that '#' terminates input. Do not silently invent additional behaviour.

For every unspecified behaviour, identify it as an assumption before implementation: null input,
empty input, a missing '#', content after '#', unsupported characters, repeated backspaces,
backspace with no output, presses beyond the mapped character count, and what keys 0 and 1 map to.

For ambiguous requirements: identify the ambiguity, propose the most reasonable behaviour, explain
why, cover it with tests, and document the assumption. Do not quietly add behaviour.

Do not justify implementation choices solely with "real phones did this" when the challenge itself
is silent. The guiding rule is: requirements first, documented assumptions second, historical
handset behaviour only when explicitly chosen and documented.

# Testing

Tests should communicate expected behaviour. Prefer behaviour-driven names, xUnit theories for
multiple cases of the same behaviour, and Arrange/Act/Assert where it aids readability. Do not test
private methods, do not expose internals for testing, and do not duplicate implementation logic in
tests. The four official examples get dedicated acceptance tests.

If a test fails, do not simply modify the test until it passes. Determine whether the production
code is wrong, the expectation is wrong, or the requirement is ambiguous — and explain which before
changing anything.

# REST API demo

Keep the endpoint thin; business rules stay in the library. Use request/response DTOs, validation,
correct status codes, ProblemDetails, OpenAPI and an interactive UI. Implement centralized exception
handling rather than try/catch in endpoints. Expected client errors map to 4xx; unexpected failures
map to a safe 500 that leaks no stack traces or internal detail. Do not add authentication, a
database, Entity Framework, repositories, message queues, caching, MediatR, CQRS or Docker unless a
real requirement arises.

# Process

Work in phases and stop for my review between them: requirements and architecture (no code), core
implementation, unit tests, core review, REST API demo, documentation, and a final quality gate.

Before every commit: review the diff, confirm only intended files changed, run the tests and build,
check warnings, and ensure no secrets or generated artifacts are included. Do not create commits
unless I ask you to.

If implementation reveals a requirement ambiguity that materially affects behaviour, stop and
explain it rather than silently inventing behaviour.
````
