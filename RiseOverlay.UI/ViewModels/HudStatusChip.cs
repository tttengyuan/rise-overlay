namespace RiseOverlay.UI.ViewModels;

/// <summary>状态色块的语义类别，决定它在 HUD 上取用哪一支主题色。</summary>
public enum HudChipKind
{
    /// <summary>愤怒（剩余时间）。</summary>
    Enrage,

    /// <summary>晕眩（累积百分比，或生效中的倒计时）。</summary>
    Stun,

    /// <summary>耐力。</summary>
    Stamina,

    /// <summary>御龙（累积百分比，或生效中的倒计时）。</summary>
    Ride,

    /// <summary>异常状态（毒 / 麻 / 眠 …）。</summary>
    Ailment,
}

/// <summary>
/// 战斗 HUD 状态行上的一个色块。
/// <para>
/// 由 <see cref="MonsterHudViewModel"/> 从 DTO 的结构化字段构建，内容与同一行的纯文本
/// （<c>StatusLineText</c> / <c>AilmentsLineText</c>）完全等价，仅供 UI 做分段着色。
/// 那两个字符串属性保持不变，未使用本集合的宿主仍可正常显示。
/// </para>
/// </summary>
/// <param name="Label">状态名，如「愤怒」「晕眩」「毒」。</param>
/// <param name="Value">数值或倒计时，如「1:18」「7%」「生效」。</param>
/// <param name="Kind">语义类别，UI 据此选主题色。</param>
/// <param name="IsActive">该状态是否正在生效（异常生效中 / 晕眩中 / 御龙中）。</param>
public sealed record HudStatusChip(
    string Label,
    string Value,
    HudChipKind Kind,
    bool IsActive = false);
