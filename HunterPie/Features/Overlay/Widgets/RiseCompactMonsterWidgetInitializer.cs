using HunterPie.Core.Client;
using HunterPie.Core.Client.Configuration.Overlay;
using HunterPie.Core.Client.Localization;
using HunterPie.Core.Domain.Enums;
using HunterPie.Core.Game;
using HunterPie.UI.Architecture.Overlay;
using HunterPie.UI.Overlay;
using HunterPie.UI.Overlay.Service;
using HunterPie.UI.Overlay.Views;
using RiseOverlay.Data;
using RiseOverlay.UI.Integration;
using RiseOverlay.UI.Overlay;
using System;
using System.Threading.Tasks;

namespace HunterPie.Features.Overlay.Widgets;

/// <summary>
/// Registers compact Rise monster HUD. Requires static JSON at
/// <c>{AppBase}/static/monsters-overlay.json</c> (copied from RiseOverlay.Data).
/// </summary>
internal class RiseCompactMonsterWidgetInitializer(
    IOverlay overlay,
    ILocalizationRepository localizationRepository) : IWidgetInitializer
{
    private readonly IOverlay _overlay = overlay;
    private readonly ILocalizationRepository _localizationRepository = localizationRepository;

    private RiseMonsterHudController? _handler;
    private WidgetView? _view;

    public GameProcessType SupportedGames => GameProcessType.MonsterHunterRise;

    public Task LoadAsync(IContext context)
    {
        RiseCompactMonsterWidgetConfig config = ClientConfigHelper.DeferOverlayConfig(
            game: context.Process.Type,
            overlay => ((MHROverlayConfig)overlay).RiseCompactMonsterWidget
        );

        if (!config.Initialize)
            return Task.CompletedTask;

        MonsterStaticStore store;
        try
        {
            store = MonsterStaticStore.Load(MonsterStaticStore.DefaultJsonPath());
        }
        catch (Exception)
        {
            // Still show live HP without static weakness table.
            store = MonsterStaticStore.LoadEmpty();
        }

        var viewModel = new RiseCompactMonsterViewModel(config);

        string LocalizePart(string id)
        {
            string path = $"//Strings/Monsters/Shared/Part[@Id='{id}']";
            return _localizationRepository.ExistsBy(path)
                ? _localizationRepository.FindStringBy(path)
                : id;
        }

        _handler = new RiseMonsterHudController(
            context: context,
            viewModel: viewModel,
            staticStore: store,
            localizePart: LocalizePart
        );

        _view = _overlay.Register(viewModel);
        return Task.CompletedTask;
    }

    public void Unload()
    {
        _overlay.Unregister(_view);
        _handler?.UnhookEvents();
        _handler = null;
        _view = null;
    }
}
