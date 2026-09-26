using System.Numerics;
using RCA;

Console.WriteLine("RCA DIAGNOSTIC TEST");
Console.WriteLine();

int passed = 0;
int failed = 0;

void Check(string name, bool condition)
{
    if (condition)
    {
        Console.WriteLine($"PASS - {name}");
        passed++;
    }
    else
    {
        Console.WriteLine($"FAIL - {name}");
        failed++;
    }
}


// ============================================================
// RANDOM 256-BIT RCA TESTS
// ============================================================

RCAMain rca = RCAMain.CreateRandom();

Check("P is 256-bit", RCAMain.BitLength(rca.P) == 256);
Check("Q is 256-bit", RCAMain.BitLength(rca.Q) == 256);
Check("Seed is 256-bit", RCAMain.BitLength(rca.Seed) == 256);
Check("P != Q", rca.P != rca.Q);

Check("P passes primality test", RCAMain.IsPrime(rca.P));
Check("Q passes primality test", RCAMain.IsPrime(rca.Q));

List<BigInteger> constants = rca.MakeConstants();

Check("Constant count", constants.Count == RCAMain.Rounds + 2);
Check("Constants within Q", constants.All(c => c >= 0 && c < rca.Q));

BigInteger polyInput = 123456789;

Check(
    "Poly1 within Q",
    rca.Poly1(polyInput) >= 0 &&
    rca.Poly1(polyInput) < rca.Q
);

Check(
    "Poly2 within Q",
    rca.Poly2(polyInput) >= 0 &&
    rca.Poly2(polyInput) < rca.Q
);


// ============================================================
// DIFFUSION
// ============================================================

BigInteger diffusionX = 123456789;
BigInteger diffusionY = 987654321;

BigInteger rDiff = constants[^2];
BigInteger sDiff = constants[^1];

var diffusion = rca.DiffusionForward(
    diffusionX,
    diffusionY,
    rDiff,
    sDiff
);

var diffusionBack = rca.DiffusionInverse(
    diffusion.A,
    diffusion.B,
    rDiff,
    sDiff
);

Check(
    "Diffusion reversibility",
    diffusionBack.X == diffusionX &&
    diffusionBack.Y == diffusionY
);


// ============================================================
// TRIANGULAR
// ============================================================

var triangular = rca.TriForward(
    diffusionX,
    diffusionY,
    constants[0],
    constants[1]
);

var triangularBack = rca.TriInverse(
    triangular.U,
    triangular.V,
    constants[0],
    constants[1]
);

Check(
    "Triangular reversibility",
    triangularBack.X == diffusionX &&
    triangularBack.Y == diffusionY
);


// ============================================================
// FULL REVERSIBILITY
// ============================================================

(BigInteger X, BigInteger Y) TestRoundTrip(
    BigInteger x,
    BigInteger y)
{
    var encrypted = rca.RcaForward(x, y);

    return rca.RcaInverse(
        encrypted.X,
        encrypted.Y
    );
}

var testCases = new (string Name, BigInteger X, BigInteger Y)[]
{
    ("Full reversibility (0, 0)", 0, 0),
    ("Full reversibility (1, 1)", 1, 1),
    (
        "Full reversibility (123456789, 987654321)",
        123456789,
        987654321
    ),
    (
        "Full reversibility (large P/Q-ish pair)",
        rca.P - 1,
        rca.Q - 1
    ),
    (
        "Full reversibility (Q-1,Q-2)",
        rca.Q - 1,
        rca.Q - 2
    ),
    (
        "Full reversibility (2^255,2^254)",
        BigInteger.One << 255,
        BigInteger.One << 254
    )
};

foreach (var test in testCases)
{
    var result = TestRoundTrip(test.X, test.Y);

    Check(
        test.Name,
        result.X == RCAMain.Mod(test.X, rca.Q) &&
        result.Y == RCAMain.Mod(test.Y, rca.Q)
    );
}


