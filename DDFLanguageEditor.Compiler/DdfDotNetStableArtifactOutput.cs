using System;
using System.Collections.Generic;
using System.IO;

namespace DDFLanguageEditor.Compiler
{
    /// <summary>
    /// Materializes the verified compiler emission at a short, stable path.
    /// The content-addressed cache remains an internal implementation detail;
    /// this directory is the user-facing development output and is replaced as
    /// one complete bundle.
    /// </summary>
    internal static class DdfDotNetStableArtifactOutput
    {
        internal static DdfDotNetArtifactPublicationResult Replace(
            DdfDotNetArtifactEmissionResult emission, string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(outputRoot) ||
                !Path.IsPathRooted(outputRoot))
                return Failure("È richiesta una directory artefatti assoluta.");

            string destination;
            try
            {
                destination = Path.GetFullPath(outputRoot).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(Path.GetFileName(destination)) ||
                    string.IsNullOrEmpty(Path.GetDirectoryName(destination)))
                    return Failure("La directory artefatti non può essere una radice filesystem.");
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is NotSupportedException ||
                                               exception is PathTooLongException)
            {
                return Failure("Directory artefatti non valida: " +
                    exception.Message);
            }

            string parent = Path.GetDirectoryName(destination);
            string leaf = Path.GetFileName(destination);
            string candidate = Path.Combine(parent, "." + leaf +
                ".ddf-next-" + Guid.NewGuid().ToString("N"));
            string previous = Path.Combine(parent, "." + leaf +
                ".ddf-previous-" + Guid.NewGuid().ToString("N"));
            bool movedPrevious = false;
            try
            {
                Directory.CreateDirectory(parent);
                if (File.Exists(destination))
                    return Failure("La directory artefatti è occupata da un file.");

                DdfDotNetArtifactPublicationResult staged =
                    DdfDotNetArtifactPublisher.Publish(emission, candidate);
                if (!staged.Success) return staged;

                if (Directory.Exists(destination))
                {
                    Directory.Move(destination, previous);
                    movedPrevious = true;
                }
                Directory.Move(candidate, destination);
                if (movedPrevious) Directory.Delete(previous, true);

                var paths = new List<string>();
                foreach (string relativePath in staged.PublishedPaths)
                    paths.Add(relativePath);
                return new DdfDotNetArtifactPublicationResult(destination,
                    paths, null);
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is ArgumentException ||
                                               exception is NotSupportedException)
            {
                try
                {
                    if (!Directory.Exists(destination) && movedPrevious &&
                        Directory.Exists(previous))
                        Directory.Move(previous, destination);
                }
                catch (Exception restoreException) when (
                    restoreException is IOException ||
                    restoreException is UnauthorizedAccessException)
                {
                    return Failure("Aggiornamento output fallito: " +
                        exception.Message + " Ripristino fallito: " +
                        restoreException.Message);
                }
                return Failure("Aggiornamento output fallito: " +
                    exception.Message);
            }
            finally
            {
                DeleteOwnedDirectory(candidate, parent);
                DeleteOwnedDirectory(previous, parent);
            }
        }

        private static DdfDotNetArtifactPublicationResult Failure(
            string message) => new(null, null, new[]
            {
                new DdfDotNetContractIssue("DDFC417", message)
            });

        private static void DeleteOwnedDirectory(string path, string parent)
        {
            if (!Directory.Exists(path) || !string.Equals(
                    Path.GetDirectoryName(Path.GetFullPath(path)),
                    Path.GetFullPath(parent).TrimEnd(
                        Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase)) return;
            try { Directory.Delete(path, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
