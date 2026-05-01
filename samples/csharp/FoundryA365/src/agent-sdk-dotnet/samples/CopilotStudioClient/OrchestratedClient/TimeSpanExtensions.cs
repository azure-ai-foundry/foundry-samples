namespace OrchestratedClientSample
{
    internal static class TimeSpanExtensions
    {
        internal static string ToDurationString(this TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"hh\:mm\:ss\.fff");
        }
    }
}
