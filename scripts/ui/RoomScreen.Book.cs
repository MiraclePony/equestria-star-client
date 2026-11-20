using System;
using System.Collections.Generic;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.State;

namespace EquestriaStar.UI;

/// <summary>
/// 等待房间的“小剧场页”外观：左页舞台按座位立起纸片立牌，右页是“角色表”。
/// 只改排版与上色，场景节点路径（测试依赖的 ActionButton / MemberList / PollTimer）保持不变。
/// </summary>
public partial class RoomScreen
{
    private void ApplyBookStyle()
    {
        var stage = EntryBook.BuildStage(this, "room");
        var page = EntryBook.RightPage["room"];
        EntryBook.Footer(stage, page);
        _standees = new Control { Name = "Standees", MouseFilter = MouseFilterEnum.Ignore };
        EntryBook.AddAt(stage, _standees, new Rect2(112, 600, 600, 214));

        var rootMargin = GetNode<MarginContainer>("RootMargin");
        EntryBook.PlaceOnStage(rootMargin, page);
        rootMargin.AddThemeConstantOverride("margin_left", 30);
        rootMargin.AddThemeConstantOverride("margin_right", 30);
        rootMargin.AddThemeConstantOverride("margin_top", 6);
        rootMargin.AddThemeConstantOverride("margin_bottom", 76);
        var layout = GetNode<VBoxContainer>("RootMargin/Layout");
        layout.AddThemeConstantOverride("separation", 12);

        // 页眉：小标志 | 房间名 …… 右侧留给书签
        var header = GetNode<HBoxContainer>("RootMargin/Layout/Header");
        header.AddThemeConstantOverride("separation", 18);
        header.CustomMinimumSize = new Vector2(0, 84);
        var infoBar = GetNode<HBoxContainer>("RootMargin/Layout/InfoBar");
        infoBar.AddThemeConstantOverride("separation", 16);
        _backButton.Reparent(infoBar);
        infoBar.MoveChild(_backButton, 0);
        _backButton.Text = "返回";
        _backButton.TooltipText = "离开房间";
        EntryBook.StyleCapsule(_backButton, EntryBook.CapsuleKind.Warm, 44, 18, "back");
        _backButton.CustomMinimumSize = new Vector2(118, 44);

        var crest = EntryBook.Crest(196, 22);
        crest.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(crest);
        header.MoveChild(crest, 0);
        var bar = EntryBook.MakeLabel("|", 34, EntryBook.InkSoft);
        header.AddChild(bar);
        header.MoveChild(bar, 1);
        var titleBox = GetNode<VBoxContainer>("RootMargin/Layout/Header/TitleBox");
        titleBox.Alignment = BoxContainer.AlignmentMode.Center;
        titleBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        EntryBook.StyleLabel(_roomNameLabel, 36, EntryBook.Ink, EntryBook.TitleFont);
        _roomNameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _roomNameLabel.ClipText = true;
        _roomNameLabel.CustomMinimumSize = new Vector2(200, 0);
        header.AddChild(new Control { CustomMinimumSize = new Vector2(290, 0), MouseFilter = MouseFilterEnum.Ignore });

        var headRule = EntryBook.Rule(1.5f, new Color(EntryBook.InkSoft, 0.35f));
        layout.AddChild(headRule);
        layout.MoveChild(headRule, 1);

        _roomMetaLabel.Reparent(infoBar);
        infoBar.MoveChild(_roomMetaLabel, 1);
        EntryBook.StyleLabel(_roomMetaLabel, 17, EntryBook.Ink);
        _roomMetaLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _roomMetaLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _roomMetaLabel.ClipText = true;
        _roomMetaLabel.VerticalAlignment = VerticalAlignment.Center;
        _ownerLabel.Visible = false;
        _roomStatusLabel.Visible = false;
        _statusTag = new EntryBook.Tag(40, 18, 120);
        infoBar.AddChild(_statusTag);
        _statusTag.Set(EntryBook.TagKind.Empty, "同步中");

        GetNode<CanvasItem>("RootMargin/Layout/Divider").Visible = false;
        var memberHeader = GetNode<HBoxContainer>("RootMargin/Layout/MemberHeader");
        foreach (var child in memberHeader.GetChildren())
        {
            if (child is CanvasItem item)
            {
                item.Visible = false;
            }
        }

        var section = EntryBook.SectionHeader("角色表");
        section.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        memberHeader.AddChild(section);

        // 成员行 + 空座位行：不滚动，按内容撑开（最多 6 座）
        var scroll = GetNode<ScrollContainer>("RootMargin/Layout/MemberScroll");
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _memberList.AddThemeConstantOverride("separation", 4);
        _emptySeats = new VBoxContainer { Name = "EmptySeats", MouseFilter = MouseFilterEnum.Ignore };
        _emptySeats.AddThemeConstantOverride("separation", 4);
        layout.AddChild(_emptySeats);
        layout.MoveChild(_emptySeats, scroll.GetIndex() + 1);
        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        layout.AddChild(spacer);
        layout.MoveChild(spacer, _emptySeats.GetIndex() + 1);

        // 同步状态行 + 主按钮
        var bottomBar = GetNode<HBoxContainer>("RootMargin/Layout/BottomBar");
        var syncRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        syncRow.AddThemeConstantOverride("separation", 8);
        layout.AddChild(syncRow);
        layout.MoveChild(syncRow, bottomBar.GetIndex());
        var signal = EntryBook.MakeIcon("signal", 24, new Color("7f9174"));
        syncRow.AddChild(signal);
        _syncStatusLabel.Reparent(syncRow);
        EntryBook.StyleLabel(_syncStatusLabel, 16, EntryBook.InkSoft);
        _syncStatusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _syncStatusLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _syncStatusLabel.ClipText = true;
        _syncStatusLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _hintLabel = EntryBook.MakeLabel("", 16, EntryBook.InkSoft, null, HorizontalAlignment.Right);
        syncRow.AddChild(_hintLabel);

        bottomBar.Alignment = BoxContainer.AlignmentMode.Center;
        _actionButton.CustomMinimumSize = new Vector2(480, 60);
        _actionButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        StyleAction(EntryBook.CapsuleKind.Primary, null);

        // 玩家书签（房间页不放退出登录，只显示昵称）
        var bookmark = EntryBook.PlayerBookmark(Session.GetPlayerName(), null);
        AddChild(bookmark);
        MoveChild(bookmark, _leaveConfirm.GetIndex());
        EntryBook.PlaceOnStage(bookmark, new Rect2(page.End.X - 330, 0, 300, 140));

        StyleLeaveConfirm();
        ShowStatus("", false);
    }

