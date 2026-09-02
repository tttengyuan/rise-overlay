using HunterPie.Core.Client;
using HunterPie.Core.Client.Configuration;
using HunterPie.Core.Client.Configuration.Overlay.Class;
using HunterPie.Core.Domain.Enums;
using HunterPie.Core.Game;
using HunterPie.UI.Architecture.Overlay;
using HunterPie.UI.Overlay;
using HunterPie.UI.Overlay.Service;
using HunterPie.UI.Overlay.Views;
using HunterPie.UI.Overlay.Widgets.Classes;
using HunterPie.UI.Overlay.Widgets.Classes.ViewModels;
using System.Threading.Tasks;

namespace HunterPie.Features.Overlay.Widgets;

internal class ClassWidgetInitializer(IOverlay overlay) : IWidgetInitializer
{
    private readonly IOverlay _overlay = overlay;

    private IContextHandler? _handler;
    private WidgetView? _view;

    /// <summary>
    /// Rise Overlay keeps weapon class widgets off; World may still use them.
    /// </summary>
    public GameProcessType SupportedGames => GameProcessType.MonsterHunterWorld;

    public Task LoadAsync(IContext context)
    {
        if (!AnyClassWidgetEnabled(context.Process.Type))
            return Task.CompletedTask;

        ClassWidgetConfig config = ClientConfigHelper.DeferOverlayConfig(
            context.Process.Type,
            it => it.LongSwordWidget
        );

        if (!config.Initialize)
            return Task.CompletedTask;

        var viewModel = new ClassViewModel(config);
        _handler = new ClassWidgetContextHandler(
            context: context,
            viewModel: viewModel
        );

        _view = _overlay.Register(viewModel);

        return Task.CompletedTask;
    }

    public void Unload()
    {
        _overlay.Unregister(_view);
        _handler?.UnhookEvents();
        _handler = null;
    }

    private static bool AnyClassWidgetEnabled(GameProcessType game)
    {
        OverlayConfig overlay = ClientConfigHelper.GetOverlayConfigFrom(game);
        return overlay.LongSwordWidget.Initialize
               || overlay.ChargeBladeWidget.Initialize
               || overlay.InsectGlaiveWidget.Initialize
               || overlay.DualBladesWidget.Initialize
               || overlay.SwitchAxeWidget.Initialize;
    }
}
