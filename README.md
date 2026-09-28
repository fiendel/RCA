# RCA

**RCA** is an **experimental reversible mathematical transformation** being developed as a potential cryptographic primitive for **RemoteControlC#**.

It combines nonlinear polynomial transformations, triangular reversible mappings, generated round constants, and Cauchy-based diffusion into a fully reversible construction over a finite field.

> [!WARNING]
>
> ### Experimental / Alpha
>
> RCA has **not** been proven cryptographically secure and has not undergone professional cryptanalysis.
>
> Passing the included tests does **not** constitute a security guarantee.
>
> RCA is a mathematical research project, not a replacement for established cryptographic standards.

---

## What is RCA?

RCA works on a two-element state:

```text
(x, y)
```

over a finite field:

```text
F_Q
```

where `Q` is a prime modulus.

At a high level, each transformation combines:

```text
          ┌──────────────────────┐
          │   Polynomial Layer   │
          └──────────┬───────────┘
                     │
                     ▼
          ┌──────────────────────┐
          │ Triangular Transform │
          └──────────┬───────────┘
                     │
                     ▼
          ┌──────────────────────┐
          │  Cauchy Diffusion    │
          └──────────┬───────────┘
                     │
                     ▼
               Next Round
```

The construction is explicitly reversible.

For valid parameters:

```text
RcaInverse(RcaForward(x, y)) = (x, y)
```

using RCA's normalized modular representation.

The important distinction is:

```text
reversible ≠ secure
```

RCA is currently investigating whether the construction has useful cryptographic properties.

---

# Project Goals

RCA has two primary goals:

1. Provide a precise and reproducible implementation of the mathematical construction.
2. Investigate its mathematical and cryptographic properties through systematic analysis and public cryptanalysis.

The construction is intentionally developed as an independent mathematical experiment rather than as a wrapper around an established cipher.

The project therefore emphasizes:

* explicit mathematics;
* reproducibility;
* reversible components;
* reduced-field experimentation;
* deterministic test cases;
* structural analysis;
* public cryptanalysis.

---

# Mathematical Domain

RCA operates over the finite field:

```text
F_Q
```

where `Q` must be prime.

All arithmetic is performed modulo `Q` and normalized to:

```text
0 ≤ result < Q
```

Conceptually:

```text
QMod(a) = a mod Q
```

Because `Q` is prime, every non-zero element has a modular inverse.

This property is required by the Cauchy diffusion layer.

The implementation uses:

```text
System.Numerics.BigInteger
```

which allows the same mathematical construction to be explored using both large runtime parameters and deliberately small fields.

---

# Parameters

## Default Configuration

The current implementation uses:

| Parameter |                                                                            Value |
| --------- | -------------------------------------------------------------------------------: |
| `S`       | `105838779746977706534567425713943043587340851698368009336013493163745832440967` |
| `P`       |  `66318991444146036142144795907406834002613589887754326532857488346747551061993` |
| `Q`       |  `81069498142629847296323192818110672837011592537063629126965579807735251115349` |
| `Rounds`  |                                                                              `8` |

`Q` is required to be prime.

`P` and `S` are normalized modulo `Q` and must satisfy the restrictions required by the diffusion layer.

The default constructor additionally requires the seed to be exactly **256 bits**.

---

# Reduced-Field Configurations

RCA provides a reduced-field configuration for mathematical experimentation:

```text
RCA.CreateReduced(seed, rounds, p, s, q)
```

This allows the same construction to operate over small prime fields such as:

```text
Q = 17
```

Reduced configurations are useful for:

* exhaustive testing;
* bijection verification;
* manually inspectable examples;
* reduced-round experiments;
* structural analysis;
* mathematical experimentation.

A small field is a **research configuration**, not an equivalent representation of the security level of the 256-bit configuration.

---

# Nonlinear Polynomial Layer

RCA currently uses two different nonlinear polynomials.

Both are evaluated modulo `Q`.

## Poly1 — Degree 5

```math
P_1(z)=z^5+Pz^3+Sz^2+(P+S)z+(PS+1)\pmod Q
```

The implementation derives:

```text
z² = z · z
z³ = z² · z
z⁵ = z³ · z²
```

and evaluates:

