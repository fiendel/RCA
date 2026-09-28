using System.Numerics;
using System.Security.Cryptography;

namespace RCA;

public class RCA
{
    // Default 256-bit RCA parameters.
    public static readonly BigInteger S = BigInteger.Parse(
        "105838779746977706534567425713943043587340851698368009336013493163745832440967");

    public static readonly BigInteger P = BigInteger.Parse(
        "66318991444146036142144795907406834002613589887754326532857488346747551061993");

    public static readonly BigInteger Q = BigInteger.Parse(
        "81069498142629847296323192818110672837011592537063629126965579807735251115349");

    private const int PrimeTestRounds = 32;

    private static readonly int[] SmallPrimes =
    [
        2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47
    ];

    private readonly BigInteger[] _constants;

    // Instance parameters allow the exact same implementation
    // to be exercised over reduced fields such as F17.
    private readonly BigInteger _p;
    private readonly BigInteger _s;
    private readonly BigInteger _q;

    public BigInteger Seed { get; }
    public int Rounds { get; }

    public RCA(BigInteger seed, int rounds = 8)
        : this(seed, rounds, P, S, Q, require256BitSeed: true)
    {
    }

    // Reduced-field constructor.
    // Existing calls are unaffected; this is for mathematical audits.
    public RCA(
        BigInteger seed,
        int rounds,
        BigInteger p,
        BigInteger s,
        BigInteger q)
        : this(seed, rounds, p, s, q, require256BitSeed: false)
    {
    }

    private RCA(
        BigInteger seed,
        int rounds,
        BigInteger p,
        BigInteger s,
        BigInteger q,
        bool require256BitSeed)
    {
        if (q <= 1)
            throw new ArgumentOutOfRangeException(nameof(q));

        if (!IsPrime(q))
            throw new ArgumentException("Modulus must be prime.", nameof(q));

        if (p < 0 || p >= q)
            p = Mod(p, q);

        if (s < 0 || s >= q)
            s = Mod(s, q);

        if (require256BitSeed)
        {
            if (seed < 0 || seed.GetBitLength() != 256)
                throw new ArgumentException(
                    "Seed must be exactly 256-bit.",
                    nameof(seed));
        }
        else
        {
            seed = Mod(seed, q);
        }

        if (rounds < 1)
            throw new ArgumentOutOfRangeException(
                nameof(rounds),
                "Rounds must be at least 1.");

        ValidateCauchyParameters(p, s, q);

        _p = p;
        _s = s;
        _q = q;

        Seed = Mod(seed, _q);
        Rounds = rounds;

        _constants = MakeConstants();
    }

    public static RCA CreateRandom(int rounds = 8) =>
        new(GenerateRandom256(), rounds);

    public static RCA CreateReduced(
        BigInteger seed,
        int rounds,
        BigInteger p,
        BigInteger s,
        BigInteger q) =>
        new(seed, rounds, p, s, q);

    public static bool IsPrime(BigInteger value) =>
        IsProbablePrime(value);

    public static BigInteger CreateRandomPrime256() =>
        GeneratePrime256();

