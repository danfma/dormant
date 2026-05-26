using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using VerifyTests;

namespace Dormant.SourceGeneration.Tests;

// Shared harness for generator tests: Verify init (for US1+ snapshot tests) + driver helpers.
internal static class GeneratorTestHarness
{
    [ModuleInitializer]
    public static void Initialize() => VerifySourceGenerators.Initialize();

    public static GeneratorDriver CreateDriver(params AdditionalText[] additionalTexts) =>
        CSharpGeneratorDriver.Create(
            generators: [new DormantGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

    // Variant that supplies global build properties (e.g. build_property.DormantNamingConvention).
    public static GeneratorDriver CreateDriver(
        System.Collections.Generic.Dictionary<string, string> globalOptions,
        params AdditionalText[] additionalTexts
    ) =>
        CSharpGeneratorDriver.Create(
            generators: [new DormantGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts,
            parseOptions: null,
            optionsProvider: new TestOptionsProvider(globalOptions),
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );
}

// Supplies global analyzer-config options (build properties) to the generator under test.
internal sealed class TestOptionsProvider(
    System.Collections.Generic.Dictionary<string, string> global
) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
{
    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions { get; } =
        new TestOptions(global);

    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(
        SyntaxTree tree
    ) => TestOptions.Empty;

    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(
        AdditionalText textFile
    ) => TestOptions.Empty;
}

internal sealed class TestOptions(System.Collections.Generic.Dictionary<string, string> values)
    : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
{
    public static readonly TestOptions Empty = new([]);

    public override bool TryGetValue(
        string key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value
    ) => values.TryGetValue(key, out value);
}

// In-memory AdditionalText for feeding DormantQL files to the generator under test.
internal sealed class TestAdditionalText(string path, string text) : AdditionalText
{
    private readonly SourceText _text = SourceText.From(text);

    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}
