namespace Duxel.Core;

// Pinned PresenceBadge icon mapping; tiny and extra-large scale the official10/20 glyphs.
internal static class UiPresenceBadgeIcons
{
    internal static UiMenuIcon Select(UiPresenceBadgeOptions options)
    {
        options.Validate();
        var variant = options.Status switch
        {
            UiPresenceStatus.Available => options.OutOfOffice ? 1 : 0,
            UiPresenceStatus.Away => options.OutOfOffice ? 8 : 2,
            UiPresenceStatus.Blocked => 3,
            UiPresenceStatus.Busy => options.OutOfOffice ? 9 : 4,
            UiPresenceStatus.DoNotDisturb => options.OutOfOffice ? 6 : 5,
            UiPresenceStatus.Offline => options.OutOfOffice ? 8 : 7,
            UiPresenceStatus.OutOfOffice => 8,
            UiPresenceStatus.Unknown => 9,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        var size = options.Size switch
        {
            UiBadgeSize.Tiny or UiBadgeSize.ExtraSmall => 0,
            UiBadgeSize.Small => 1, UiBadgeSize.Medium => 2,
            UiBadgeSize.Large or UiBadgeSize.ExtraLarge => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        return Painters[variant * 4 + size];
    }
    private static readonly UiMenuIcon[] Painters =
    [
        UiMenuIcon.Create<UiPresenceAvailableFilled10, UiPresenceAvailableFilled10>(),
        UiMenuIcon.Create<UiPresenceAvailableFilled12, UiPresenceAvailableFilled12>(),
        UiMenuIcon.Create<UiPresenceAvailableFilled16, UiPresenceAvailableFilled16>(),
        UiMenuIcon.Create<UiPresenceAvailableFilled20, UiPresenceAvailableFilled20>(),
        UiMenuIcon.Create<UiPresenceAvailableRegular10, UiPresenceAvailableRegular10>(),
        UiMenuIcon.Create<UiPresenceAvailableRegular12, UiPresenceAvailableRegular12>(),
        UiMenuIcon.Create<UiPresenceAvailableRegular16, UiPresenceAvailableRegular16>(),
        UiMenuIcon.Create<UiPresenceAvailableRegular20, UiPresenceAvailableRegular20>(),
        UiMenuIcon.Create<UiPresenceAwayFilled10, UiPresenceAwayFilled10>(),
        UiMenuIcon.Create<UiPresenceAwayFilled12, UiPresenceAwayFilled12>(),
        UiMenuIcon.Create<UiPresenceAwayFilled16, UiPresenceAwayFilled16>(),
        UiMenuIcon.Create<UiPresenceAwayFilled20, UiPresenceAwayFilled20>(),
        UiMenuIcon.Create<UiPresenceBlockedRegular10, UiPresenceBlockedRegular10>(),
        UiMenuIcon.Create<UiPresenceBlockedRegular12, UiPresenceBlockedRegular12>(),
        UiMenuIcon.Create<UiPresenceBlockedRegular16, UiPresenceBlockedRegular16>(),
        UiMenuIcon.Create<UiPresenceBlockedRegular20, UiPresenceBlockedRegular20>(),
        UiMenuIcon.Create<UiPresenceBusyFilled10, UiPresenceBusyFilled10>(),
        UiMenuIcon.Create<UiPresenceBusyFilled12, UiPresenceBusyFilled12>(),
        UiMenuIcon.Create<UiPresenceBusyFilled16, UiPresenceBusyFilled16>(),
        UiMenuIcon.Create<UiPresenceBusyFilled20, UiPresenceBusyFilled20>(),
        UiMenuIcon.Create<UiPresenceDndFilled10, UiPresenceDndFilled10>(),
        UiMenuIcon.Create<UiPresenceDndFilled12, UiPresenceDndFilled12>(),
        UiMenuIcon.Create<UiPresenceDndFilled16, UiPresenceDndFilled16>(),
        UiMenuIcon.Create<UiPresenceDndFilled20, UiPresenceDndFilled20>(),
        UiMenuIcon.Create<UiPresenceDndRegular10, UiPresenceDndRegular10>(),
        UiMenuIcon.Create<UiPresenceDndRegular12, UiPresenceDndRegular12>(),
        UiMenuIcon.Create<UiPresenceDndRegular16, UiPresenceDndRegular16>(),
        UiMenuIcon.Create<UiPresenceDndRegular20, UiPresenceDndRegular20>(),
        UiMenuIcon.Create<UiPresenceOfflineRegular10, UiPresenceOfflineRegular10>(),
        UiMenuIcon.Create<UiPresenceOfflineRegular12, UiPresenceOfflineRegular12>(),
        UiMenuIcon.Create<UiPresenceOfflineRegular16, UiPresenceOfflineRegular16>(),
        UiMenuIcon.Create<UiPresenceOfflineRegular20, UiPresenceOfflineRegular20>(),
        UiMenuIcon.Create<UiPresenceOofRegular10, UiPresenceOofRegular10>(),
        UiMenuIcon.Create<UiPresenceOofRegular12, UiPresenceOofRegular12>(),
        UiMenuIcon.Create<UiPresenceOofRegular16, UiPresenceOofRegular16>(),
        UiMenuIcon.Create<UiPresenceOofRegular20, UiPresenceOofRegular20>(),
        UiMenuIcon.Create<UiPresenceUnknownRegular10, UiPresenceUnknownRegular10>(),
        UiMenuIcon.Create<UiPresenceUnknownRegular12, UiPresenceUnknownRegular12>(),
        UiMenuIcon.Create<UiPresenceUnknownRegular16, UiPresenceUnknownRegular16>(),
        UiMenuIcon.Create<UiPresenceUnknownRegular20, UiPresenceUnknownRegular20>(),
    ];
}

[UiFluentIcon("presence_available", UiFluentIconStyle.Filled, 10)] internal readonly partial struct UiPresenceAvailableFilled10;
[UiFluentIcon("presence_available", UiFluentIconStyle.Filled, 12)] internal readonly partial struct UiPresenceAvailableFilled12;
[UiFluentIcon("presence_available", UiFluentIconStyle.Filled, 16)] internal readonly partial struct UiPresenceAvailableFilled16;
[UiFluentIcon("presence_available", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiPresenceAvailableFilled20;
[UiFluentIcon("presence_available", UiFluentIconStyle.Regular, 10)] internal readonly partial struct UiPresenceAvailableRegular10;
[UiFluentIcon("presence_available", UiFluentIconStyle.Regular, 12)] internal readonly partial struct UiPresenceAvailableRegular12;
[UiFluentIcon("presence_available", UiFluentIconStyle.Regular, 16)] internal readonly partial struct UiPresenceAvailableRegular16;
[UiFluentIcon("presence_available", UiFluentIconStyle.Regular, 20)] internal readonly partial struct UiPresenceAvailableRegular20;
[UiFluentIcon("presence_away", UiFluentIconStyle.Filled, 10)] internal readonly partial struct UiPresenceAwayFilled10;
[UiFluentIcon("presence_away", UiFluentIconStyle.Filled, 12)] internal readonly partial struct UiPresenceAwayFilled12;
[UiFluentIcon("presence_away", UiFluentIconStyle.Filled, 16)] internal readonly partial struct UiPresenceAwayFilled16;
[UiFluentIcon("presence_away", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiPresenceAwayFilled20;
[UiFluentIcon("presence_blocked", UiFluentIconStyle.Regular, 10)] internal readonly partial struct UiPresenceBlockedRegular10;
[UiFluentIcon("presence_blocked", UiFluentIconStyle.Regular, 12)] internal readonly partial struct UiPresenceBlockedRegular12;
[UiFluentIcon("presence_blocked", UiFluentIconStyle.Regular, 16)] internal readonly partial struct UiPresenceBlockedRegular16;
[UiFluentIcon("presence_blocked", UiFluentIconStyle.Regular, 20)] internal readonly partial struct UiPresenceBlockedRegular20;
[UiFluentIcon("presence_busy", UiFluentIconStyle.Filled, 10)] internal readonly partial struct UiPresenceBusyFilled10;
[UiFluentIcon("presence_busy", UiFluentIconStyle.Filled, 12)] internal readonly partial struct UiPresenceBusyFilled12;
[UiFluentIcon("presence_busy", UiFluentIconStyle.Filled, 16)] internal readonly partial struct UiPresenceBusyFilled16;
[UiFluentIcon("presence_busy", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiPresenceBusyFilled20;
[UiFluentIcon("presence_dnd", UiFluentIconStyle.Filled, 10)] internal readonly partial struct UiPresenceDndFilled10;
[UiFluentIcon("presence_dnd", UiFluentIconStyle.Filled, 12)] internal readonly partial struct UiPresenceDndFilled12;
[UiFluentIcon("presence_dnd", UiFluentIconStyle.Filled, 16)] internal readonly partial struct UiPresenceDndFilled16;
[UiFluentIcon("presence_dnd", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiPresenceDndFilled20;
[UiFluentIcon("presence_dnd", UiFluentIconStyle.Regular, 10)] internal readonly partial struct UiPresenceDndRegular10;
[UiFluentIcon("presence_dnd", UiFluentIconStyle.Regular, 12)] internal readonly partial struct UiPresenceDndRegular12;
[UiFluentIcon("presence_dnd", UiFluentIconStyle.Regular, 16)] internal readonly partial struct UiPresenceDndRegular16;
[UiFluentIcon("presence_dnd", UiFluentIconStyle.Regular, 20)] internal readonly partial struct UiPresenceDndRegular20;
[UiFluentIcon("presence_offline", UiFluentIconStyle.Regular, 10)] internal readonly partial struct UiPresenceOfflineRegular10;
[UiFluentIcon("presence_offline", UiFluentIconStyle.Regular, 12)] internal readonly partial struct UiPresenceOfflineRegular12;
[UiFluentIcon("presence_offline", UiFluentIconStyle.Regular, 16)] internal readonly partial struct UiPresenceOfflineRegular16;
[UiFluentIcon("presence_offline", UiFluentIconStyle.Regular, 20)] internal readonly partial struct UiPresenceOfflineRegular20;
[UiFluentIcon("presence_oof", UiFluentIconStyle.Regular, 10)] internal readonly partial struct UiPresenceOofRegular10;
[UiFluentIcon("presence_oof", UiFluentIconStyle.Regular, 12)] internal readonly partial struct UiPresenceOofRegular12;
[UiFluentIcon("presence_oof", UiFluentIconStyle.Regular, 16)] internal readonly partial struct UiPresenceOofRegular16;
[UiFluentIcon("presence_oof", UiFluentIconStyle.Regular, 20)] internal readonly partial struct UiPresenceOofRegular20;
[UiFluentIcon("presence_unknown", UiFluentIconStyle.Regular, 10)] internal readonly partial struct UiPresenceUnknownRegular10;
[UiFluentIcon("presence_unknown", UiFluentIconStyle.Regular, 12)] internal readonly partial struct UiPresenceUnknownRegular12;
[UiFluentIcon("presence_unknown", UiFluentIconStyle.Regular, 16)] internal readonly partial struct UiPresenceUnknownRegular16;
[UiFluentIcon("presence_unknown", UiFluentIconStyle.Regular, 20)] internal readonly partial struct UiPresenceUnknownRegular20;
