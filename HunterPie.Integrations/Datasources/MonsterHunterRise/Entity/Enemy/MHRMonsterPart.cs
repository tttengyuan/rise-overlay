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
    private bool _seenFullFlinch;
    private bool _breakLatched;
    private bool _severLatched;
    private float _breakLatchMaxHealth;

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
    /// Physical break/sever. Rise often keeps MaxHealth and refills Health after a break
    /// (HunterPie: Health==MaxHealth &amp;&amp; Flinch!=MaxFlinch). We latch so a regenerating
    /// flinch does not bring back 「可破」.
    /// </summary>
    public bool IsStructurallyBroken
    {
        get
        {
            if (Count > 0 || _breakLatched || _severLatched)
                return true;

            if (MaxHealth <= 0 && _seenBreakableHealth)
                return true;

            if (MaxHealth > 0 && Health <= 0)
                return true;

            // Live break edge: refilled after damage, or flinch drop while full after damage.
            if (_seenBreakableDamage
                && MaxHealth > 0
                && Health >= MaxHealth)
                return true;

            // Live sever edge.
            if (_seenFullFlinch
                && MaxSever > 0 && Sever >= MaxSever
                && MaxFlinch > 0 && Flinch < MaxFlinch)
                return true;

            return false;
        }
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

        if (data.MaxFlinch > 0 && data.Flinch >= data.MaxFlinch)
            _seenFullFlinch = true;

        // Breakable: Rise often refills Health to MaxHealth after a break instead of
        // zeroing MaxHealth. Afflicted Qurio cycles also churn part HP in memory — once
        // broken, stay broken unless MaxHealth grows (rare true multi-break pool).
        if (data.MaxHealth > 0)
        {
            _seenBreakableHealth = true;

            if (_breakLatched && data.MaxHealth > _breakLatchMaxHealth + 1f)
            {
                _breakLatched = false;
                _seenBreakableDamage = false;
                _breakLatchMaxHealth = 0;
            }

            if (!_breakLatched)
            {
                if (data.Health < data.MaxHealth)
                    _seenBreakableDamage = true;
                else if (_seenBreakableDamage)
                {
                    _breakLatched = true;
                    _breakLatchMaxHealth = data.MaxHealth;
                }
            }
        }
        else if (_seenBreakableHealth)
        {
            _breakLatched = true;
        }

        // Severable: Sever==MaxSever at hunt start is normal. Cut = flinch drop after a full flinch.
        if (_seenFullFlinch
            && data.MaxSever > 0 && data.Sever >= data.MaxSever
            && data.MaxFlinch > 0 && data.Flinch < data.MaxFlinch)
            _severLatched = true;

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
        if (structure.MaxSever > 0)
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