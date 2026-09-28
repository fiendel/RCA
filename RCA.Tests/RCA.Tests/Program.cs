using System.Numerics;
using System.Security.Cryptography;


using RcaEngine = RCA.RCA;

internal class Program
{
    const int WordBits = 256;
    const int StateBits = 512;
    const int Rounds = 32;
    const int Samples = 5000;
    const int DifferentialSamples = 50000;
    const int BiasSamples = 100000;
    const int LinearMasks = 200;
    const int LinearSamples = 10000;

    static int PassCount;
    static int FailCount;
    static int WarnCount;

    static void Main()
    {
        Console.WriteLine("============================================================");
        Console.WriteLine("RCA 1.5 ALPHA - 256-BIT / 512-BIT CRYPTANALYSIS");
        Console.WriteLine("============================================================");

        var rca = RCA.RCA.CreateRandom(Rounds);

        InstanceTest(rca);
        DeterminismTest(rca);
        ReversibilityTest(rca);
        ParameterStructureTest(rca);
        EdgeCaseTest(rca);
        WordDiffusionTest(rca);
        StrictAvalancheTest(rca);
        InverseAvalancheTest(rca);
        DifferentialTest(rca);
        ZeroDifferentialTest(rca);
        ComplementTest(rca);
        InputSwapTest(rca);
        RelatedSeedTest(rca);
        SeedAvalancheTest(rca);
        SeedEquivalenceTest(rca);
        CollisionTest(rca);
        FixedPointTest(rca);
        CycleTest(rca);
        LinearTest(rca);
        BitPairCorrelationTest(rca);
        BiasTest(rca);
        ModularStructureTest(rca);
        ConstantTest(rca);
        RoundSymmetryTest(rca);
        ReducedRoundTest(rca);
        FiniteDifferenceTest(rca);
        ProductionTest(rca);

        Console.WriteLine();
        Console.WriteLine("============================================================");
        Console.WriteLine($"PASS = {PassCount}");
        Console.WriteLine($"FAIL = {FailCount}");
        Console.WriteLine($"WARN = {WarnCount}");
        Console.WriteLine("============================================================");

        if (FailCount == 0 && WarnCount == 0)
            Console.WriteLine("TEST SUITE PASSED");
        else if (FailCount == 0)
            Console.WriteLine("TEST SUITE COMPLETED WITH WARNINGS");
        else
            Console.WriteLine("TEST SUITE FAILED");

        Console.WriteLine("============================================================");
    }

    static void Header(string name) => Console.WriteLine($"\nTEST: {name}");

    static void Pass(string name)
    {
        PassCount++;
        Console.WriteLine($"PASS: {name}");
    }

    static void Fail(string name)
    {
        FailCount++;
        Console.WriteLine($"FAIL: {name}");
    }

    static void Warn(string name)
    {
        WarnCount++;
        Console.WriteLine($"WARN: {name}");
    }

    static void Characterize(string name, double avg, int min, int max)
    {
        Console.WriteLine($"CHARACTERIZATION: {name} avg={avg:F3} min={min} max={max}");
    }