    private static BigInteger GenerateRandom256()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);

        bytes[31] |= 0x80;
        bytes[0] |= 0x01;

        return new BigInteger(bytes, isUnsigned: true);
    }

    private static BigInteger GeneratePrime256()
    {
        while (true)
        {
            BigInteger candidate = GenerateRandom256();

            if (IsProbablePrime(candidate))
                return candidate;
        }
    }

    private static bool IsProbablePrime(BigInteger n)
    {
        if (n < 2)
            return false;

        foreach (int prime in SmallPrimes)
        {
            if (n == prime)
                return true;

            if (n % prime == 0)
                return false;
        }

        if (n.IsEven)
            return false;

        BigInteger d = n - 1;
        int s = 0;

        while (d.IsEven)
        {
            d >>= 1;
            s++;
        }

        for (int i = 0; i < PrimeTestRounds; i++)
        {
            BigInteger a = RandomBelow(n - 3) + 2;
            BigInteger x = BigInteger.ModPow(a, d, n);

            if (x == 1 || x == n - 1)
                continue;

            bool passed = false;

            for (int r = 1; r < s; r++)
            {
                x = BigInteger.ModPow(x, 2, n);

                if (x == n - 1)
                {
                    passed = true;
                    break;
                }

                if (x == 1)
                    break;
            }

            if (!passed)
                return false;
        }

        return true;
    }

    private static BigInteger RandomBelow(BigInteger maximum)
    {
        if (maximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        int byteCount = maximum.GetByteCount(isUnsigned: true);
        byte[] bytes = new byte[byteCount];

        while (true)
        {
            RandomNumberGenerator.Fill(bytes);

            BigInteger value = new(bytes, isUnsigned: true);

            if (value < maximum)
                return value;
        }
    }

    public static int BitLength(BigInteger value) =>
        (int)value.GetBitLength();

    public static BigInteger Mod(
        BigInteger value,
        BigInteger modulus)
    {
        if (modulus <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(modulus),
                "Modulus must be positive.");

        BigInteger result = value % modulus;

        return result < 0
            ? result + modulus
            : result;
    }

    private BigInteger QMod(BigInteger value) =>
        Mod(value, _q);

    private static void ValidateCauchyParameters(
        BigInteger p,
        BigInteger s,
        BigInteger q)
    {
        p = Mod(p, q);
        s = Mod(s, q);

        if (p == Mod(-1, q) || p == Mod(-2, q))
            throw new ArgumentException(
                "P must not be congruent to -1 or -2 modulo Q.",
                nameof(p));

        if (s == Mod(-1, q) || s == Mod(-2, q))
            throw new ArgumentException(
                "S must not be congruent to -1 or -2 modulo Q.",
                nameof(s));

        if (p == s)
            throw new ArgumentException(
                "P and S must be distinct modulo Q.");
    }

    public BigInteger Poly1(BigInteger z)
    {
        BigInteger a = _p;
        BigInteger b = _s;

        BigInteger z2 = QMod(z * z);
        BigInteger z3 = QMod(z2 * z);
        BigInteger z5 = QMod(z3 * z2);

        return QMod(
            z5
            + a * z3
            + b * z2
            + (a + b) * z
            + (a * b + 1));
    }

    // Hardened degree-7 polynomial:
    //
    // P2(z) =
    // z^7
    // + S z^5
    // + P z^3
    // + (S^2 + 1) z^2
    // + (P + 2S) z
    // + (PS + S + 1)
    //
    public BigInteger Poly2(BigInteger z)
    {
        BigInteger a = _p;
        BigInteger b = _s;

        BigInteger z2 = QMod(z * z);
        BigInteger z3 = QMod(z2 * z);
        BigInteger z5 = QMod(z3 * z2);
        BigInteger z7 = QMod(z5 * z2);

        return QMod(
            z7
            + b * z5
            + a * z3
            + (b * b + 1) * z2
            + (a + 2 * b) * z
            + (a * b + b + 1));
    }

    public BigInteger ConstantStep(BigInteger v)
    {
        BigInteger v2 = QMod(v * v);
        BigInteger v3 = QMod(v2 * v);
        BigInteger v5 = QMod(v3 * v2);

        return QMod(
            v5
            + 17 * v3
            + 31 * v2
            + 13 * v
            + 29);
    }

    private BigInteger[] MakeConstants()
    {
        BigInteger[] constants = new BigInteger[Rounds + 2];

        BigInteger v = QMod(Seed + _p + _s);
        constants[0] = v;

        for (int i = 1; i <= Rounds + 1; i++)
        {
            BigInteger pi = BigInteger.ModPow(_p, i, _q);

            BigInteger roundTerm = QMod(
                pi * i
                + _s * i * i * i);

            v = QMod(ConstantStep(v) + roundTerm);
            constants[i] = v;
        }

        return constants;
    }

    // Hardened odd triangular layer:
    //
    // u = x + 3 P1(yP) + c1
    // v = y + 5 P2(uS) + c2
    //
    public (BigInteger x, BigInteger y) TriForward(
        BigInteger x,
        BigInteger y,
        BigInteger c1,
        BigInteger c2)
    {
        BigInteger yMasked = QMod(y * _p);

        BigInteger u = QMod(
            x
            + 3 * Poly1(yMasked)
            + c1);

        BigInteger uMasked = QMod(u * _s);

        BigInteger v = QMod(
            y
            + 5 * Poly2(uMasked)
            + c2);

        return (u, v);
    }

    public (BigInteger x, BigInteger y) TriInverse(
        BigInteger u,
        BigInteger v,
        BigInteger c1,
        BigInteger c2)
    {
        BigInteger uMasked = QMod(u * _s);

        BigInteger y = QMod(
            v
            - 5 * Poly2(uMasked)
            - c2);

        BigInteger yMasked = QMod(y * _p);

        BigInteger x = QMod(
            u
            - 3 * Poly1(yMasked)
            - c1);

        return (x, y);
    }

    // Cauchy MDS matrix:
    //
    // [ (1+P)^-1   (1+S)^-1 ]
    // [ (2+P)^-1   (2+S)^-1 ]
    //
    // r and s are retained in the method signature so existing
    // callers do not break. They represent the Cauchy parameters.
    public (BigInteger x, BigInteger y) DiffusionForward(
        BigInteger x,
        BigInteger y,
        BigInteger r,
        BigInteger s)
    {
        BigInteger a = QMod(1 + r);
        BigInteger b = QMod(1 + s);
        BigInteger c = QMod(2 + r);
        BigInteger d = QMod(2 + s);

        BigInteger aInv = ModInverse(a, _q);
        BigInteger bInv = ModInverse(b, _q);
        BigInteger cInv = ModInverse(c, _q);
        BigInteger dInv = ModInverse(d, _q);

        BigInteger xOut = QMod(
            aInv * x
            + bInv * y);

        BigInteger yOut = QMod(
            cInv * x
            + dInv * y);

        return (xOut, yOut);
    }

    public (BigInteger x, BigInteger y) DiffusionInverse(
        BigInteger x,
        BigInteger y,
        BigInteger r,
        BigInteger s)
    {
        BigInteger a = ModInverse(QMod(1 + r), _q);
        BigInteger b = ModInverse(QMod(1 + s), _q);
        BigInteger c = ModInverse(QMod(2 + r), _q);
        BigInteger d = ModInverse(QMod(2 + s), _q);

        BigInteger det = QMod(a * d - b * c);

        BigInteger detInv = ModInverse(det, _q);

        BigInteger originalX = QMod(
            detInv * (d * x - b * y));

        BigInteger originalY = QMod(
            detInv * (-c * x + a * y));

        return (originalX, originalY);
    }

    // Modular inverse using the extended Euclidean algorithm.
    private static BigInteger ModInverse(
        BigInteger value,
        BigInteger modulus)
    {
        value = Mod(value, modulus);

        if (value == 0)
            throw new ArgumentException(
                "Value has no modular inverse.",
                nameof(value));

        BigInteger oldR = value;
        BigInteger r = modulus;

        BigInteger oldT = 1;
        BigInteger t = 0;

        while (r != 0)
        {
            BigInteger quotient = oldR / r;

            (oldR, r) = (r, oldR - quotient * r);
            (oldT, t) = (t, oldT - quotient * t);
        }

        if (oldR != 1)
            throw new ArithmeticException(
                "Value is not invertible modulo the field modulus.");

        return Mod(oldT, modulus);
    }

    public (BigInteger x, BigInteger y) RoundForward(
        BigInteger x,
        BigInteger y,
        int round,
        BigInteger rDiff,
        BigInteger sDiff)
    {
        ValidateRound(round);

        BigInteger u;
        BigInteger v;

        if ((round & 1) != 0)
        {
            (u, v) = TriForward(
                x,
                y,
                _constants[round - 1],
                _constants[round]);
        }
        else
        {
            // Even triangular layer:
            //
            // u = y + 3 P2(x) + c1
            // v = x + 5 P1(u) + c2
            // output = (v, u)

            u = QMod(
                y
                + 3 * Poly2(x)
                + _constants[round - 1]);

            v = QMod(
                x
                + 5 * Poly1(u)
                + _constants[round]);

            (u, v) = (v, u);
        }

        // Cauchy diffusion is applied after the first round.
        if (round == 1)
            (u, v) = DiffusionForward(
                u,
                v,
                rDiff,
                sDiff);

        return (u, v);
    }

    public (BigInteger x, BigInteger y) RoundInverse(
        BigInteger x,
        BigInteger y,
        int round,
        BigInteger rDiff,
        BigInteger sDiff)
    {
        ValidateRound(round);

        if (round == 1)
        {
            (x, y) = DiffusionInverse(
                x,
                y,
                rDiff,
                sDiff);
        }

        if ((round & 1) != 0)
        {
            return TriInverse(
                x,
                y,
                _constants[round - 1],
                _constants[round]);
        }

        // Forward:
        //
        // u = y + 3 P2(x) + c1
        // v = x + 5 P1(u) + c2
        // output = (v, u)
        //
        // At this point:
        // x = output.x = v
        // y = output.y = u

        BigInteger u = y;
        BigInteger v = x;

        BigInteger originalX = QMod(
            v
            - 5 * Poly1(u)
            - _constants[round]);

        BigInteger originalY = QMod(
            u
            - 3 * Poly2(originalX)
            - _constants[round - 1]);

        return (originalX, originalY);
    }

    public (BigInteger x, BigInteger y) RcaForward(
        BigInteger x,
        BigInteger y)
    {
        // Keep the existing internal call structure.
        // The two arguments now represent the Cauchy parameters P,S.
        BigInteger rDiff = _p;
        BigInteger sDiff = _s;

        x = QMod(x);
        y = QMod(y);

        for (int round = 1; round <= Rounds; round++)
            (x, y) = RoundForward(
                x,
                y,
                round,
                rDiff,
                sDiff);

        return (x, y);
    }

    public (BigInteger x, BigInteger y) RcaInverse(
        BigInteger x,
        BigInteger y)
    {
        BigInteger rDiff = _p;
        BigInteger sDiff = _s;

        x = QMod(x);
        y = QMod(y);

        for (int round = Rounds; round >= 1; round--)
            (x, y) = RoundInverse(
                x,
                y,
                round,
                rDiff,
                sDiff);

        return (x, y);
    }

    private void ValidateRound(int round)
    {
        if (round < 1 || round > Rounds)
            throw new ArgumentOutOfRangeException(
                nameof(round),
                $"Round must be between 1 and {Rounds}.");
    }
}
