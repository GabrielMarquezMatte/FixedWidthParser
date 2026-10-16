# FixedWidthParser Style Guide

Code style rules. For build/PR/test process, see [CONTRIBUTING.md](CONTRIBUTING.md); for the shape of
the codebase, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Priorities

1. **Correctness** — the code must be right. No shortcuts that sacrifice safety or accuracy.
2. **Performance** — optimize where it matters, but never at the cost of readability.
3. **Readability** — code is read far more than it is written.

---

## Ternaries

Use a ternary only when the entire expression fits on one line and both branches are single values — no function calls, no method chains, no nesting.

```csharp
// OK: two literal values
int resolvedOverflow = writeKind == WriteKind.String ? 1 : 2;

// OK: one local each side
int byteCount = ascii ? text.Length : encodedLength;

// NOT OK: nested ternary
string result = a ? b ? "x" : "y" : "z";

// NOT OK: branches contain logic
int value = condition ? ComputeA() : ComputeB();

// NOT OK: a long condition, even with plain values
byte sign = Padding != ' ' && length > 0 && (first == (byte)'-' || first == (byte)'+') ? first : padding;

// Use an if that returns instead (see "Avoid else" below):
private static int Value(bool condition)
{
    if (condition)
    {
        return ComputeA();
    }
    return ComputeB();
}
```

When in doubt, use `if`.

---

## Avoid `else`

Whenever possible, do not write `else`. A branch that ends the method — `return`, `continue`,
`break`, `throw` — makes the `else` redundant, and the code after it reads as the main path.

```csharp
// NOT OK: two branches at the same depth
if (Alignment != Alignment.Right)
{
    content.CopyTo(slice);
    slice[length..].Fill(padding);
}
else
{
    slice[..pad].Fill(padding);
    content.CopyTo(slice[pad..]);
}

// OK: the special case returns, the other case follows unindented
if (Alignment != Alignment.Right)
{
    content.CopyTo(slice);
    slice[length..].Fill(padding);
    return;
}
slice[..pad].Fill(padding);
content.CopyTo(slice[pad..]);
```

When neither branch can end the method, extract the `if` into its own method that returns. Keep
`else` only when both branches must fall through to shared code that follows and extracting them
would split one idea across two methods.

---

## Control Flow — Prefer Early Return

Avoid wrapping the happy path in a condition. Guard and exit early; keep the main logic at the lowest indentation level.

```csharp
// NOT OK: body is nested under the guard
internal void Compact()
{
    if (Start > 0)
    {
        int len = End - Start;
        ...
    }
}

// OK: guard exits early
internal void Compact()
{
    if (Start <= 0)
    {
        return;
    }
    int len = End - Start;
    ...
}
```

Always use braces on `if`, even single-line bodies. The linter enforces this.

---

## Scope Nesting — Maximum 3 Levels

Count every block that introduces indentation: `if`, `else`, `for`, `foreach`, `while`, `using`, `try`, `switch`. A function body is level 0. Nesting beyond 3 is a signal to extract a method.

```
level 0: function body
level 1: while (true)
level 2:   if (ScanFrom < End)
level 3:     if (rel >= 0)         ← maximum allowed
level 4:       if (...)            ← extract this into a private method
```

When you hit level 4, stop and ask: what is this inner block doing? Name it and move it.

---

## Method Bodies — Always Block Body

Every method, constructor, operator and local function must use a block body with braces. Expression
bodies (`=>`) are forbidden on them, no matter how short.

```csharp
// NOT OK
private static string Foo() => Bar();

// OK
private static string Foo()
{
    return Bar();
}
```

Properties and their accessors are exempt: `public TModel Current => _current;` and
`get => field ?? Encoding.UTF8;` are both fine, as is a block body. `.editorconfig` encodes the same
split and turns IDE0027 off.

This rule exists to keep diffs readable and the line count honest. A one-liner expression body hides complexity behind a single line and makes the 70-line rule easy to game.

---

## Function Length — Maximum 70 Lines

Count every non-blank, non-comment line inside the function body (opening and closing brace included). If a function exceeds 70 lines, split it.

70 is the review target. The build enforces a looser backstop through MA0051 (90 lines / 50 statements, see `.editorconfig`), so a method between 71 and 90 lines compiles but still fails review.

Do not game the limit by collapsing logic onto single lines. The limit exists to keep methods focused, not to reward line-golf. A method that packs three operations on one line to stay under 70 is worse than one that splits them across 71 readable lines.

Common splits:

