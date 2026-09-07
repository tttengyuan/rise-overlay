using HunterPie.Core.Architecture.Events;
using HunterPie.Core.Domain.Interfaces;
using HunterPie.Core.Extensions;
using HunterPie.Core.Game.Data.Definitions;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Enums;
using HunterPie.Integrations.Datasources.Common.Entity.Enemy;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Enemy;

public sealed class MHRMonsterPart : CommonPart, IUpdatable<MHRPartStructure>, IUpdatable<MHRQurioPartData>
{
    private float _health;
    private float _flinch;
    private float _sever;
    private PartType _type;
    private bool _isInQurio;
    private bool _seenBreakableHealth;
    private bool _seenBreakableDamage;
    private bool _seenSeverableHealth;
    private bool _seenSeverableDamage;
    private bool _breakConfirmed;
    private bool _severConfirmed;
    private int _missingBreakPoolSamples;
    private int _missingSeverPoolSamples;

    private const int MissingPoolConfirmationSamples = 3;

    public override string Id { get; protected set; }

    public override float Health
    {
        get => _health;
        protected set
        {
            if (value != _health)
            {
                _health = value;
                this.Dispatch(_onHealthUpdate, this);
            }
        }
    }

    public override float MaxHealth { get; protected set; }

    public override float Flinch
    {
        get => _flinch;
        protected set
        {
            if (value != _flinch)
            {
                _flinch = value;
                this.Dispatch(_onFlinchUpdate, this);
            }
        }
    }

    public override float MaxFlinch { get; protected set; }

    public override float Tenderize { get; protected set; }
    public override float MaxTenderize { get; protected set; }

    public override float Sever
    {
        get => _sever;
        protected set
        {
            if (value != _sever)
            {
                _sever = value;
                this.Dispatch(_onSeverUpdate, this);
            }
        }
    }

    public override float MaxSever { get; protected set; }

    public float QurioHealth
    {
        get;
        private set
        {
            if (value != field)
            {
                field = value;
                this.Dispatch(_onQurioHealthChange, this);
            }
        }
    }

    public float QurioMaxHealth { get; private set; }

    /// <summary>True only while the Qurio infestation is active on this part.</summary>
    public bool IsQurioInfected => _isInQurio;

    /// <summary>
    /// Confirmed physical break/sever. The break and sever pools are tracked independently;
    /// a tail break must never be promoted to a sever.
    /// </summary>
    public bool IsStructurallyBroken => Count > 0 || _breakConfirmed || _severConfirmed;

    public bool IsBreakConfirmed => _breakConfirmed;

    public bool IsSeverConfirmed => _severConfirmed;

    /// <summary>True after this tail has exposed a real sever pool at least once.</summary>
    public bool HasSeverableEvidence => _seenSeverableHealth;

    /// <summary>True after a cuttable tail has been severed (latched).</summary>
    public bool IsSeverLatched => _severConfirmed;

    private bool IsCuttableTailPart => IsCuttableTailPartId(Id);

