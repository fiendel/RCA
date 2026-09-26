# RCA

RCA is an experimental reversible mathematical transformation designed to serve as the main cryptographic primitive for the **RemoteControlC#** project.

The current implementation is a C#/.NET 10 port of the **RCA 1.5 Alpha** reference implementation originally developed and tested in Maxima.

> **Status: Experimental / Alpha**
>
> RCA has not been proven cryptographically secure and has not undergone professional cryptanalysis. Passing the included tests does not constitute a security guarantee.

## Project Goals

The RCA project has two main goals:

1. Provide a precise C# implementation of the RCA mathematical construction.
2. Eventually use RCA as the principal cryptographic primitive within RemoteControlC#.

RCA is intentionally being developed as its own construction rather than as a wrapper around conventional cryptographic algorithms.

This project does **not** attempt to replace established cryptographic standards in general-purpose security applications.

## Current Version

**RCA 1.5 Alpha**

The current mathematical reference configuration uses:

* `S = 17`
* `Rounds = 8`
* `P` — prime modulus parameter
* `Q` — prime modulus parameter
* `Seed` — deterministic constant-generation seed

The original Maxima reference configuration uses 61-bit values for `P` and `Q`.

The C# runtime implementation is designed to use **256-bit parameters**.

## Architecture

The core transformation operates on a pair of integers:

```text
(x, y)
```

with arithmetic performed modulo `Q`.

The construction consists of several layers:

```text
Input
  │
  ▼
Round 1
  │
  ├── Triangular transformation
  │
  └── Diffusion
  │
  ▼
Round 2
  │
  └── Alternating triangular transformation
  │
  ▼
Round 3
  │
  └── Triangular transformation
  │
  ▼
...
  │
  ▼
Round 8
  │
  ▼
Output
```

The transformation is explicitly reversible.

For every valid state:

```text
RcaInverse(RcaForward(x, y)) = (x, y)
```

subject to the modular representation used by RCA.

## Main Components

### Polynomial Layer

RCA uses two fifth-degree polynomial transformations:

```text
Poly1(z)
Poly2(z)
```

Their coefficients depend on the RCA parameters `P`, `Q`, and `S`.

### Triangular Transformation

The triangular layer performs two sequential transformations:

```text
u = x + 3·Poly1(y) + c1
v = y + 5·Poly2(u) + c2
```

Because the second operation depends on the already transformed value `u`, the transformation can be inverted in reverse order.

### Diffusion

The diffusion layer operates on the two state values using parameters derived from the generated constants.

Its matrix has determinant:

```text
1
```

making the transformation directly invertible modulo `Q`.

### Round Structure

RCA currently uses:

```text
8 rounds
```

Odd and even rounds use different triangular arrangements.

Diffusion is applied after the first round.

### Constant Generation

Round constants are deterministically generated from:

```text
P
Q
S
Seed
```

The implementation generates:

```text
Rounds + 2
```

constants.

The final two constants are used by the diffusion layer.

## Parameters

### Reference Parameters

The original Maxima RCA 1.5 Alpha reference configuration uses:

```text
P =
2305843009213693951

Q =
2305843009213694087

S =
17

Rounds =
8
```

with the corresponding reference seed stored in the test suite.

These values are retained specifically for compatibility testing against the Maxima implementation.

### Runtime Parameters

The C# implementation currently generates:

```text
P    = random 256-bit probable prime
Q    = random 256-bit probable prime
Seed = random 256-bit value

S      = 17
Rounds = 8
```

`P` and `Q` are generated independently and are required to be different.

## Testing

The repository contains a standalone diagnostic test project:

```text
RCA.Tests
```

The test runner currently verifies:

* 256-bit parameter sizes
* `P != Q`
* primality checks
* constant count
* constant range
* polynomial output range
* diffusion reversibility
* triangular reversibility
* complete RCA reversibility
* different-input behavior
* deterministic behavior
* independent parameter generation
* exact Maxima reference vectors

Current result:

```text
FINAL RESULT: 26 passed, 0 failed
RCA TEST SUITE: PASS
```

## Maxima Compatibility

The C# implementation has been tested against the original Maxima RCA 1.5 Alpha implementation.

Five independent reference inputs currently reproduce the exact Maxima outputs:

```text
(0, 0)

(1, 1)

(123456789, 987654321)

(Q-1, Q-2)

(2^60, 2^59)
```

For example:

```text
Input:
123456789
987654321

Maxima:
351668050717095916
50542148241801508

C#:
351668050717095916
50542148241801508
```

This provides an exact implementation-level compatibility check between the Maxima reference and the C# implementation.

## Reference Diagnostics

The original Maxima RCA 1.5 Alpha diagnostic suite reports:

```text
PASS "PRIMES"
PASS "DETERMINISM"
PASS "TRIANGULAR"
PASS "DIFFUSION"
PASS "DETERMINANT"
PASS "REVERSIBILITY"
PASS "ROUND-COUNT"
PASS "ROUND-EFFECT"
```

Its reported sensitivity measurements are:

```text
AVALANCHE    60.8084577
DIFFERENTIAL 60.8524578
PARAMETER    61.124975
SEED         61.734476
```

These measurements are retained as reference diagnostics.

They should **not** be interpreted as a cryptographic security proof.

## Security Status

RCA is experimental cryptographic research.

The following statements are intentionally **not** made:

* RCA is cryptographically secure.
* RCA is resistant to known cryptanalytic attacks.
* RCA is suitable for protecting sensitive data.
* RCA is equivalent to a standardized cipher.
* The avalanche measurements prove security.
* The reversibility tests prove security.

The current goal is to make the implementation mathematically precise, deterministic where required, independently testable, and suitable for further cryptanalysis.

Public cryptanalysis and independent review are encouraged.

## Dependencies

The core implementation currently relies on standard .NET functionality, including:

* `System.Numerics.BigInteger`
* `System.Security.Cryptography.RandomNumberGenerator`

No external cryptographic library is required for the RCA implementation.

## Target Framework

```text
.NET 10
```

## Repository Structure

The intended structure is:

```text
RCA/
│
├── RCA/
│   ├── RCAMain.cs
│   └── ...
│
├── RCA.Tests/
│   └── Program.cs
│
├── RCA.sln
└── README.md
```

## Relationship With RemoteControlC#

RCA is being developed as an independent library so that the mathematical implementation can be tested and evolved separately from the RemoteControlC# application.

The intended architecture is:

```text
RemoteControlC#
       │
       ▼
      RCA
       │
       ▼
RCA mathematical transformation
```

Authentication, protocol framing, session management, replay protection, and other protocol-level mechanisms are separate concerns and will be designed above the RCA primitive.

## Development Philosophy

The project follows several principles:

* Keep the mathematical construction explicit.
* Preserve compatibility with the Maxima reference implementation.
* Avoid unnecessary dependencies.
* Use `BigInteger` rather than restricting the implementation to machine-sized integers.
* Maintain deterministic reference vectors.
* Keep mathematical changes separate from integration work.
* Treat experimental cryptography as experimental until independently analyzed.

## License

License information will be added when the repository licensing decision is finalized.