- Read one column's attribute vs. assemble the model from its columns
- Find a line boundary vs. parse the line within it
- The rare growth or error path extracted from the caller

---

## Naming

- Private fields: `_camelCase`
- Private static methods: `PascalCase`
- Local variables: `camelCase`
- Prefer full words over abbreviations (`index` over `idx`, `source` over `src`) unless the abbreviation is idiomatic in the domain (`buf`, `len`, `pos`)

### The runtime and generated paths are peers

A model is parsed and written either by the runtime path (`FixedWidthParser<T>`, `FixedWidthReader<T>`,
`FixedWidthWriter<T>`) or by the generated one (`FixedWidth.*` over `IFixedWidthModel<TSelf>`). A
capability on one gets the same name shape on the other: `FixedWidth.ReadFile<T>` mirrors
`FixedWidthReader<T>.ReadFile`, and the same options object configures both. A caller who learns one
path should be able to guess the other. When you add a member to one, grep the other and match, or add
the missing sibling in the same change.

---

## Comments

**The default is no comment.** Code is expected to explain itself through naming and execution flow.
Reaching for a comment is the signal to look at the code again first: if a reader cannot follow the
logic, that is a defect in the code, not a gap in its documentation. Rename the variable, extract the
method, flatten the branch. Fix it there, and the comment becomes unnecessary.

A comment is justified only once that has been tried and the code still cannot carry the meaning —
because the reason lives outside the code and no amount of naming can bring it in:

- A constraint imposed by a platform, a dependency, or the bytes of a file.
- An invariant a reader could silently break, where breaking it stays compiling and passes review.

```csharp
// OK: a dependency's behavior the code cannot express
// csFastFloat does not fail on trailing content; it stops and returns the prefix it parsed.

// NOT OK: describes what the code does
// Loop through each column and parse its value.

// NOT OK: the code should have said this itself
// i is the index of the '\n' that ends the line
int i = ...;          // -> name it lineFeedIndex and delete the comment
```

Never reference the task, the ticket, or the caller.

### Findings do not belong in code

Measurements, benchmark results, allocation counts, and the reasoning that picked one approach over
another are **not comments**. They go in [`docs/`](docs/README.md).

This includes the whole class of "why it is this way" content: a throughput figure that justified a
buffer size, an allocation count that motivated pooling, a design that was prototyped and measured
and then rejected. Put it in a document, with the numbers, the machine, and the input. Then the
next person can re-run it and disagree with the evidence — which a comment asserting `// 5x faster`
never lets them do.

What may stay in the code is the constraint itself, stated without its evidence:

```csharp
// OK: the ceiling, not the benchmark that found it
// ponytail: at most four decimals; anything finer takes the BCL formatter.

// NOT OK: a finding, and it rots the moment anything around it changes
// Measured 8 ns vs 46 ns for double.TryFormat, so the scaled path stays.
```

### A comment must stay true, or it is worse than no comment

Comments carry maintenance cost. Two failure modes are specifically banned:

- **Do not describe a state the code is no longer in.** When you refactor, reread the comments on what
  you touched.
- **Do not cite a file that can disappear.** Never point a comment at a plan, a spec, or a section
  marker as the authoritative explanation. Those are working artifacts: they get renamed, merged and
  deleted, and the comment then sends a reader to nothing, which is worse than silence. A committed
  document under [`docs/`](docs/README.md) is different in kind — it is permanent, and pointing at one is
  how a finding stays out of the code. The test is whether the target is expected to still exist in a
  year, not whether a link is involved.

---

## Error Handling

Throw at trust boundaries: constructor arguments, public API parameters, malformed external data. Use `ArgumentOutOfRangeException.ThrowIfNegative` and similar .NET 6+ throw helpers. Do not add defensive checks for conditions that are impossible given the internal invariants.

Match the exception to whose fault it is:

| Fault | Exception |
|---|---|
| A line that does not fit the layout | `FixedWidthParseException`, naming the line, column and raw text |
| A source that breaks a configured limit (`MaxLineLength`) | `InvalidDataException` |
| A model whose layout is invalid | `InvalidOperationException` from the runtime constructor, or a generator diagnostic |
| A value too wide for its column when writing | `InvalidOperationException` naming the column |
| A bad argument from the caller | `ArgumentException` and its subtypes |

Never answer a malformed file with `ArgumentException`/`ArgumentOutOfRangeException`: that tells the
caller they made a mistake when the input is what is broken. If file bytes can reach a .NET throw
helper, you are missing a validation step upstream.

