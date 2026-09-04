public static class Program
{
    public static void Main()
    {
        // Deliberate compile error: missing expression after '='.
        int x =;
        System.Console.WriteLine(x);
    }
}
