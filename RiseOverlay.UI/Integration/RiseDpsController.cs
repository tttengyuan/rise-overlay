using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Entity.Party;
using HunterPie.Core.Game.Enums;
using HunterPie.Core.Game.Events;
using HunterPie.UI.Overlay;
using RiseOverlay.Domain;
using RiseOverlay.UI.Overlay;

namespace RiseOverlay.UI.Integration;

/// <summary>
/// Wires Rise party <see cref="IPartyMember"/> damage into compact <see cref="ViewModels.DpsPanelViewModel"/>.
/// DPS = totalDamage / max(1, questElapsedSeconds), same as Task 11 spec.
/// </summary>
public sealed class RiseDpsController : IContextHandler, IDisposable
{
    private readonly IContext _context;
    private readonly RiseCompactMonsterViewModel _viewModel;
    private readonly HashSet<IPartyMember> _members = new();
    private double _timeElapsed;

    public RiseDpsController(IContext context, RiseCompactMonsterViewModel viewModel)
    {
        _context = context;
        _viewModel = viewModel;
        _timeElapsed = context.Game.TimeElapsed;

        HookEvents();
        SyncExistingMembers();
        PushPanel();
    }

    public void HookEvents()
    {
        _context.Game.Player.Party.OnMemberJoin += OnMemberJoin;
        _context.Game.Player.Party.OnMemberLeave += OnMemberLeave;
        _context.Game.OnTimeElapsedChange += OnTimeElapsedChange;
        _context.Game.OnQuestStart += OnQuestStart;
        _context.Game.OnQuestEnd += OnQuestEnd;
        _context.Game.Player.OnStageUpdate += OnStageUpdate;
    }

    public void UnhookEvents()
    {
        _context.Game.Player.Party.OnMemberJoin -= OnMemberJoin;
        _context.Game.Player.Party.OnMemberLeave -= OnMemberLeave;
        _context.Game.OnTimeElapsedChange -= OnTimeElapsedChange;
        _context.Game.OnQuestStart -= OnQuestStart;
        _context.Game.OnQuestEnd -= OnQuestEnd;
        _context.Game.Player.OnStageUpdate -= OnStageUpdate;

        foreach (var member in _members.ToArray())
            DetachMember(member);
        _members.Clear();
    }

    public void Dispose() => UnhookEvents();

    private void SyncExistingMembers()
    {
        foreach (IPartyMember member in _context.Game.Player.Party.Members)
            TryAttachMember(member);
    }

    private void OnMemberJoin(object? sender, IPartyMember e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            TryAttachMember(e);
            PushPanel();
        });

    private void OnMemberLeave(object? sender, IPartyMember e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            DetachMember(e);
            PushPanel();
        });

    private void OnDamageDealt(object? sender, IPartyMember e)
        => _viewModel.UIThread.BeginInvoke(PushPanel);

    private void OnTimeElapsedChange(object? sender, TimeElapsedChangeEventArgs e)
    {
        // Throttle UI refresh ~0.5s (matches DamageMeterControllerV2 cadence).
        const double precision = 0.5;
        double lastBucket = _timeElapsed % precision;
        double newBucket = e.TimeElapsed % precision;
        _timeElapsed = e.TimeElapsed;

        if (!e.IsTimerReset && newBucket >= lastBucket)
            return;

        _viewModel.UIThread.BeginInvoke(PushPanel);
    }

    private void OnQuestStart(object? sender, IQuest e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            ClearMembers();
            SyncExistingMembers();
            _timeElapsed = _context.Game.TimeElapsed;
            PushPanel();
        });

    private void OnQuestEnd(object? sender, QuestEndEventArgs e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            // Keep members so final scores stay visible until stage/quest restart clears them.
            _timeElapsed = e.TimeElapsed.TotalSeconds;
            PushPanel();
        });

    private void OnStageUpdate(object? sender, EventArgs e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            if (_context.Game.Quest is not null)
                return;

            ClearMembers();
            SyncExistingMembers();
            _timeElapsed = _context.Game.TimeElapsed;
            PushPanel();
        });

    private void TryAttachMember(IPartyMember member)
    {
        if (member.Type is not (MemberType.Player or MemberType.Companion))
            return;

        if (!_members.Add(member))
            return;

        member.OnDamageDealt += OnDamageDealt;
    }

    private void DetachMember(IPartyMember member)
    {
        if (!_members.Remove(member))
            return;

        member.OnDamageDealt -= OnDamageDealt;
    }

    private void ClearMembers()
    {
        foreach (var member in _members.ToArray())
            DetachMember(member);
        _members.Clear();
    }

    private void PushPanel()
    {
        var snapshots = _members
            .Select(m => new DpsMemberSnapshot(
                Name: m.Name,
                IsSelf: m.IsMyself,
                TotalDamage: m.Damage))
            .ToArray();

        var dto = DpsPanelMapper.FromSnapshots(snapshots, _timeElapsed);
        _viewModel.DpsPanel.ApplyDto(dto);
    }
}
