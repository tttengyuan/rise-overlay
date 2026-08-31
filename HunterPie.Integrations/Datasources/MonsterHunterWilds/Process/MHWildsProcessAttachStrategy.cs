using HunterPie.Core.Domain.Enums;
using HunterPie.Core.Domain.Process;
using HunterPie.Core.Domain.Process.Service;
using HunterPie.Core.Extensions;
using HunterPie.Core.Game.Events;
using SystemProcess = System.Diagnostics.Process;

namespace HunterPie.Integrations.Datasources.MonsterHunterWilds.Process;

internal class MHWildsProcessAttachStrategy : IProcessAttachStrategy
{
    public string Name => SupportedGameNames.MONSTER_HUNTER_WILDS;

    public GameProcessType Game => GameProcessType.MonsterHunterWilds;

    public ProcessStatus Status
    {
        get;
        private set
        {
            if (field == value)
                return;

            ProcessStatus oldStatus = field;
            field = value;

            this.Dispatch(
                StatusChange,
                new SimpleValueChangeEventArgs<ProcessStatus>(oldStatus, value)
            );
        }
    }
    public event EventHandler<SimpleValueChangeEventArgs<ProcessStatus>>? StatusChange;

    public bool CanAttach(SystemProcess process)
    {
        // Rise Overlay: Wilds detection disabled (strategy kept so the solution still compiles).
        _ = process;
        return false;
    }

    public void SetStatus(ProcessStatus status) => Status = status;
}