// ============================================================
// DETERMINISM
// ============================================================

var output1 = rca.RcaForward(
    123456789,
    987654321
);

var output2 = rca.RcaForward(
    123456789,
    987654321
);

var output3 = rca.RcaForward(
    987654321,
    123456789
);

Check(
    "Different inputs produce different outputs",
    output1.X != output3.X ||
    output1.Y != output3.Y
);

Check(
    "Deterministic for same instance",
    output1.X == output2.X &&
    output1.Y == output2.Y
);


// ============================================================
// INDEPENDENT INSTANCE
// ============================================================

RCAMain independentRca = RCAMain.CreateRandom();

Check(
    "Independent instances have different parameters",
    rca.P != independentRca.P ||
    rca.Q != independentRca.Q ||
    rca.Seed != independentRca.Seed
);

Console.WriteLine();
Console.WriteLine($"RESULT: {passed} passed, {failed} failed");


// ============================================================
// MAXIMA REFERENCE CONFIGURATION
// ============================================================

Console.WriteLine();
Console.WriteLine("MAXIMA REFERENCE VECTORS");
Console.WriteLine();

BigInteger referenceP =
    BigInteger.Parse("2305843009213693951");

BigInteger referenceQ =
    BigInteger.Parse("2305843009213694087");

string referenceSeedText =
    "144118022604360466379332110321945356667996320176821654815560508224772578026524718354405474321932602135753904354421963001119536916592817041633014258851078405811288960924013247772905844781202479288539485403198721646843881";

BigInteger referenceSeed =
    BigInteger.Parse(referenceSeedText);

RCAMain referenceRca = new(
    referenceP,
    referenceQ,
    referenceSeed
);

Console.WriteLine($"P: {referenceP}");
Console.WriteLine($"Q: {referenceQ}");
Console.WriteLine($"Seed: {referenceSeed}");
Console.WriteLine();


// ============================================================
// EXACT MAXIMA REFERENCE VECTORS
// ============================================================

var referenceVectors =
    new (string Name,
         BigInteger X,
         BigInteger Y,
         BigInteger ExpectedX,
         BigInteger ExpectedY)[]
{
    (
        "(0, 0)",
        0,
        0,
        3851785920185771,
        2023708316284830910
    ),

    (
        "(1, 1)",
        1,
        1,
        2262457973714018858,
        120026758354511018
    ),

    (
        "(123456789, 987654321)",
        123456789,
        987654321,
        351668050717095916,
        50542148241801508
    ),

    (
        "(Q-1, Q-2)",
        referenceQ - 1,
        referenceQ - 2,
        363262715077253956,
        1243962249239280161
    ),

    (
        "(2^60, 2^59)",
        BigInteger.One << 60,
        BigInteger.One << 59,
        475603513027121799,
        2195832359774307805
    )
};


// ============================================================
// RUN REFERENCE VECTORS
// ============================================================

foreach (var vector in referenceVectors)
{
    var result = referenceRca.RcaForward(
        vector.X,
        vector.Y
    );

    Console.WriteLine($"Input:    {vector.Name}");
    Console.WriteLine($"C# X:     {result.X}");
    Console.WriteLine($"C# Y:     {result.Y}");
    Console.WriteLine($"Maxima X: {vector.ExpectedX}");
    Console.WriteLine($"Maxima Y: {vector.ExpectedY}");

    bool matches =
        result.X == vector.ExpectedX &&
        result.Y == vector.ExpectedY;

    Check(
        $"Maxima vector {vector.Name}",
        matches
    );

    Console.WriteLine();
}


// ============================================================
// FINAL RESULT
// ============================================================

Console.WriteLine(
    $"FINAL RESULT: {passed} passed, {failed} failed"
);

if (failed == 0)
    Console.WriteLine("RCA TEST SUITE: PASS");
else
    Console.WriteLine("RCA TEST SUITE: FAIL");