```text
Poly1(z) =
    z⁵
  + P·z³
  + S·z²
  + (P + S)·z
  + (P·S + 1)
```

modulo `Q`.

---

## Poly2 — Degree 7

```math
P_2(z)=z^7+Sz^5+Pz^3+(S^2+1)z^2+(P+2S)z+(PS+S+1)\pmod Q
```

The implementation derives:

```text
z²
z³
z⁵
z⁷
```

through repeated multiplication.

The resulting polynomial is:

```text
Poly2(z) =
    z⁷
  + S·z⁵
  + P·z³
  + (S² + 1)·z²
  + (P + 2S)·z
  + (P·S + S + 1)
```

modulo `Q`.

The degree-5 and degree-7 layers provide the nonlinear component of the current construction.

---

# Round Constants

RCA generates deterministic round constants from:

```text
Seed
P
S
Q
```

The initial value is:

```math
v_0=(Seed+P+S)\bmod Q
```

A polynomial constant transformation is then repeatedly applied.

## ConstantStep

```math
C(v)=v^5+17v^3+31v^2+13v+29\pmod Q
```

For round index `i`, the implementation additionally computes:

```math
P_i=P^i\bmod Q
```

and:

```math
T_i=iP_i+Si^3\pmod Q
```

The next constant is:

```math
v_i=C(v_{i-1})+T_i\pmod Q
```

The implementation generates:

```text
Rounds + 2
```

constants:

```text
v₀ ... v(Rounds+1)
```

The relevant constants are then consumed by the round transformations.

---

# Triangular Transformation

The central reversible component is a **triangular transformation**.

The important idea is that each stage introduces one new value while retaining the previous value in a directly recoverable form.

This means RCA does **not** need to invert `Poly1` or `Poly2`.

That distinction is fundamental to the construction.

---

## Odd Rounds

An odd round computes:

```math
u=x+3P_1(yP)+c_1\pmod Q
```

followed by:

```math
v=y+5P_2(uS)+c_2\pmod Q
```

The conceptual flow is:

```text
       y
       │
       ▼
      y·P
       │
       ▼
     Poly1
       │
       ▼
   3 · Poly1
       │
       ├───────────────┐
       │               │
       ▼               │
   x + ... + c₁        │
       │               │
       ▼               │
       u               │
       │               │
       ▼               │
      u·S              │
       │               │
       ▼               │
     Poly2              │
       │               │
       ▼               │
   5 · Poly2            │
       │               │
       └──────► y + ... + c₂
                       │
                       ▼
                       v
```

The inverse starts from `(u, v)`:

```math
y=v-5P_2(uS)-c_2\pmod Q
```

and then:

```math
x=u-3P_1(yP)-c_1\pmod Q
```

No polynomial inversion is necessary.

The reversibility comes from the **triangular structure**.

---

# Even Rounds

Even rounds use the complementary triangular arrangement.

First:

```math
u=y+3P_2(x)+c_1\pmod Q
```

Then:

```math
v=x+5P_1(u)+c_2\pmod Q
```

The output is swapped:

```text
output = (v, u)
```

Therefore the inverse begins with:

```text
u = output.y
v = output.x
```

and recovers:

```math
x=v-5P_1(u)-c_2\pmod Q
```

followed by:

```math
y=u-3P_2(x)-c_1\pmod Q
```

Again, neither nonlinear polynomial needs to be inverted.

---

# Cauchy Diffusion

Diffusion is applied after the first round.

The current construction derives its diffusion matrix from:

```text
P
S
```

using:

```math
M=
\begin{pmatrix}
(1+P)^{-1} & (1+S)^{-1}\\
(2+P)^{-1} & (2+S)^{-1}
\end{pmatrix}
\pmod Q
```

For a state:

```text
(x, y)
```

the diffusion produces:

```math
x'=(1+P)^{-1}x+(1+S)^{-1}y\pmod Q
```

and:

```math
y'=(2+P)^{-1}x+(2+S)^{-1}y\pmod Q
```

The constructor prevents parameter values that would make a denominator zero modulo `Q`.

Therefore `P` and `S` cannot be congruent to:

```text
-1
```

or:

```text
-2
```

modulo `Q`.

They must also be distinct modulo `Q`.

---

## Diffusion Inverse

The implementation calculates the determinant from the actual matrix:

```math
det(M)=ad-bc
```

For:

```math
M=
\begin{pmatrix}
a&b\\
c&d
\end{pmatrix}
```

the inverse is:

```math
M^{-1}
=
det(M)^{-1}
\begin{pmatrix}
d&-b\\
-c&a
\end{pmatrix}
```

provided:

```text
det(M) ≠ 0 mod Q
```

The implementation explicitly calculates the determinant and its modular inverse.

There is therefore **no assumption that the diffusion determinant is 1**.

---

# Complete Round Structure

The current implementation uses:

```text
8 rounds
```

with diffusion applied only after the first round.

```text
                 INPUT
                   │
                   ▼
             ┌───────────┐
             │  ROUND 1  │
             │   ODD     │
             └─────┬─────┘
                   │
                   ▼
             ┌───────────┐
             │  CAUCHY   │
             │ DIFFUSION │
             └─────┬─────┘
                   │
                   ▼
             ┌───────────┐
             │  ROUND 2  │
             │   EVEN    │
             └─────┬─────┘
                   │
                   ▼
             ┌───────────┐
             │  ROUND 3  │
             │   ODD     │
             └─────┬─────┘
                   │
                   ▼
             ┌───────────┐
             │  ROUND 4  │
             │   EVEN    │
             └─────┬─────┘
                   │
                   ▼
             ┌───────────┐
             │  ROUND 5  │
             │   ODD     │
             └─────┬─────┘
                   │
                   ▼
             ┌───────────┐
             │  ROUND 6  │
             │   EVEN    │
             └─────┬─────┘
                   │
                   ▼
             ┌───────────┐
             │  ROUND 7  │
             │   ODD     │
             └─────┬─────┘
                   │
                   ▼
             ┌───────────┐
             │  ROUND 8  │
             │   EVEN    │
             └─────┬─────┘
                   │
                   ▼
                 OUTPUT
```

The inverse performs the corresponding operations in reverse order and applies the inverse diffusion before undoing round 1.

---

# Forward Transformation

The public forward operation is:

```text
RcaForward(x, y)
```

The input state is first normalized:

```text
x = x mod Q
y = y mod Q
```

The implementation then executes:

```text
Round 1
Diffusion
Round 2
...
Round 8
```

producing:

```text
(x', y')
```

---

# Inverse Transformation

The public inverse operation is:

```text
RcaInverse(x, y)
```

The inverse processes the construction backwards:

```text
Round 8
Round 7
...
Round 2
Inverse Diffusion
Round 1
```

For valid parameters and the normalized field representation:

```text
RcaInverse(RcaForward(x, y)) = (x, y)
```

and:

```text
RcaForward(RcaInverse(x, y)) = (x, y)
```

---

# Prime Generation

The implementation contains a probable-prime generator for 256-bit values.

Candidates are generated using:

```text
RandomNumberGenerator
```

and tested using a Miller-Rabin-style probable-prime test with:

```text
32 rounds
```

Small-prime trial division is performed before the probabilistic test.

The result is a:

> **probable prime**

rather than a mathematical proof of primality.

---

# Random Parameters

The implementation provides:

```text
CreateRandom()
```

for generating a random 256-bit seed.

It also provides:

```text
CreateRandomPrime256()
```

for generating a 256-bit probable prime.

The current runtime configuration uses the fixed `P`, `Q`, and `S` values documented above.

The constructor architecture nevertheless supports:

* supplied parameters;
* independently supplied seeds;
* reduced-field configurations;
* experimental parameter sets.

---

# Testing

The project contains a standalone diagnostic test project:

```text
RCA.Tests
```

The tests examine properties including:

* parameter validity;
* prime-modulus validation;
* constant generation;
* constant ranges;
* polynomial output ranges;
* triangular reversibility;
* diffusion reversibility;
* complete RCA reversibility;
* deterministic behaviour;
* different-input behaviour;
* reduced-field behaviour;
* reference compatibility where applicable.

A passing test means:

> the implementation passed the property that was tested.

It does **not** mean:

> RCA is cryptographically secure.

---

# Cryptanalytic Diagnostics

RCA is being subjected to increasingly demanding analysis, including:

| Analysis              | Purpose                                          |
| --------------------- | ------------------------------------------------ |
| Bijection             | Verify one-to-one behaviour                      |
| Avalanche             | Measure output sensitivity                       |
| Differential          | Search for non-random input/output differences   |
| Linear                | Search for exploitable linear relationships      |
| Walsh                 | Examine Boolean correlation structure            |
| Reduced-round         | Study individual round counts                    |
| Structural            | Search for algebraic or architectural weaknesses |
| Parameter sensitivity | Examine dependence on `P`, `S`, and `Q`          |
| Seed sensitivity      | Examine dependence on the seed                   |
| Statistical tests     | Search for unusual distributions                 |

These experiments are **diagnostic tools**, not security proofs.

In particular:

```text
reversibility ≠ cryptographic security
```

```text
avalanche ≠ cryptographic security
```

```text
statistical uniformity ≠ cryptographic security
```

A reproducible structural or cryptanalytic weakness is substantially more meaningful than simply observing a desirable statistical result.

---

# Experimental Security Status

RCA currently makes **no claim** that it is:

* cryptographically secure;
* resistant to known or future attacks;
* suitable for protecting sensitive information;
* equivalent to a standardized cryptographic primitive;
* production-ready cryptography;
* secure merely because its current tests pass.

The construction is intended to remain open to independent analysis.

Areas of interest include:

* distinguishers;
* differential attacks;
* linear attacks;
* algebraic attacks;
* invariants;
* reduced-round attacks;
* structural shortcuts;
* unexpected parameter relationships;
* state-recovery shortcuts;
* seed-recovery attacks;
* attacks substantially below generic brute-force complexity.

The central research question is simple:

> **Can the construction be broken?**

A reproducible attack is valuable research information, even if it completely invalidates the current design.

---

# Public Cryptanalysis

The mathematical construction is intentionally documented openly.

The objective is not to hide the design.

The objective is to discover whether the design contains weaknesses that have not yet been identified.

Independent researchers are encouraged to:

1. Reproduce the implementation.
2. Verify the mathematical specification.
3. Test reduced configurations.
4. Analyze individual layers.
5. Analyze reduced-round versions.
6. Analyze the complete construction.
7. Publish reproducible weaknesses or attacks.

Until substantially more analysis has been performed, RCA should be regarded as an:

> **open cryptanalytic experiment**

---

# RemoteControlC# Integration

RCA is developed as an independent library so that its mathematical construction can be tested separately from the RemoteControlC# application.

The intended architecture is:

```text
┌─────────────────────┐
│   RemoteControlC#   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│        RCA          │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Mathematical        │
│ Transformation      │
└─────────────────────┘
```

RCA is responsible for the mathematical transformation itself.

Higher-level security concerns remain separate, including:

* authentication;
* key management;
* session establishment;
* replay protection;
* protocol framing;
* message integrity;
* transport security.

Using RCA alone does not automatically provide these properties.

---

# Dependencies

The mathematical implementation currently relies on standard .NET functionality:

```text
System.Numerics.BigInteger
System.Security.Cryptography.RandomNumberGenerator
```

No external cryptographic library is required for the RCA mathematical construction.

---

# Target Framework

```text
.NET 10
```

---

# Repository Structure

The intended structure is:

```text
RCA/
│
├── RCA/
│   ├── RCA.cs
│   └── ...
│
├── RCA.Tests/
│   └── Program.cs
│
├── RCA.sln
└── README.md
```

The exact filenames may evolve as the project develops.

---

# Development Philosophy

RCA follows a few simple principles:

* Keep the mathematics explicit.
* Separate mathematical properties from cryptographic claims.
* Preserve deterministic behaviour where required.
* Use `BigInteger` for arbitrary-size integer arithmetic.
* Support reduced fields for mathematical experimentation.
* Maintain reproducible test cases.
* Test reversible components independently.
* Analyse individual layers and the complete construction.
* Keep the mathematical implementation separate from RemoteControlC# integration.
* Treat the construction as experimental until independently analysed.
* Prefer reproducible mathematical evidence over unsupported security claims.

---

# License

License information will be added when the repository licensing decision is finalized.

---

<div align="center">

**RCA**

*An experimental reversible mathematical construction.*

```text
Build → Measure → Analyse → Try to Break → Improve
```

**Experimental. Reversible. Open to analysis.**

</div>