The runtime path builds its layout once per type in a static constructor. A layout error there is
captured and rethrown from the instance constructor, never left to surface as a
`TypeInitializationException`.

---

## Untrusted Input

Files and request bodies are attacker-controlled. These rules are not optional in reader code.

- **Bound every buffer a source can grow.** A source without line breaks must hit `MaxLineLength`
  before it drives an allocation, on every reader: stream, async stream and `PipeReader`.
- **When you add a guard to one reader, apply it to every sibling reader.** The three readers split
  lines independently; a limit enforced on two of them is a hole in the third.
- **Validate before you allocate, not before you use.** Renting a bigger buffer first and discovering the
  line is too long afterwards still hands the attacker the allocation.
- **Never let a malformed value silently change semantics.** A column that does not parse fails the
  line; it never becomes a default value.
- New or changed line-splitting code needs a test that feeds the malformed shape (no line break, a
  line one byte over the limit, a `\r` at a buffer boundary). Assert the exception type and its line
  number, not merely that something threw.

---

## Pooled Buffers

`ArrayPool<T>.Shared` is used throughout the readers and the writer. Misuse here is a correctness bug,
not a performance nit.

- Every `Rent` needs a matching `Return` on **every** exit path, including thrown exceptions — a
  `try`/`finally` around the use. A dropped buffer silently shrinks the pool for the whole process.
- Never `Return` a buffer that anything still references, and never touch a buffer after returning it.
- Pooled arrays come back oversized and dirty. Bound reads by the logical length you tracked, never by
  `array.Length`.
- Buffers that live beyond one call are returned in `Dispose`/`DisposeAsync`; guard against double-return,
  since `Dispose` may be called twice.

---

## Twins

Three sets of code paths in this library must stay behaviorally identical, and nothing in the compiler
keeps them honest:

- **Runtime and generated.** `FixedWidthParser<T>`/`FixedWidthWriter<T>` and the generator's
  `TryParse`/`TryFormat` call the same `FixedWidthRuntime` helpers. Put shared behavior there, not in
  either side.
- **Stream, async stream and pipe readers.** Line semantics — the byte order mark, `\r\n`, empty
  lines, a last line without a break, `MaxLineLength` — must match across all three.
- **Sync and async writers.** `WriteMany` and `WriteManyAsync` batch the same way.

So:

- A behavior fix to one twin **must** be applied to the others in the same change. Read all of them
  before you edit any.
- Behavior shared by twins belongs in a helper none of them duplicates — the split should cover only
  the awaiting or the code generation, not the logic around it.
- Changes to a twinned path need a parity test asserting identical output, including at buffer
  boundaries (a line straddling a refill or a pipe segment) — that is exactly where implementations drift.
- A synchronous method does not take a `CancellationToken`.

---

## Tests and Benchmarks

Harness code is held to the same standard as library code, because a broken harness reports success.

- **Fail loudly, never silently.** A setup that never ran, an input that parsed to nothing — assert it.
  The dangerous case is not the harness that crashes; it is the one that measures zero work and
  publishes a number.
- **Benchmarks against another library must compare matched work, or say plainly that they do not.**
  If our side writes bytes straight to the stream while the competitor goes through a `StreamWriter`,
  say so next to the numbers. An unlabelled comparison that flatters us is a defect.
- **State the machine for every published number**, and never derive a ratio across results measured on
  different hardware. Allocation figures are deterministic and comparable across machines; timings are
  not.
- Assert real behavior — exact bytes, exact values, exception types and their line and column. A test
  that calls a method and asserts no throw documents nothing.

---

## Performance Notes

These rules apply only when a function is on a measured hot path (splitting lines, parsing or
formatting a column):

- Work on `ReadOnlySpan<byte>`; decode to `string` only for a `string` column.
- Use `ArrayPool<T>.Shared` for buffers that live beyond one stack frame, `stackalloc` for small ones.
- Avoid LINQ in hot paths; use `IndexOf` on spans, which benefits from SIMD.
- Specialize generic code over `struct` strategies (`IRecordLineParser<T>`) so the JIT devirtualizes,
  rather than over interfaces or delegates.
- Split cold paths (growth, error diagnosis, throw helpers) into `[MethodImpl(MethodImplOptions.NoInlining)]`
  methods so they do not bloat the hot caller's IL and cost it inlining.
- Optimize against a measurement, and record what it showed in [`docs/`](docs/README.md) — never in a comment.
  An optimization with no number behind it is a guess that costs readability.

Outside the hot path (building a layout, the static constructors), ordinary allocations and reflection are fine.
