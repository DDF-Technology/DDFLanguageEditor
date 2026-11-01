using DDFLanguageEditor.Core;

// Contract-only smoke: all replies below are locally constructed test data.
// There is no plug-in assembly, worker, authorization or build execution.
DdfExtensionProtocolCatalog.ValidateJson(File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "ddf-extension-protocol.json")));

var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
    "1.0.0", "0.42", "0.50", "Demo.dll", new string('a', 64),
    Array.Empty<string>(), new[] { "file.read", "file.write" });
var semanticPoint = new DdfExtensionPointDescriptor("ddf.demo.semantic",
    DdfExtensionPointKind.SemanticOperation, "1.0", new[] { "file.read" });
var runtimePoint = new DdfExtensionPointDescriptor("ddf.demo.runtime",
    DdfExtensionPointKind.RuntimeOperation, "1.0", Array.Empty<string>());
var hostPoint = new DdfExtensionPointDescriptor("ddf.demo.host",
    DdfExtensionPointKind.HostFunction, "1.0", Array.Empty<string>());
var buildPoint = new DdfExtensionPointDescriptor("ddf.demo.build",
    DdfExtensionPointKind.BuildBackend, "1.0", new[] { "file.read", "file.write" });
var catalog = new DdfExtensionPointCatalog(manifest,
    new[] { semanticPoint, runtimePoint, hostPoint, buildPoint });

var source = new DdfExtensionSourceSnapshot("src/main.ddf", "main() { ret 42; }");
var semantic = new DdfExtensionSemanticRequest(semanticPoint, source,
    "FunctionDeclaration", 0, 4);
var sourceLedger = new DdfExtensionSourceCorrelationLedger(TimeSpan.FromSeconds(5));
var semanticTicket = sourceLedger.Issue(semantic);
Check(DdfExtensionSourceFrameCodec.ParseRequest(semanticTicket.RequestJson,
    catalog).Semantic?.RuleId == "FunctionDeclaration");
var semanticResult = DdfExtensionSourceAssessment.AcceptSemantic(sourceLedger,
    DdfExtensionSourceFrameCodec.SerializeResponse(semanticTicket.CallId,
        DdfExtensionSemanticResponse.Success(semantic, new[]
        {
            new DdfExtensionSemanticDiagnostic(semantic,
                "ddf.demo.semantic.diagnostic.example",
                DdfExtensionSemanticSeverity.Info, "Esempio di diagnostica", 0, 4)
        })));
Check(semanticResult.Success && semanticResult.Semantic?.Diagnostics.Count == 1);
Console.WriteLine("SemanticOperation: contratto verificato");

foreach (DdfExtensionPointDescriptor point in new[] { runtimePoint, hostPoint })
{
    var request = new DdfExtensionCallRequest(point,
        new[] { new KeyValuePair<string, DdfExtensionValue>("value",
            DdfExtensionValue.FromInteger(42)) });
    var callLedger = new DdfExtensionCallCorrelationLedger(TimeSpan.FromSeconds(5));
    var ticket = callLedger.Issue(request);
    Check(DdfExtensionCallFrameCodec.ParseRequest(ticket.RequestJson,
        catalog).Request.Point.Id == point.Id);
    var assessment = DdfExtensionCallAssessment.Accept(callLedger,
        DdfExtensionCallFrameCodec.SerializeResponse(ticket.CallId,
            DdfExtensionCallResponse.Success(point, DdfExtensionValue.FromInteger(42))));
    Check(assessment.Success && assessment.Response?.Value.Integer == 42);
    Console.WriteLine(point.Kind + ": contratto verificato");
}

var build = new DdfExtensionBuildRequest(buildPoint, new[] { source },
    source.RelativePath, DdfExtensionBuildTargetKind.Executable);
var buildTicket = sourceLedger.Issue(build);
Check(DdfExtensionSourceFrameCodec.ParseRequest(buildTicket.RequestJson,
    catalog).Build?.TargetKind == DdfExtensionBuildTargetKind.Executable);
var buildResult = DdfExtensionSourceAssessment.AcceptBuild(sourceLedger,
    DdfExtensionSourceFrameCodec.SerializeResponse(buildTicket.CallId,
        DdfExtensionBuildResponse.Failure(build, "ddf.demo.build.error.not-implemented")));
Check(buildResult.Diagnostic?.Code == "DDFX601");
Console.WriteLine("BuildBackend: errore simulato classificato DDFX601");
Console.WriteLine("CONTRACTS ONLY: nessun plug-in caricato o eseguito");

static void Check(bool valid)
{
    if (!valid) throw new InvalidOperationException("Smoke del contratto non riuscito.");
}
