using System.Numerics;

namespace RCA;

public class RCASession
{
    public RCAMain Rca { get; }

    public RCASession(RCAMain rca)
    {
        Rca = rca ?? throw new ArgumentNullException(nameof(rca));
    }

    public (BigInteger X, BigInteger Y) Encrypt(BigInteger x, BigInteger y)
    {
        return Rca.RcaForward(x, y);
    }

    public (BigInteger X, BigInteger Y) Decrypt(BigInteger x, BigInteger y)
    {
        return Rca.RcaInverse(x, y);
    }
}