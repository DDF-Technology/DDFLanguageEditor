using System;
using System.Linq;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.LanguagePackagePublishingDemo;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 7 && (args[0] == "starter" || args[0] == "clone-ddf"))
            {
                var identity = new DdfLanguageAuthoringIdentity(args[2], args[3],
                    args[4], args[5], new[] { args[6] });
                DdfLanguageAuthoringTemplateResult template = args[0] == "starter"
                    ? DdfLanguageAuthoringProjectFactory.CreateBlank(identity)
                    : DdfLanguageAuthoringProjectFactory.CloneEmbeddedDdf(identity);
                if (!template.Success) return Fail(template.Diagnostics.Select(item =>
                    item.ToString()));
                DdfLanguageAuthoringGenerationResult generated =
                    DdfLanguagePackageAuthoring.GenerateDirectory(template.Project,
                        args[1]);
                if (!generated.Success) return Fail(generated.Diagnostics.Select(item =>
                    item.ToString()));
                Console.WriteLine("Pacchetto creato: " + generated.OutputPath);
                return 0;
            }

            if (args.Length == 3 && args[0] == "publish")
            {
                DdfLanguageAuthoringResult loaded =
                    DdfLanguagePackageAuthoring.LoadDirectory(args[1]);
                if (!loaded.Success) return Fail(loaded.Diagnostics.Select(item =>
                    item.ToString()));
                DdfLanguagePackageArchiveResult archive =
                    DdfLanguagePackageArchiveBuilder.Generate(loaded.Project,
                        args[2]);
                if (!archive.Success) return Fail(archive.Diagnostics.Select(item =>
                    item.ToString()));
                Console.WriteLine("Archivio: " + archive.OutputPath);
                Console.WriteLine("SHA-256: " + archive.Sha256);
                return 0;
            }

            Console.Error.WriteLine("Uso:");
            Console.Error.WriteLine("  starter <cartella-nuova> <id> <nome> <versione-pacchetto> <versione-linguaggio> <.estensione>");
            Console.Error.WriteLine("  clone-ddf <cartella-nuova> <id> <nome> <versione-pacchetto> <versione-linguaggio> <.estensione>");
            Console.Error.WriteLine("  publish <cartella-pacchetto> <archivio-nuovo.ddflang>");
            return 2;
        }
        catch (Exception exception) when (exception is ArgumentException ||
            exception is System.IO.IOException ||
            exception is UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int Fail(System.Collections.Generic.IEnumerable<string> diagnostics)
    {
        foreach (string diagnostic in diagnostics)
            Console.Error.WriteLine(diagnostic);
        return 1;
    }
}
