using HunterPie.Core.Game.Data.Definitions;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Enemy;

namespace RiseOverlay.Domain.Tests;

public sealed class MHRMonsterPartStateTests
{
    [Fact]
    public void Damage_refill_with_full_flinch_does_not_confirm_break()
    {
        MHRMonsterPart part = CreatePart("PART_HEAD", Sample(health: 500, maxHealth: 500));

        part.Update(Sample(health: 320, maxHealth: 500));
        part.Update(Sample(health: 500, maxHealth: 500));

        Assert.False(part.IsStructurallyBroken);
    }

    [Fact]
    public void Single_missing_break_pool_sample_does_not_confirm_break()
    {
        MHRMonsterPart part = CreatePart("PART_HEAD", Sample(health: 500, maxHealth: 500));

        part.Update(Sample(health: 0, maxHealth: 0));
        Assert.False(part.IsStructurallyBroken);

        part.Update(Sample(health: 500, maxHealth: 500));

        Assert.False(part.IsStructurallyBroken);
    }

    [Fact]
    public void Single_missing_sever_pool_sample_does_not_confirm_sever()
    {
        MHRMonsterPart part = CreatePart("PART_TAIL", Sample(
            health: 500,
            maxHealth: 500,
            flinch: 200,
            maxFlinch: 200,
            sever: 800,
            maxSever: 800));

        part.Update(Sample(
            health: 500,
            maxHealth: 500,
            flinch: 200,
            maxFlinch: 200,
            sever: 0,
            maxSever: 0));
        part.Update(Sample(
            health: 500,
            maxHealth: 500,
            flinch: 200,
            maxFlinch: 200,
            sever: 800,
            maxSever: 800));

        Assert.False(part.IsSeverLatched);
        Assert.True(part.HasSeverableEvidence);
    }

    [Fact]
    public void Break_cross_check_does_not_confirm_tail_sever()
    {
        MHRMonsterPart part = CreatePart("PART_TAIL", Sample(
            health: 500,
            maxHealth: 500,
            sever: 800,
            maxSever: 800));

        part.Update(Sample(
            health: 300,
            maxHealth: 500,
            sever: 800,
            maxSever: 800));
        part.Update(Sample(
            health: 500,
            maxHealth: 500,
            flinch: 120,
            maxFlinch: 200,
            sever: 800,
            maxSever: 800));

        Assert.True(part.IsBreakConfirmed);
        Assert.False(part.IsSeverConfirmed);
    }

    [Fact]
    public void Confirmed_break_stays_confirmed_while_flinch_bar_refills()
    {
        MHRMonsterPart part = CreatePart("PART_HEAD", Sample(health: 500, maxHealth: 500));

        part.Update(Sample(health: 280, maxHealth: 500));
        part.Update(Sample(health: 500, maxHealth: 500, flinch: 120, maxFlinch: 200));
        Assert.True(part.IsBreakConfirmed);

        part.Update(Sample(health: 500, maxHealth: 500, flinch: 200, maxFlinch: 200));

        Assert.True(part.IsBreakConfirmed);
        Assert.Equal(200, part.Flinch);
        Assert.Equal(200, part.MaxFlinch);
    }

    [Fact]
    public void Sever_damage_refill_with_flinch_cross_check_confirms_sever()
    {
        MHRMonsterPart part = CreatePart("PART_TAIL", Sample(
            health: 500,
            maxHealth: 500,
            sever: 800,
            maxSever: 800));

        part.Update(Sample(
            health: 500,
            maxHealth: 500,
            sever: 420,
            maxSever: 800));
        part.Update(Sample(
            health: 500,
            maxHealth: 500,
            flinch: 120,
            maxFlinch: 200,
            sever: 800,
            maxSever: 800));

        Assert.True(part.IsSeverConfirmed);
    }

    [Fact]
    public void Three_missing_sever_pool_samples_confirm_sever()
    {
        MHRMonsterPart part = CreatePart("PART_TAIL", Sample(
            health: 500,
            maxHealth: 500,
            sever: 800,
            maxSever: 800));

        part.Update(Sample(health: 500, maxHealth: 500));
        part.Update(Sample(health: 500, maxHealth: 500));
        Assert.False(part.IsSeverConfirmed);

        part.Update(Sample(health: 500, maxHealth: 500));
        Assert.True(part.IsSeverConfirmed);
    }

    private static MHRMonsterPart CreatePart(string id, MHRPartStructure initial)
    {
        var definition = new MonsterPartDefinition { String = id };
        var part = new MHRMonsterPart(definition, initial);
        part.Update(initial);
        return part;
    }

    private static MHRPartStructure Sample(
        float health,
        float maxHealth,
        float flinch = 200,
        float maxFlinch = 200,
        float sever = 0,
        float maxSever = 0)
        => new()
        {
            Health = health,
            MaxHealth = maxHealth,
            Flinch = flinch,
            MaxFlinch = maxFlinch,
            Sever = sever,
            MaxSever = maxSever
        };
}
