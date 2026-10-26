# Security Policy

## Supported Versions

Only the latest published release of [`FixedWidthParser.NET`](https://www.nuget.org/packages/FixedWidthParser.NET)
receives security fixes. Older releases are not patched retroactively; upgrade to the latest version
before reporting an issue. The native library (`FixedWidthParser.Native`), the `fixedwidth` Python
package and the C++ header are built from the same source and reported the same way.

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Report vulnerabilities privately via
[GitHub Private Vulnerability Reporting](https://github.com/GabrielMarquezMatte/FixedWidthParser/security/advisories/new),
or by email to **gabrielandremarquez.matte@gmail.com**.

Include, where possible:
- A minimal reproduction: the model and the input that triggers it
- The reading or writing path involved (file or stream, sync, async or parallel, generated or runtime,
  or the native library through C, C++ or Python)
- The impact you observed (crash, excessive memory/CPU, incorrect data, etc.)
- The affected version(s)

You should receive an initial response within 5 business days. If the report is
confirmed, a fix will be prepared and released before any public disclosure.
