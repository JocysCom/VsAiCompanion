using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.VSSdkCompatibility;
using Microsoft.VisualStudio.Text.Tagging;
using System.Diagnostics;

[VisualStudioContribution]
public class ExtensionCommand : Microsoft.VisualStudio.Extensibility.Commands.Command
{
	private TraceSource traceSource;
	private AsyncServiceProviderInjection<DTE, DTE2> dte;
	private MefInjection<IBufferTagAggregatorFactoryService> bufferTagAggregatorFactoryService;

	public ExtensionCommand(
		VisualStudioExtensibility extensibility,
		TraceSource traceSource,
		AsyncServiceProviderInjection<DTE, DTE2> dte,
		MefInjection<IBufferTagAggregatorFactoryService> bufferTagAggregatorFactoryService)
		: base(extensibility)
	{
		this.dte = dte;
		this.bufferTagAggregatorFactoryService = bufferTagAggregatorFactoryService;
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
		await Extensibility.Shell().ShowPromptAsync("Hello from an extension!", PromptOptions.OK, cancellationToken);
	}
}
