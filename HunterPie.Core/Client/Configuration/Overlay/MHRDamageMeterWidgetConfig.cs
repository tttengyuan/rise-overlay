namespace HunterPie.Core.Client.Configuration.Overlay;

/// <summary>
/// Rise Overlay: compact CombatStack DPS panel replaces the chart Damage Meter.
/// Default Initialize=false (persisted configs may still have true until reset).
/// </summary>
public class MHRDamageMeterWidgetConfig : DamageMeterWidgetConfig
{
    public MHRDamageMeterWidgetConfig()
    {
        Initialize = false;
    }
}