    private void StyleAction(EntryBook.CapsuleKind kind, string? icon)
    {
        if (_actionKind == kind && _actionIcon == icon)
        {
            return;
        }

        _actionKind = kind;
        _actionIcon = icon;
        _actionButton.Icon = null;
        EntryBook.StyleCapsule(_actionButton, kind, 60, 27, icon);
        EntryBook.KeepIconBesideText(_actionButton);
    }

    private void UpdateEmptySeats(RoomDetailDto room)
    {
        var taken = new HashSet<int>();
        foreach (var member in room.Members ?? [])
        {
            taken.Add(member.SeatNo);
        }

        var wanted = new List<int>();
        for (var seat = 1; seat <= Math.Clamp(room.MaxPlayers, 0, 8); seat++)
        {
            if (!taken.Contains(seat))
            {
                wanted.Add(seat);
            }
        }

        var existing = new List<int>();
        foreach (var child in _emptySeats.GetChildren())
        {
            if (!child.IsQueuedForDeletion())
            {
                existing.Add((int)child.GetMeta("seat", 0));
            }
        }

        if (existing.Count == wanted.Count && existing.TrueForAll(wanted.Contains))
        {
            return;
        }

        foreach (var child in _emptySeats.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var seat in wanted)
        {
            var row = new PanelContainer { CustomMinimumSize = new Vector2(0, 54), MouseFilter = MouseFilterEnum.Ignore };
            row.SetMeta("seat", seat);
            row.AddThemeStyleboxOverride("panel", RoomMemberRow.RowBox(false));
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            margin.AddThemeConstantOverride("margin_left", 16);
            margin.AddThemeConstantOverride("margin_right", 10);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            row.AddChild(margin);
            var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            line.AddThemeConstantOverride("separation", 16);
            margin.AddChild(line);
            var number = new Label { Text = seat.ToString() };
            var badge = RoomMemberRow.SeatBadge(number, out var plate);
            plate.Modulate = new Color(1, 1, 1, 0.5f);
            line.AddChild(badge);
            line.AddChild(RoomMemberRow.Leader());
            var tag = new EntryBook.Tag(36, 17, 136);
            tag.Set(EntryBook.TagKind.Empty, "等待加入");
            line.AddChild(tag);
            _emptySeats.AddChild(row);
        }
    }

