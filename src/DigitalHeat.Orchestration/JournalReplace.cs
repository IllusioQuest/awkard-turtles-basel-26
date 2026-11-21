namespace DigitalHeat.Orchestration;

internal static class JournalReplace
{
    // Windows can temporarily refuse replacement while a reader/scanner holds the old file.
    // Preserve the flushed temporary journal and fail closed if the bounded retry expires.
    internal static void Move(string temporary,string destination)
    {
        const int MaximumAttempts=26;
        const int RetryDelayMilliseconds=20;
        for(int attempt=1;;attempt++){
            try{File.Move(temporary,destination,true);return;}
            catch(Exception e)when(OperatingSystem.IsWindows()&&attempt<MaximumAttempts&&
                (e is IOException or UnauthorizedAccessException)&&((e.HResult&0xffff) is 5 or 32 or 33)){
                Thread.Sleep(RetryDelayMilliseconds);
            }
        }
    }
}
