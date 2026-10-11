using System.Threading.Tasks;

namespace DDFLanguageEditor.Cli
{
    internal static class Program
    {
        private static Task<int> Main(string[] args) =>
            DdfCliApplication.RunAsync(args, System.Console.Out,
                System.Console.Error);
    }
}
