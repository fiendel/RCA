using System.Numerics;
using System.Security.Cryptography;

namespace RCA;

public class RCAMain
{
    public BigInteger P { get; }
    public BigInteger Q { get; }
    public BigInteger Seed { get; }
    public const int S = 17;
    public const int Rounds = 8;

    public RCAMain(BigInteger p, BigInteger q, BigInteger seed)
    {
        P = p;
        Q = q;
        Seed = seed;
    }

    public static RCAMain CreateRandom()
    {
        BigInteger p = GeneratePrime256();
        BigInteger q;

        do
            q = GeneratePrime256();
        while (q == p);

        BigInteger seed = GenerateRandom256();

        return new RCAMain(p, q, seed);
    }

    private static BigInteger GenerateRandom256()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        bytes[31] |= 0x80;
        return new BigInteger(bytes, isUnsigned: true);
    }

    private static BigInteger GeneratePrime256()
    {
        while (true)
        {
            BigInteger candidate = GenerateRandom256();

            if (candidate.IsEven)
                candidate++;

            if (IsProbablePrime(candidate))
                return candidate;
        }
    }

    private static bool IsProbablePrime(BigInteger n)
    {
        if (n < 2)
            return false;

        if (n == 2 || n == 3)
            return true;

        if (n.IsEven)
            return false;

        BigInteger d = n - 1;
        int s = 0;

        while (d.IsEven)
        {
            d /= 2;
            s++;
        }

        for (int i = 0; i < 32; i++)
        {
            BigInteger a = RandomBelow(n - 3) + 2;
            BigInteger x = BigInteger.ModPow(a, d, n);

            if (x == 1 || x == n - 1)
                continue;

            bool probablyPrime = false;

            for (int r = 1; r < s; r++)
            {
                x = BigInteger.ModPow(x, 2, n);

                if (x == n - 1)
                {
                    probablyPrime = true;
                    break;
                }
            }

            if (!probablyPrime)
                return false;
        }

        return true;
    }

    private static BigInteger RandomBelow(BigInteger maximum)
    {
        byte[] bytes = maximum.ToByteArray(isUnsigned: true);
        BigInteger value;

        do
        {
            RandomNumberGenerator.Fill(bytes);
            value = new BigInteger(bytes, isUnsigned: true);
        }
        while (value >= maximum);

        return value;
    }

    public static int BitLength(BigInteger value)
    {
        return (int)value.GetBitLength();
    }

    public static BigInteger Mod(BigInteger value, BigInteger modulus)
    {
        BigInteger result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    public BigInteger Poly1(BigInteger z)
    {
        BigInteger a = Mod(P, Q);
        BigInteger b = Mod(S, Q);

        return Mod(
            BigInteger.Pow(z, 5) +
            a * BigInteger.Pow(z, 3) +
            b * BigInteger.Pow(z, 2) +
            (a + b) * z +
            (a * b + 1),
            Q
        );
    }

    public BigInteger Poly2(BigInteger z)
    {
        BigInteger a = Mod(P, Q);
        BigInteger b = Mod(S, Q);

        return Mod(
            BigInteger.Pow(z, 5) +
            a * BigInteger.Pow(z, 3) +
            (b * b + 1) * BigInteger.Pow(z, 2) +
            (a + 2 * b) * z +
            (a * b + b + 1),
            Q
        );
    }

    public BigInteger ConstantStep(BigInteger v)
    {
        return Mod(
            BigInteger.Pow(v, 5) +
            17 * BigInteger.Pow(v, 3) +
            31 * BigInteger.Pow(v, 2) +
            13 * v +
            29,
            Q
        );
    }
    public List<BigInteger> MakeConstants()
    {
        List<BigInteger> constants = [];

        BigInteger v = Mod(Seed + P + S, Q);
        constants.Add(v);

        for (int i = 1; i <= Rounds + 1; i++)
        {
            v = Mod(ConstantStep(v) + P * i + S * i * i, Q);
            constants.Add(v);
        }

        return constants;
    }

    public (BigInteger U, BigInteger V) TriForward(BigInteger x, BigInteger y, BigInteger c1, BigInteger c2)
    {
        BigInteger u = Mod(x + 3 * Poly1(y) + c1, Q);
        BigInteger v = Mod(y + 5 * Poly2(u) + c2, Q);

        return (u, v);
    }

    public (BigInteger X, BigInteger Y) TriInverse(BigInteger u, BigInteger v, BigInteger c1, BigInteger c2)
    {
        BigInteger y = Mod(v - 5 * Poly2(u) - c2, Q);
        BigInteger x = Mod(u - 3 * Poly1(y) - c1, Q);

        return (x, y);
    }
    public (BigInteger A, BigInteger B) DiffusionForward(BigInteger x, BigInteger y, BigInteger r, BigInteger s)
    {
        BigInteger a = Mod(x + r * y, Q);
        BigInteger b = Mod(s * x + (r * s + 1) * y, Q);

        return (a, b);
    }
    public (BigInteger X, BigInteger Y) DiffusionInverse(BigInteger a, BigInteger b, BigInteger r, BigInteger s)
    {
        BigInteger x = Mod((r * s + 1) * a - r * b, Q);
        BigInteger y = Mod(-s * a + b, Q);

        return (x, y);
    }

    public (BigInteger X, BigInteger Y) RoundForward(BigInteger x, BigInteger y, int round, List<BigInteger> constants, BigInteger rDiff, BigInteger sDiff)
    {
        BigInteger u, v;

        if (round % 2 != 0)
        {
            (u, v) = TriForward(x, y, constants[round - 1], constants[round]);
        }
        else
        {
            u = Mod(y + 5 * Poly1(x) + constants[round - 1], Q);
            v = Mod(x + 3 * Poly2(u) + constants[round], Q);
            (u, v) = (v, u);
        }

        if (round == 1)
            (u, v) = DiffusionForward(u, v, rDiff, sDiff);

        return (u, v);
    }

    public (BigInteger X, BigInteger Y) RoundInverse(BigInteger x, BigInteger y, int round, List<BigInteger> constants, BigInteger rDiff, BigInteger sDiff)
    {
        if (round == 1)
            (x, y) = DiffusionInverse(x, y, rDiff, sDiff);

        if (round % 2 != 0)
            return TriInverse(x, y, constants[round - 1], constants[round]);

        BigInteger originalX = Mod(x - 3 * Poly2(y) - constants[round], Q);
        BigInteger originalY = Mod(y - 5 * Poly1(originalX) - constants[round - 1], Q);

        return (originalX, originalY);
    }

    public (BigInteger X, BigInteger Y) RcaForward(BigInteger x, BigInteger y)
    {
        List<BigInteger> constants = MakeConstants();
        BigInteger rDiff = constants[^2];
        BigInteger sDiff = constants[^1];

        (BigInteger X, BigInteger Y) state = (Mod(x, Q), Mod(y, Q));

        for (int round = 1; round <= Rounds; round++)
            state = RoundForward(state.X, state.Y, round, constants, rDiff, sDiff);

        return state;
    }

    public (BigInteger X, BigInteger Y) RcaInverse(BigInteger x, BigInteger y)
    {
        List<BigInteger> constants = MakeConstants();
        BigInteger rDiff = constants[^2];
        BigInteger sDiff = constants[^1];

        (BigInteger X, BigInteger Y) state = (Mod(x, Q), Mod(y, Q));

        for (int round = Rounds; round >= 1; round--)
            state = RoundInverse(state.X, state.Y, round, constants, rDiff, sDiff);

        return state;
    }

    public static bool IsPrime(BigInteger value)
    {
        return IsProbablePrime(value);
    }
}