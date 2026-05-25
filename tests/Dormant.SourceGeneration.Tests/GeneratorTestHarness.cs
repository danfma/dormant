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
                trackIncrementalGeneratorSteps: true));
}

// In-memory AdditionalText for feeding DormantQL files to the generator under test.
internal sealed class TestAdditionalText(string path, string text) : AdditionalText
{
    private readonly SourceText _text = SourceText.From(text);

    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}
