using GagSpeak.Services.Mediator;
using System.Numerics;

namespace GagSpeak.UI.Profile;

/// <summary>
/// The UI Design for the KinkPlates.
/// </summary>
public partial class KinkPlateUI : WindowMediatorSubscriberBase
{
    private Vector2 RectMin { get; set; } = Vector2.Zero;
    private Vector2 RectMax { get; set; } = Vector2.Zero;

    // Left Side
    private Vector2 CloseButtonPos => RectMin + Vector2.One * 16f;
    private Vector2 CloseButtonSize => Vector2.One * 24f;

    private Vector2 ProfilePictureBorderPos => RectMin + Vector2.One * 12f;
    private Vector2 ProfilePictureBorderSize => Vector2.One * 224f;

    private Vector2 ProfilePicturePos => RectMin + Vector2.One * 16f;
    private Vector2 ProfilePictureSize => Vector2.One * 216f;

    private Vector2 SupporterIconBorderPos => RectMin + new Vector2(182, 16);
    private Vector2 SupporterIconBorderSize => Vector2.One * 52f;

    private Vector2 SupporterIconPos => RectMin + new Vector2(177, 21);
    private Vector2 SupporterIconSize => Vector2.One * 54f;

    private Vector2 IconOverviewListPos => RectMin + new Vector2(12, 290);
    private Vector2 IconOverviewListSize => new Vector2(224, 34);

    private Vector2 DescriptionBorderPos => RectMin + new Vector2(12, 331);
    private Vector2 DescriptionBorderSize => new Vector2(550, 108);

    // Center Middle
    private Vector2 TitleLineStartPos => RectMin + new Vector2(247, 76);
    private Vector2 TitleLineEndPos => new Vector2(563, 76);

    private Vector2 GagSlotOneBorderPos => RectMin + new Vector2(257, 94);
    private Vector2 GagSlotTwoBorderPos => RectMin + new Vector2(360, 94);
    private Vector2 GagSlotThreeBorderPos => RectMin + new Vector2(464, 94);
    private Vector2 GagSlotBorderSize => Vector2.One * 85f;

    private Vector2 GagSlotOnePos => RectMin + new Vector2(261, 98);
    private Vector2 GagSlotTwoPos => RectMin + new Vector2(364, 98);
    private Vector2 GagSlotThreePos => RectMin + new Vector2(468, 98);
    private Vector2 GagSlotSize => Vector2.One * 77f;

    private Vector2 GagLockOneBorderPos => RectMin + new Vector2(270, 166);
    private Vector2 GagLockTwoBorderPos => RectMin + new Vector2(373, 166);
    private Vector2 GagLockThreeBorderPos => RectMin + new Vector2(477, 166);
    private Vector2 GagLockBorderSize => Vector2.One * 59f;

    private Vector2 GagLockOnePos => RectMin + new Vector2(274, 170);
    private Vector2 GagLockTwoPos => RectMin + new Vector2(377, 170);
    private Vector2 GagLockThreePos => RectMin + new Vector2(481, 170);
    private Vector2 GagLockSize => Vector2.One * 55f;

    private Vector2 StatsPos => RectMin + new Vector2(392, 307);

    // Right Side.
    private Vector2 LockedSlotsPanelBorderPos => RectMin + new Vector2(572, 13);
    private Vector2 LockedSlotsPanelBorderSize => new Vector2(163, 423);

    private Vector2 LockedSlotsPanelPos => RectMin + new Vector2(576, 17);
    private Vector2 LockedSlotsPanelSize => new Vector2(155, 415);

    private Vector2 LockAffectersRowPos => RectMin + new Vector2(597, 23);
    private Vector2 LockAffectersRowSize => new Vector2(114, 27);

    private Vector2 LockedSlotsGroupPos => RectMin + new Vector2(588, 56);
    private Vector2 LockedSlotsGroupSize => new Vector2(132, 348);

    private Vector2 HardcoreTraitsRowPos => RectMin + new Vector2(586, 407);
    private Vector2 HardcoreTraitsRowSize => new Vector2(136, 20);
}
