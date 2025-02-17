using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DragaliaAPI.Database.Entities.Abstract;
using DragaliaAPI.Shared.Definitions.Enums;
using DragaliaAPI.Shared.Features.Summoning;
using DragaliaAPI.Shared.MasterAsset;
using DragaliaAPI.Shared.MasterAsset.Models;
using Microsoft.EntityFrameworkCore;

namespace DragaliaAPI.Database.Entities;

[Table("PlayerCharaData")]
[PrimaryKey(nameof(ViewerId), nameof(CharaId))]
public class DbPlayerCharaData : DbPlayerData
{
    [Column("CharaId")]
    [TypeConverter(typeof(EnumConverter))]
    public required Charas CharaId { get; set; }

    [Column("Rarity")]
    public required byte Rarity { get; set; }

    [Column("Exp")]
    public int Exp { get; set; } = 0;

    [Column("Level")]
    public byte Level { get; set; } = 1;

    // Divides unlocked node count by nodes/limit breaks that increase max Level
    // Only if the unlockCount is equal or higher than the divisor the quotient will return a 1, adding 5 levels for that step
    [NotMapped]
    public byte AdditionalMaxLevel
    {
        get
        {
            return (byte)(
                ((ManaNodeUnlockCount / (ushort)ManaNodes.Circle5) * 5)
                + (
                    (
                        ManaNodeUnlockCount
                        / (ushort)(
                            ManaNodes.Circle5
                            | ManaNodes.Node1
                            | ManaNodes.Node2
                            | ManaNodes.Node3
                            | ManaNodes.Node4
                            | ManaNodes.Node5
                        )
                    ) * 5
                )
                + ((ManaNodeUnlockCount / (ushort)ManaNodes.Circle6) * 5)
                + (
                    (
                        ManaNodeUnlockCount
                        / (ushort)(
                            ManaNodes.Circle6
                            | ManaNodes.Node1
                            | ManaNodes.Node2
                            | ManaNodes.Node3
                            | ManaNodes.Node4
                            | ManaNodes.Node5
                        )
                    ) * 5
                )
            );
        }
    }

    [Column("HpPlusCount")]
    public byte HpPlusCount { get; set; } = 0;

    [Column("AtkPlusCount")]
    public byte AttackPlusCount { get; set; } = 0;

    [NotMapped]
    public byte LimitBreakCount
    {
        get => (byte)Math.Min(ManaNodeUnlockCount >> 10, ManaNodesUtil.MaxLimitbreakSpiral);
        set => ManaNodeUnlockCount = (ushort)(value << 10);
    }

    [Column("IsNew")]
    [TypeConverter(typeof(BooleanConverter))]
    public bool IsNew { get; set; } = true;

    [Column("Skill1Lvl")]
    public byte Skill1Level { get; set; } = 1;

    [Column("Skill2Lvl")]
    public byte Skill2Level { get; set; } = 0;

    [Column("Abil1Lvl")]
    public byte Ability1Level { get; set; } = 1;

    [Column("Abil2Lvl")]
    public byte Ability2Level { get; set; }

    [Column("Abil3Lvl")]
    public byte Ability3Level { get; set; }

    [Column("BurstAtkLvl")]
    public byte BurstAttackLevel { get; set; }

    // For some reason this is what the standard attack node upgrade is called
    [Column("ComboBuildupCount")]
    public int ComboBuildupCount { get; set; }

    [Column("HpBase")]
    public required ushort HpBase { get; set; }

    [Column("HpNode")]
    public ushort HpNode { get; set; } = 0;

    [NotMapped]
    public int Hp => this.HpBase + this.HpNode;

    [Column("AtkBase")]
    public required ushort AttackBase { get; set; }

    [Column("AtkNode")]
    public ushort AttackNode { get; set; } = 0;

    [NotMapped]
    public int Attack
    {
        get => AttackBase + AttackNode;
    }

    [Column("ExAbility1Lvl")]
    public byte ExAbilityLevel { get; set; } = 1;

    [Column("ExAbility2Lvl")]
    public byte ExAbility2Level { get; set; } = 1;

    [Column("IsTemp")]
    public bool IsTemporary { get; set; }

    [Column("IsUnlockEditSkill")]
    public bool IsUnlockEditSkill { get; set; }

    [Column("ManaNodeUnlockCount")]
    public ushort ManaNodeUnlockCount { get; private set; }

    [Column("ListViewFlag")]
    public bool ListViewFlag { get; set; }

    [Column("GetTime")]
    public DateTimeOffset GetTime { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public SortedSet<int> ManaCirclePieceIdList
    {
        get => ManaNodesUtil.GetSetFromManaNodes((ManaNodes)ManaNodeUnlockCount);
        set =>
            ManaNodeUnlockCount = (ushort)
                ManaNodesUtil.SetManaCircleNodesFromSet(value, (ManaNodes)ManaNodeUnlockCount);
    }

    /// <summary>
    /// EF Core / test constructor.
    /// </summary>
    public DbPlayerCharaData() { }

    /// <summary>
    /// User-facing constructor.
    /// </summary>
    /// <param name="deviceAccountId">Primary key.</param>
    [SetsRequiredMembers]
    public DbPlayerCharaData(long viewerId, Charas id)
    {
        CharaData data = MasterAsset.CharaData.Get(id);

        byte rarity = (byte)data.Rarity;
        ushort rarityHp;
        ushort rarityAtk;

        switch (rarity)
        {
            case 3:
                rarityHp = (ushort)data.MinHp3;
                rarityAtk = (ushort)data.MinAtk3;
                break;
            case 4:
                rarityHp = (ushort)data.MinHp4;
                rarityAtk = (ushort)data.MinAtk4;
                break;
            case 5:
                rarityHp = (ushort)data.MinHp5;
                rarityAtk = (ushort)data.MinAtk5;
                break;
            default:
                throw new UnreachableException("Invalid rarity!");
        }

        this.ViewerId = viewerId;
        this.Rarity = rarity;
        this.CharaId = id;
        this.HpBase = rarityHp;
        this.AttackBase = rarityAtk;
        this.BurstAttackLevel = (byte)data.DefaultBurstAttackLevel;
        this.Ability1Level = (byte)data.DefaultAbility1Level;
        this.Ability2Level = (byte)data.DefaultAbility2Level;
        this.Ability3Level = (byte)data.DefaultAbility3Level;
        this.IsUnlockEditSkill = data.GetAvailability() == UnitAvailability.Story;
    }
}