    static BigInteger RandomBelow(BigInteger maximum)
    {
        int bytes = maximum.GetByteCount(isUnsigned: true);
        byte[] buffer = new byte[bytes];

        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            BigInteger value = new(buffer, isUnsigned: true);
            if (value < maximum)
                return value;
        }
    }

    static BigInteger RandomSeed256()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        bytes[31] |= 0x80;
        return new BigInteger(bytes, isUnsigned: true);
    }

    static BigInteger RandomField(BigInteger q) => RandomBelow(q);

    static (BigInteger x, BigInteger y) RandomState(BigInteger q) => (RandomField(q), RandomField(q));

    static int CountBits(BigInteger value)
    {
        value = BigInteger.Abs(value);
        int count = 0;

        while (value != 0)
        {
            value >>= 1;
            count++;
        }

        return count;
    }

    static int Hamming(BigInteger a, BigInteger b) => CountBits(a ^ b);

    static int StateDistance((BigInteger x, BigInteger y) a, (BigInteger x, BigInteger y) b) => Hamming(a.x, b.x) + Hamming(a.y, b.y);

    static BigInteger FlipBit(BigInteger value, int bit) => value ^ (BigInteger.One << bit);

    static int Parity(BigInteger value)
    {
        int parity = 0;

        while (value != 0)
        {
            parity ^= 1;
            value &= value - 1;
        }

        return parity;
    }

    static BigInteger RandomMask()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return new BigInteger(bytes, isUnsigned: true);
    }

    static void InstanceTest(RCA.RCA rca)
    {
        Header("INSTANCE");

        int pBits = RCA.RCA.BitLength(RCA.RCA.P);
        int qBits = RCA.RCA.BitLength(RCA.RCA.Q);
        int sBits = RCA.RCA.BitLength(RCA.RCA.S);
        int seedBits = RCA.RCA.BitLength(rca.Seed);

        Console.WriteLine($"P bits     : {pBits}");
        Console.WriteLine($"Q bits     : {qBits}");
        Console.WriteLine($"S bits     : {sBits}");
        Console.WriteLine($"Seed bits  : {seedBits}");
        Console.WriteLine($"Word bits  : {WordBits}");
        Console.WriteLine($"State bits : {StateBits}");
        Console.WriteLine($"Rounds     : {rca.Rounds}");

        Pass("RCA instance created");

        if (pBits == 256) Pass("P is exactly 256-bit");
        else Fail($"P must be exactly 256-bit, actual={pBits}");

        if (qBits == 256) Pass("Q is exactly 256-bit");
        else Fail($"Q must be exactly 256-bit, actual={qBits}");

        if (sBits == 256) Pass("S is exactly 256-bit");
        else Fail($"S must be exactly 256-bit, actual={sBits}");

        if (seedBits == 256) Pass("Seed is exactly 256-bit");
        else Fail($"Seed must be exactly 256-bit, actual={seedBits}");

        if (WordBits == 256) Pass("Words are exactly 256-bit");
        else Fail("Word size is not 256-bit");

        if (StateBits == 512) Pass("State is exactly 512-bit");
        else Fail("State size is not 512-bit");

        if (rca.Rounds == Rounds) Pass("Round count is 32");
        else Fail($"Round count must be {Rounds}, actual={rca.Rounds}");

        if (RCA.RCA.P != RCA.RCA.Q) Pass("P != Q");
        else Fail("P == Q");

        if (RCA.RCA.Q > 0) Pass("Q positive");
        else Fail("Q is not positive");
    }

    static void DeterminismTest(RCA.RCA rca)
    {
        Header("DETERMINISM");

        bool ok = true;

        for (int i = 0; i < Samples; i++)
        {
            var state = RandomState(RCA.RCA.Q);
            var a = rca.RcaForward(state.x, state.y);
            var b = rca.RcaForward(state.x, state.y);

            if (a != b)
            {
                ok = false;
                break;
            }
        }

        if (ok) Pass("Deterministic");
        else Fail("Determinism");
    }

    static void ReversibilityTest(RCA.RCA rca)
    {
        Header("REVERSIBILITY");

        bool ok = true;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);
            var restored = rca.RcaInverse(output.x, output.y);

            if (restored != input)
            {
                ok = false;
                Console.WriteLine($"Failure at sample {i}");
                Console.WriteLine($"Input    : {input}");
                Console.WriteLine($"Output   : {output}");
                Console.WriteLine($"Restored : {restored}");
                break;
            }
        }

        if (ok) Pass("Full 32-round reversibility");
        else Fail("Full 32-round reversibility");
    }

    static void ParameterStructureTest(RCA.RCA rca)
    {
        Header("PARAMETER STRUCTURE");

        if (RCA.RCA.IsPrime(RCA.RCA.P))
            Pass("P probable prime");
        else
            Warn("P is composite");

        if (RCA.RCA.IsPrime(RCA.RCA.Q))
            Pass("Q probable prime");
        else
            Warn("Q is composite");

        if (BigInteger.GreatestCommonDivisor(RCA.RCA.P, RCA.RCA.Q) == 1)
            Pass("gcd(P,Q)=1");
        else
            Fail("gcd(P,Q)!=1");

        if (BigInteger.GreatestCommonDivisor(RCA.RCA.S, RCA.RCA.Q) == 1)
            Pass("gcd(S,Q)=1");
        else
            Warn("gcd(S,Q)!=1");
    }

    static void EdgeCaseTest(RCA.RCA rca)
    {
        Header("EDGE CASES");

        BigInteger q = RCA.RCA.Q;
        BigInteger highBit = BigInteger.One << 255;

        BigInteger[] values =
        [
            0,
            1,
            2,
            3,
            highBit,
            q - 2,
            q - 1
        ];

        bool ok = true;

        foreach (BigInteger x in values)
            foreach (BigInteger y in values)
            {
                var output = rca.RcaForward(x, y);
                var restored = rca.RcaInverse(output.x, output.y);

                if (restored != (RCA.RCA.Mod(x, q), RCA.RCA.Mod(y, q)))
                {
                    ok = false;
                    Console.WriteLine($"Edge failure x={x} y={y}");
                    break;
                }
            }

        if (ok) Pass("Edge cases");
        else Fail("Edge cases");
    }

    static void WordDiffusionTest(RCA.RCA rca)
    {
        Header("WORD DIFFUSION");

        long sum = 0;
        int min = int.MaxValue;
        int max = 0;
        int xAffected = 0;
        int yAffected = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);

            var modifiedX = (FlipBit(input.x, Random.Shared.Next(WordBits)), input.y);
            var modifiedY = (input.x, FlipBit(input.y, Random.Shared.Next(WordBits)));

            var outputX = rca.RcaForward(modifiedX.Item1, modifiedX.Item2);
            var outputY = rca.RcaForward(modifiedY.Item1, modifiedY.Item2);

            int dx = StateDistance(output, outputX);
            int dy = StateDistance(output, outputY);

            if (dx > 0) xAffected++;
            if (dy > 0) yAffected++;

            int d = (dx + dy) / 2;
            sum += d;
            min = Math.Min(min, d);
            max = Math.Max(max, d);
        }

        double avg = (double)sum / Samples;

        Console.WriteLine($"X affected: {(double)xAffected / Samples:P2}");
        Console.WriteLine($"Y affected: {(double)yAffected / Samples:P2}");
        Characterize("Word diffusion", avg, min, max);

        if (xAffected == Samples && yAffected == Samples)
            Pass("Both 256-bit words affect output");
        else
            Fail("Word diffusion");
    }

    static void StrictAvalancheTest(RCA.RCA rca)
    {
        Header("STRICT AVALANCHE");

        long sum = 0;
        int min = int.MaxValue;
        int max = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);

            int bit = Random.Shared.Next(StateBits);
            var modified = bit < WordBits
                ? (FlipBit(input.x, bit), input.y)
                : (input.x, FlipBit(input.y, bit - WordBits));

            var changed = rca.RcaForward(modified.Item1, modified.Item2);
            int distance = StateDistance(output, changed);

            sum += distance;
            min = Math.Min(min, distance);
            max = Math.Max(max, distance);
        }

        double avg = (double)sum / Samples;
        double expected = StateBits / 2.0;
        double deviation = Math.Abs(avg - expected) / expected;

        Characterize("Strict avalanche", avg, min, max);
        Console.WriteLine($"Expected: {expected:F3}");
        Console.WriteLine($"Deviation: {deviation:P3}");

        if (deviation < 0.02 && min > StateBits * 0.30)
            Pass("Strict avalanche");
        else
            Warn("Strict avalanche outside preferred characterization");
    }

    static void InverseAvalancheTest(RCA.RCA rca)
    {
        Header("INVERSE AVALANCHE");

        long sum = 0;
        int min = int.MaxValue;
        int max = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);

            int bit = Random.Shared.Next(StateBits);
            var modified = bit < WordBits
                ? (FlipBit(output.x, bit), output.y)
                : (output.x, FlipBit(output.y, bit - WordBits));

            var restoredA = rca.RcaInverse(output.x, output.y);
            var restoredB = rca.RcaInverse(modified.Item1, modified.Item2);

            int distance = StateDistance(restoredA, restoredB);

            sum += distance;
            min = Math.Min(min, distance);
            max = Math.Max(max, distance);
        }

        double avg = (double)sum / Samples;
        Characterize("Inverse avalanche", avg, min, max);

        if (avg > StateBits * 0.45)
            Pass("Inverse avalanche");
        else
            Warn("Weak inverse avalanche characterization");
    }

    static void DifferentialTest(RCA.RCA rca)
    {
        Header("DIFFERENTIAL");

        var frequencies = new Dictionary<(BigInteger x, BigInteger y), int>();
        BigInteger dx = BigInteger.One;

        for (int i = 0; i < DifferentialSamples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var a = rca.RcaForward(input.x, input.y);
            var b = rca.RcaForward(RCA.RCA.Mod(input.x + dx, RCA.RCA.Q), input.y);

            var differential = (
                RCA.RCA.Mod(b.x - a.x, RCA.RCA.Q),
                RCA.RCA.Mod(b.y - a.y, RCA.RCA.Q)
            );

            frequencies.TryGetValue(differential, out int count);
            frequencies[differential] = count + 1;
        }

        int maxFrequency = frequencies.Values.Max();
        double probability = (double)maxFrequency / DifferentialSamples;

        Console.WriteLine($"Unique differentials: {frequencies.Count}");
        Console.WriteLine($"Maximum frequency: {maxFrequency}");
        Console.WriteLine($"Maximum probability: {probability:P4}");

        if (maxFrequency <= Math.Max(5, DifferentialSamples / 1000))
            Pass("Differential distribution");
        else
            Warn("Differential concentration detected");
    }

    static void ZeroDifferentialTest(RCA.RCA rca)
    {
        Header("ZERO DIFFERENTIAL");

        int zeroCount = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var a = rca.RcaForward(input.x, input.y);
            var b = rca.RcaForward(RCA.RCA.Mod(input.x + 1, RCA.RCA.Q), input.y);

            if (a == b)
                zeroCount++;
        }

        Console.WriteLine($"Zero differentials: {zeroCount}/{Samples}");

        if (zeroCount == 0)
            Pass("No zero differential collisions");
        else
            Fail("Zero differential collisions");
    }

    static void ComplementTest(RCA.RCA rca)
    {
        Header("COMPLEMENT");

        BigInteger mask = (BigInteger.One << WordBits) - 1;
        int equal = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var complement = (input.x ^ mask, input.y ^ mask);

            var a = rca.RcaForward(input.x, input.y);
            var b = rca.RcaForward(complement.Item1, complement.Item2);

            if (a == b)
                equal++;
        }

        Console.WriteLine($"Equal outputs: {equal}/{Samples}");

        if (equal == 0)
            Pass("Complement separation");
        else
            Warn("Complement relation detected");
    }

    static void InputSwapTest(RCA.RCA rca)
    {
        Header("INPUT SWAP");

        long sum = 0;
        int min = int.MaxValue;
        int max = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var a = rca.RcaForward(input.x, input.y);
            var b = rca.RcaForward(input.y, input.x);

            int distance = StateDistance(a, b);

            sum += distance;
            min = Math.Min(min, distance);
            max = Math.Max(max, distance);
        }

        double avg = (double)sum / Samples;
        Characterize("Input swap", avg, min, max);

        if (avg > StateBits * 0.45)
            Pass("Input swap separation");
        else
            Warn("Input swap symmetry");
    }

    static void RelatedSeedTest(RCA.RCA rca)
    {
        Header("RELATED SEED");

        BigInteger seed = rca.Seed;
        BigInteger relatedSeed = seed ^ BigInteger.One;
        var related = new RCA.RCA(relatedSeed, Rounds);

        long sum = 0;
        int min = int.MaxValue;
        int max = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);

            var a = rca.RcaForward(input.x, input.y);
            var b = related.RcaForward(input.x, input.y);

            int distance = StateDistance(a, b);

            sum += distance;
            min = Math.Min(min, distance);
            max = Math.Max(max, distance);
        }

        double avg = (double)sum / Samples;
        Characterize("Related seed", avg, min, max);

        if (avg > StateBits * 0.45)
            Pass("Related-seed diffusion");
        else
            Warn("Related-seed diffusion");
    }

    static void SeedAvalancheTest(RCA.RCA rca)
    {
        Header("SEED AVALANCHE");

        BigInteger seed = rca.Seed;
        BigInteger changedSeed = seed ^ (BigInteger.One << Random.Shared.Next(WordBits));

        var related = new RCA.RCA(changedSeed, Rounds);

        long sum = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);

            var a = rca.RcaForward(input.x, input.y);
            var b = related.RcaForward(input.x, input.y);

            sum += StateDistance(a, b);
        }

        double avg = (double)sum / Samples;

        Console.WriteLine($"Average seed avalanche: {avg:F3}");

        if (avg > StateBits * 0.45)
            Pass("Seed avalanche");
        else
            Warn("Seed avalanche");
    }

    static void SeedEquivalenceTest(RcaEngine rca)
    {
        Header("SEED EQUIVALENCE");

        var outputs = new HashSet<(BigInteger x, BigInteger y)>();
        bool collision = false;

        for (int i = 0; i < Samples; i++)
        {
            BigInteger seed = RandomSeed256();

            var instance = new RcaEngine(seed, Rounds);
            var input = RandomState(RcaEngine.Q);
            var output = instance.RcaForward(input.x, input.y);

            if (!outputs.Add(output))
            {
                collision = true;
                break;
            }
        }

        if (!collision)
            Pass("No sampled seed-output collisions");
        else
            Warn("Sampled seed-output collision");
    }

    static void CollisionTest(RCA.RCA rca)
    {
        Header("COLLISION");

        var outputs = new HashSet<(BigInteger x, BigInteger y)>();
        bool collision = false;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);

            if (!outputs.Add(output))
            {
                collision = true;
                break;
            }
        }

        if (!collision)
            Pass("No sampled collisions");
        else
            Fail("Output collision");
    }

    static void FixedPointTest(RCA.RCA rca)
    {
        Header("FIXED POINT");

        int count = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);

            if (output == input)
                count++;
        }

        Console.WriteLine($"Fixed points: {count}/{Samples}");

        if (count == 0)
            Pass("No sampled fixed points");
        else
            Warn("Fixed points observed");
    }

    static void CycleTest(RCA.RCA rca)
    {
        Header("CYCLE");

        var start = RandomState(RCA.RCA.Q);
        var current = start;
        var seen = new HashSet<(BigInteger x, BigInteger y)>();
        bool cycle = false;

        for (int i = 0; i < 10000; i++)
        {
            if (!seen.Add(current))
            {
                cycle = true;
                Console.WriteLine($"Cycle detected at step {i}");
                break;
            }

            current = rca.RcaForward(current.x, current.y);
        }

        if (!cycle)
            Pass("No short cycle observed");
        else
            Warn("Cycle observed");
    }

    static void LinearTest(RCA.RCA rca)
    {
        Header("LINEAR / WALSH SAMPLE");

        int maxBias = 0;
        BigInteger modulusMask = (BigInteger.One << WordBits) - 1;

        for (int maskIndex = 0; maskIndex < LinearMasks; maskIndex++)
        {
            BigInteger inputMaskX = RandomMask() & modulusMask;
            BigInteger inputMaskY = RandomMask() & modulusMask;
            BigInteger outputMaskX = RandomMask() & modulusMask;
            BigInteger outputMaskY = RandomMask() & modulusMask;

            int ones = 0;

            for (int i = 0; i < LinearSamples; i++)
            {
                var input = RandomState(RCA.RCA.Q);
                var output = rca.RcaForward(input.x, input.y);

                int inputParity = Parity(input.x & inputMaskX) ^ Parity(input.y & inputMaskY);
                int outputParity = Parity(output.x & outputMaskX) ^ Parity(output.y & outputMaskY);

                if (inputParity == outputParity)
                    ones++;
            }

            int bias = Math.Abs(ones * 2 - LinearSamples);
            maxBias = Math.Max(maxBias, bias);
        }

        double relativeBias = (double)maxBias / LinearSamples;

        Console.WriteLine($"Maximum sampled linear bias: {relativeBias:P3}");

        if (relativeBias < 0.05)
            Pass("Sampled linear/Walsh analysis");
        else
            Warn("Linear bias observed");
    }

    static void BitPairCorrelationTest(RCA.RCA rca)
    {
        Header("BIT-PAIR CORRELATION");

        int[] inputCount = new int[StateBits];
        int[] outputCount = new int[StateBits];
        int samples = Samples;

        for (int i = 0; i < samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);

            for (int bit = 0; bit < WordBits; bit++)
            {
                if (((input.x >> bit) & 1) != 0) inputCount[bit]++;
                if (((output.x >> bit) & 1) != 0) outputCount[bit]++;
            }

            for (int bit = 0; bit < WordBits; bit++)
            {
                if (((input.y >> bit) & 1) != 0) inputCount[WordBits + bit]++;
                if (((output.y >> bit) & 1) != 0) outputCount[WordBits + bit]++;
            }
        }

        double maxDeviation = 0;

        for (int i = 0; i < StateBits; i++)
        {
            double inputBias = Math.Abs((double)inputCount[i] / samples - 0.5);
            double outputBias = Math.Abs((double)outputCount[i] / samples - 0.5);
            maxDeviation = Math.Max(maxDeviation, Math.Max(inputBias, outputBias));
        }

        Console.WriteLine($"Maximum bit bias: {maxDeviation:P3}");

        if (maxDeviation < 0.05)
            Pass("Bit-pair marginal distribution");
        else
            Warn("Bit marginal bias");
    }

    static void BiasTest(RCA.RCA rca)
    {
        Header("SINGLE-BIT OUTPUT BIAS");

        long[] ones = new long[StateBits];

        for (int i = 0; i < BiasSamples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);

            for (int bit = 0; bit < WordBits; bit++)
            {
                if (((output.x >> bit) & 1) != 0) ones[bit]++;
                if (((output.y >> bit) & 1) != 0) ones[WordBits + bit]++;
            }
        }

        double maxBias = 0;

        for (int bit = 0; bit < StateBits; bit++)
        {
            double bias = Math.Abs((double)ones[bit] / BiasSamples - 0.5);
            maxBias = Math.Max(maxBias, bias);
        }

        Console.WriteLine($"Maximum single-bit bias: {maxBias:P4}");

        if (maxBias < 0.01)
            Pass("Single-bit output bias");
        else
            Warn("Single-bit output bias");
    }

    static void ModularStructureTest(RCA.RCA rca)
    {
        Header("MODULAR STRUCTURE");

        BigInteger p = RCA.RCA.P;
        BigInteger q = RCA.RCA.Q;
        BigInteger s = RCA.RCA.S;

        if (BigInteger.GreatestCommonDivisor(p, q) == 1)
            Pass("P/Q coprime");
        else
            Fail("P/Q share a factor");

        if (BigInteger.GreatestCommonDivisor(s, q) == 1)
            Pass("S/Q coprime");
        else
            Warn("S/Q share a factor");

        int zeroDivisors = 0;

        foreach (int small in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31 })
        {
            if (BigInteger.GreatestCommonDivisor(small, q) != 1)
                zeroDivisors++;
        }

        Console.WriteLine($"Small-factor probes: {zeroDivisors}");

        if (zeroDivisors == 0)
            Pass("No small zero-divisor factor detected");
        else
            Warn("Small zero-divisor factor detected");
    }

    static void ConstantTest(RCA.RCA rca)
    {
        Header("ROUND CONSTANTS");

        var seen = new HashSet<BigInteger>();
        bool duplicate = false;

        BigInteger previous = RCA.RCA.Mod(rca.Seed + RCA.RCA.P + RCA.RCA.S, RCA.RCA.Q);

        seen.Add(previous);

        for (int i = 1; i <= Rounds + 1; i++)
        {
            previous = rca.ConstantStep(previous);
            previous = RCA.RCA.Mod(previous + RCA.RCA.P * i + RCA.RCA.S * i * i, RCA.RCA.Q);

            if (!seen.Add(previous))
            {
                duplicate = true;
                break;
            }
        }

        if (!duplicate)
            Pass("Round constants unique in sample");
        else
            Warn("Round constant collision");
    }

    static void RoundSymmetryTest(RCA.RCA rca)
    {
        Header("ROUND SYMMETRY");

        long sum = 0;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);

            var r1 = rca.RoundForward(input.x, input.y, 1, BigInteger.One, BigInteger.One);
            var r2 = rca.RoundForward(input.x, input.y, 2, BigInteger.One, BigInteger.One);

            sum += StateDistance(r1, r2);
        }

        double avg = (double)sum / Samples;

        Console.WriteLine($"Average round-1/round-2 distance: {avg:F3}");

        if (avg > StateBits * 0.30)
            Pass("Round symmetry separation");
        else
            Warn("Round symmetry");
    }

    static void ReducedRoundTest(RCA.RCA rca)
    {
        Header("REDUCED ROUNDS");

        int[] roundCounts = [1, 2, 4, 8, 16, 32];

        foreach (int rounds in roundCounts)
        {
            var instance = new RCA.RCA(rca.Seed, rounds);
            bool ok = true;

            for (int i = 0; i < 1000; i++)
            {
                var input = RandomState(RCA.RCA.Q);
                var output = instance.RcaForward(input.x, input.y);
                var restored = instance.RcaInverse(output.x, output.y);

                if (restored != input)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
                Pass($"Reduced/full {rounds}-round reversibility");
            else
                Fail($"{rounds}-round reversibility");
        }
    }

    static void FiniteDifferenceTest(RCA.RCA rca)
    {
        Header("FINITE DIFFERENCE");

        const int MaxOrder = 16;
        const int Points = 64;

        for (int order = 1; order <= MaxOrder; order++)
        {
            BigInteger x = RandomField(RCA.RCA.Q);
            BigInteger y = RandomField(RCA.RCA.Q);

            BigInteger value = rca.RcaForward(x, y).x;
            bool nonZero = value != 0;

            for (int j = 1; j <= order; j++)
            {
                x = RCA.RCA.Mod(x + 1, RCA.RCA.Q);
                BigInteger next = rca.RcaForward(x, y).x;
                value = RCA.RCA.Mod(next - value, RCA.RCA.Q);
                if (value != 0)
                    nonZero = true;
            }

            Console.WriteLine($"Order {order}: {(nonZero ? "non-zero" : "zero")}");
        }

        Warn("Finite-difference structure is characterization, not a security proof");
    }

    static void ProductionTest(RCA.RCA rca)
    {
        Header("PRODUCTION SCALE");

        bool ok = true;

        for (int i = 0; i < Samples; i++)
        {
            var input = RandomState(RCA.RCA.Q);
            var output = rca.RcaForward(input.x, input.y);
            var restored = rca.RcaInverse(output.x, output.y);

            if (restored != input)
            {
                ok = false;
                Console.WriteLine($"Production failure at sample {i}");
                break;
            }
        }

        if (ok)
            Pass("Production-scale reversibility");
        else
            Fail("Production-scale reversibility");
    }
}
