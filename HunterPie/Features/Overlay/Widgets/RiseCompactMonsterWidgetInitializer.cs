using HunterPie.Core.Client;
using HunterPie.Core.Client.Configuration.Overlay;
using HunterPie.Core.Client.Localization;
using HunterPie.Core.Domain.Enums;
using HunterPie.Core.Game;
using HunterPie.Core.Observability.Logging;
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
    private readonly ILogger _logger = LoggerFactory.Create();
    private readonly IOverlay _overlay = overlay;
    private readonly ILocalizationRepository _localizationRepository = localizationRepository;

    private RiseMonsterHudController? _monsterHandler;
    private RiseDpsController? _dpsHandler;
    private QuestBriefingController? _briefingHandler;
    private WidgetView? _view;

    public GameProcessType SupportedGames => GameProcessType.MonsterHunterRise;

    public Task LoadAsync(IContext context)
    {
        RiseCompactMonsterWidgetConfig config = ClientConfigHelper.DeferOverlayConfig(
            game: context.Process.Type,
            overlay => ((MHROverlayConfig)overlay).RiseCompactMonsterWidget
        );
        DamageMeterWidgetConfig damageConfig = ClientConfigHelper.DeferOverlayConfig(
            game: context.Process.Type,
            overlay => overlay.DamageMeterWidget
        );

        if (!config.Initialize)
        {
            _logger.Info("RiseCompactMonsterWidget Initialize=false — skipped");
            return Task.CompletedTask;
        }

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

        QuestStaticStore questStore;
        try
        {
            questStore = QuestStaticStore.Load(QuestStaticStore.DefaultJsonPath());
        }
        catch (Exception)
        {
            questStore = QuestStaticStore.LoadEmpty();
        }

        var viewModel = new RiseCompactMonsterViewModel(config);
        var completionClock = new RiseOverlay.Domain.QuestCompletionClock();

        string LocalizePart(string id)
        {
            string path = $"//Strings/Monsters/Shared/Part[@Id='{id}']";
            string raw = _localizationRepository.ExistsBy(path)
                ? _localizationRepository.FindStringBy(path)
                : id;
            string cleaned = RiseOverlay.Domain.PartNameSanitizer.Clean(raw);
            return string.IsNullOrWhiteSpace(cleaned) ? raw : cleaned;
        }

        _monsterHandler = new RiseMonsterHudController(
            context: context,
            viewModel: viewModel,
            staticStore: store,
            localizePart: LocalizePart,
            questStore: questStore
        );
        _briefingHandler = new QuestBriefingController(context, viewModel, store, questStore, completionClock);
        _dpsHandler = new RiseDpsController(context, viewModel, damageConfig, completionClock);

        _view = _overlay.Register(viewModel);
        _logger.Info(
            $"Rise compact overlay registered at ({config.Position.X:0},{config.Position.Y:0}) (briefing + combat HUD)");
        return Task.CompletedTask;
    }

    public void Unload()
    {
        _overlay.Unregister(_view);
        _briefingHandler?.UnhookEvents();
        _monsterHandler?.UnhookEvents();
        _dpsHandler?.UnhookEvents();
        _briefingHandler = null;
        _monsterHandler = null;
        _dpsHandler = null;
        _view = null;
    }
}
