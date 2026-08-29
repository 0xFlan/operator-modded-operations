using System;

internal static class Program
{
    private static int Main()
    {
        try
        {
            NativePolicyContractTests.Run();
            NativePolicyReceiptTests.Run();
            Console.WriteLine("Peer native policy tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }
}

internal static class TestAssert
{
    internal static void True(bool value, string message = "assertion failed")
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    internal static T Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T ex)
        {
            return ex;
        }
        throw new InvalidOperationException(
            "expected exception " + typeof(T).FullName);
    }

    internal static byte[] Sequence(int count, byte first)
    {
        byte[] value = new byte[count];
        for (int index = 0; index < count; index++)
            value[index] = checked((byte)(first + index));
        return value;
    }
}