    /// <summary>
    /// Rise cuttable tails only. Mud/snow/rock coatings on the tail are breakables —
    /// they must never show 「可断/已断尾」.
    /// </summary>
    public static bool IsCuttableTailPartId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        if (!id.Contains("TAIL", StringComparison.OrdinalIgnoreCase))
            return false;
        if (id.Contains("MUD", StringComparison.OrdinalIgnoreCase))
            return false;
        if (id.Contains("SNOW", StringComparison.OrdinalIgnoreCase))
            return false;
        if (id.Contains("ROCK", StringComparison.OrdinalIgnoreCase))
            return false;
        if (id.Contains("ICE", StringComparison.OrdinalIgnoreCase))
            return false;
        if (id.Contains("WINDSAC", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public override PartType Type
    {
        get => _type;
        protected set
        {
            if (value != _type)
            {
                _type = value;
                this.Dispatch(_onPartTypeChange, this);
            }
        }
    }

    public override int Count { get; protected set; }

    private readonly SmartEvent<IMonsterPart> _onQurioHealthChange = new();
    public event EventHandler<IMonsterPart> OnQurioHealthChange
    {
        add => _onQurioHealthChange.Hook(value);
        remove => _onQurioHealthChange.Unhook(value);
    }

    public MHRMonsterPart(MonsterPartDefinition definition) : base(definition)
    {
        Id = definition.String;
    }

    public MHRMonsterPart(MonsterPartDefinition definition, MHRPartStructure structure) : base(definition)
    {
        Id = definition.String;

        GetCurrentType(structure);
    }

    public void Update(MHRPartStructure data)
    {
        if (Type == PartType.Qurio && !_isInQurio)
            GetCurrentType(data);

        bool hasFlinchBreakEvidence = data.MaxFlinch > 0 && data.Flinch < data.MaxFlinch;

        // Keep HunterPie's cross-check (damaged pool refilled while flinch is not full),
        // but reject one-frame pool churn. A plain damage -> refill with a full flinch bar
        // is not enough evidence to call the part broken.
        if (data.MaxHealth > 0)
        {
            _seenBreakableHealth = true;
            _missingBreakPoolSamples = 0;

            if (!_breakConfirmed)
            {
                if (data.Health < data.MaxHealth)
                    _seenBreakableDamage = true;
                else if (_seenBreakableDamage && hasFlinchBreakEvidence)
                    _breakConfirmed = true;
            }
        }
        else if (_seenBreakableHealth && !_breakConfirmed)
        {
            _missingBreakPoolSamples++;
            if (_missingBreakPoolSamples >= MissingPoolConfirmationSamples)
                _breakConfirmed = true;
        }

        // Sever is independent from break health. Require observed sever damage before the
        // original Sever==MaxSever + flinch cross-check, or a stable missing sever pool.
        if (IsCuttableTailPart)
        {
            if (data.MaxSever > 0)
            {
                _seenSeverableHealth = true;
                _missingSeverPoolSamples = 0;

                if (!_severConfirmed)
                {
                    if (data.Sever < data.MaxSever)
                        _seenSeverableDamage = true;
                    else if (_seenSeverableDamage && hasFlinchBreakEvidence)
                        _severConfirmed = true;
                }
            }
            else if (_seenSeverableHealth && !_severConfirmed)
            {
                _missingSeverPoolSamples++;
                if (_missingSeverPoolSamples >= MissingPoolConfirmationSamples)
                    _severConfirmed = true;
            }

            if (data.MaxSever > 0 && Type != PartType.Qurio)
                Type = PartType.Severable;
        }

        MaxHealth = data.MaxHealth;
        Health = data.Health;
        MaxFlinch = data.MaxFlinch;
        Flinch = data.Flinch;
        MaxSever = data.MaxSever;
        Sever = data.Sever;
    }

    public void Update(MHRQurioPartData data)
    {
        switch (data.IsInQurioState)
        {
            case false when Type != PartType.Qurio:
                return;
            case false when Type == PartType.Qurio:
                _isInQurio = false;
                return;
        }

        Type = PartType.Qurio;
        QurioMaxHealth = Math.Max(data.MaxHealth, Math.Max(data.Health, QurioMaxHealth));
        QurioHealth = data.Health;
        _isInQurio = data.IsInQurioState;
    }

    private void GetCurrentType(MHRPartStructure structure)
    {
        // Only cuttable tails are Severable. MaxSever noise on other parts → Breakable/Flinch.
        if (IsCuttableTailPart && structure.MaxSever > 0)
            Type = PartType.Severable;
        else if (structure.MaxHealth > 0)
            Type = PartType.Breakable;
        else if (structure.MaxFlinch > 0)
            Type = PartType.Flinch;
    }

    public override void Dispose()
    {
        _onQurioHealthChange.Dispose();
        base.Dispose();
    }
}
