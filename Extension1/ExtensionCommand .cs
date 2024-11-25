using EnvDTE;
using EnvDTE80;
using JocysCom.VS.AiCompanion.Extension;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.VSSdkCompatibility;
using Microsoft.VisualStudio.Text.Tagging;
using System.Diagnostics;

[VisualStudioContribution]
public class ExtensionCommand : Microsoft.VisualStudio.Extensibility.Commands.Command
{
	private readonly TraceSource _TraceSource;
	private readonly AsyncServiceProviderInjection<DTE, DTE2> _DTE;
	private readonly MefInjection<IBufferTagAggregatorFactoryService> _BufferTagAggregatorFactoryService;

	public ExtensionCommand(
		VisualStudioExtensibility extensibility,
		TraceSource traceSource,
		AsyncServiceProviderInjection<DTE, DTE2> dte,
		MefInjection<IBufferTagAggregatorFactoryService> bufferTagAggregatorFactoryService)
		: base(extensibility)
	{
		_TraceSource = traceSource;
		_DTE = dte;
		_BufferTagAggregatorFactoryService = bufferTagAggregatorFactoryService;
	}

#pragma warning disable CEE0027 // String not localized
	public override CommandConfiguration CommandConfiguration => new("Open AI Companion")
#pragma warning restore CEE0027 // String not localized
	{
		Placements = new[] { CommandPlacement.KnownPlacements.ExtensionsMenu },
		Icon = new(ImageMoniker.KnownValues.Extension, IconSettings.IconAndText),
	};

	public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
	{
		_TraceSource.TraceInformation("Executing Open AI Companion command.");
		await Extensibility.Shell().ShowToolWindowAsync<MainWindow>(true, cancellationToken);
		_TraceSource.TraceInformation("AI Companion window opened.");
	}
}
