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
}