    /// <summary>左页舞台上的座位立牌：号码圆章 + 昵称首字 + 房主皇冠 / 已准备对勾；空座位是虚线立牌。</summary>
    private void UpdateStandees(RoomDetailDto room)
    {
        foreach (var child in _standees.GetChildren())
        {
            child.QueueFree();
        }

        var seats = Math.Clamp(room.MaxPlayers, 1, 8);
        var bySeat = new Dictionary<int, RoomMemberDto>();
        foreach (var member in room.Members ?? [])
        {
            bySeat[member.SeatNo] = member;
        }

        const float gap = 8f;
        var area = _standees.Size;
        var width = MathF.Min(104f, (area.X - gap * (seats - 1)) / seats);
        var height = width * 1.58f;
        var total = seats * width + (seats - 1) * gap;
        var left = (area.X - total) / 2f;
        for (var seat = 1; seat <= seats; seat++)
        {
            bySeat.TryGetValue(seat, out var member);
            var card = new Control
            {
                MouseFilter = MouseFilterEnum.Ignore,
                Position = new Vector2(left + (seat - 1) * (width + gap), area.Y - height),
                Size = new Vector2(width, height)
            };
            _standees.AddChild(card);
            var plate = EntryBook.MakeImage(member == null ? "parts/standee_empty" : "parts/standee", Vector2.Zero, TextureRect.StretchModeEnum.Scale);
            plate.SetAnchorsPreset(LayoutPreset.FullRect);
            card.AddChild(plate);

            var isSelf = member != null && member.UserId == CurrentUserId;
            var badgeSize = width * 0.42f;
            var badge = EntryBook.MakeImage(isSelf ? "parts/seat_gold" : "parts/seat_rose", Vector2.Zero);
            badge.Position = new Vector2((width - badgeSize) / 2f, -badgeSize * 0.5f);
            badge.Size = new Vector2(badgeSize, badgeSize);
            if (member == null)
            {
                badge.Modulate = new Color(1, 1, 1, 0.55f);
            }

            card.AddChild(badge);
            var number = EntryBook.MakeLabel(seat.ToString(), (int)(badgeSize * 0.5f), new Color("fbf4e8"), EntryBook.BoldFont, HorizontalAlignment.Center);
            number.Position = badge.Position;
            number.Size = badge.Size;
            card.AddChild(number);

            if (member == null)
            {
                var shoe = EntryBook.MakeIcon("horseshoe", width * 0.34f, new Color(EntryBook.InkFaint, 0.75f));
                shoe.Position = new Vector2(width * 0.33f, height * 0.36f);
                shoe.Size = new Vector2(width * 0.34f, width * 0.34f);
                card.AddChild(shoe);
                continue;
            }

            var isOwner = string.Equals(member.MemberRole, "OWNER", StringComparison.OrdinalIgnoreCase);
            var isReady = string.Equals(member.ReadyStatus, "READY", StringComparison.OrdinalIgnoreCase);
            if (isOwner)
            {
                var crown = EntryBook.MakeImage("parts/crown", Vector2.Zero);
                crown.Position = new Vector2(width * 0.31f, height * 0.18f);
                crown.Size = new Vector2(width * 0.38f, width * 0.27f);
                card.AddChild(crown);
            }

            var initial = EntryBook.MakeLabel(
                EntryBook.Initial(RoomMemberRow.PlainName(member)),
                (int)(width * 0.36f),
                EntryBook.Ink,
                EntryBook.TitleFont,
                HorizontalAlignment.Center);
            initial.Position = new Vector2(0, height * 0.3f);
            initial.Size = new Vector2(width, height * 0.3f);
            card.AddChild(initial);
            if (isReady && !isOwner)
            {
                var dotSize = width * 0.3f;
                var dot = new Panel
                {
                    MouseFilter = MouseFilterEnum.Ignore,
                    Position = new Vector2((width - dotSize) / 2f, height * 0.62f),
                    Size = new Vector2(dotSize, dotSize)
                };
                dot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = new Color("9fb091"),
                    BorderColor = new Color("fbf4e8"),
                    BorderWidthLeft = 2,
                    BorderWidthTop = 2,
                    BorderWidthRight = 2,
                    BorderWidthBottom = 2,
                    CornerRadiusTopLeft = 40,
                    CornerRadiusTopRight = 40,
                    CornerRadiusBottomLeft = 40,
                    CornerRadiusBottomRight = 40,
                    AntiAliasing = true
                });
                card.AddChild(dot);
                var tickSize = dotSize * 0.6f;
                var tick = EntryBook.MakeIcon("check", tickSize, new Color("fbf4e8"));
                tick.Position = dot.Position + new Vector2((dotSize - tickSize) / 2f, (dotSize - tickSize) / 2f);
                tick.Size = new Vector2(tickSize, tickSize);
                card.AddChild(tick);
            }
        }
    }

    private void StyleLeaveConfirm()
    {
        _leaveConfirm.GetNode<ColorRect>("Scrim").Color = new Color(0.16f, 0.1f, 0.07f, 0.46f);
        var panel = _leaveConfirm.GetNode<PanelContainer>("Center/Panel");
        EntryBook.DecorateCard(panel, 460, 116);
        var margin = _leaveConfirm.GetNode<MarginContainer>("Center/Panel/Margin");
        margin.AddThemeConstantOverride("margin_left", 44);
        margin.AddThemeConstantOverride("margin_right", 44);
        margin.AddThemeConstantOverride("margin_top", 34);
        margin.AddThemeConstantOverride("margin_bottom", 40);
        EntryBook.StyleCardTitle(_leaveConfirm.GetNode<Label>("Center/Panel/Margin/Layout/TitleLabel"), 32);
        EntryBook.StyleLabel(_leaveConfirmMessage, 19, EntryBook.Ink);
        _leaveConfirmMessage.HorizontalAlignment = HorizontalAlignment.Center;
        _leaveConfirmMessage.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        EntryBook.StyleLabel(_leaveConfirmStatus, 16, EntryBook.InfoInk);
        _leaveConfirmStatus.HorizontalAlignment = HorizontalAlignment.Center;
        _leaveNoButton.Text = "再想想";
        _leaveYesButton.Text = "确认离开";
        EntryBook.StyleCapsule(_leaveNoButton, EntryBook.CapsuleKind.Secondary, 50, 20);
        EntryBook.StyleCapsule(_leaveYesButton, EntryBook.CapsuleKind.Primary, 50, 20);
        _leaveNoButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _leaveYesButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _leaveConfirm.GetNode<HBoxContainer>("Center/Panel/Margin/Layout/ButtonRow").AddThemeConstantOverride("separation", 14);
    }
}
