"""
给游戏内覆盖层里写死在场景中的按钮挂上纸艺操作图标（只加 icon 属性，不改文字、信号、节点路径）。
图标尺寸由 ingame_paper_theme.tres 的 Button/constants/icon_max_width 统一限制。
用法：python tools/ui/add_button_icons.py
"""
import io
import re

ICON_ROOT = 'res://assets/art/ui/paper_hud/icons/'

# 场景 → {按钮节点名: 图标名}。只覆盖静态按钮；代码动态生成的按钮在各自脚本里挂图标。
MAPPING = {
    'scenes/game/ui/CardViewerOverlay.tscn': {
        'SellButton': 'sell', 'RefreshButton': 'refresh', 'QuestRefreshButton': 'refresh',
        'RetryButton': 'retry', 'ConfirmButton': 'check',
    },
    'scenes/game/ui/PartnerOverlay.tscn': {
        'BackButton': 'back', 'RetryButton': 'retry', 'AbandonButton': 'abandon', 'ConfirmButton': 'check',
    },
    'scenes/game/ui/QuestCheckOverlay.tscn': {'RetryButton': 'retry', 'BackButton': 'back'},
    'scenes/game/ui/QuestJournalOverlay.tscn': {
        'RetryButton': 'retry', 'InventoryButton': 'organize', 'AbandonRewardButton': 'abandon', 'YesButton': 'check',
    },
    'scenes/game/ui/QuestTargetSubmissionOverlay.tscn': {'BackButton': 'back', 'RetryButton': 'retry', 'ConfirmButton': 'check'},
    'scenes/game/ui/QuestTaskCard.tscn': {'SubmitButton': 'submit', 'AbandonButton': 'abandon'},
    'scenes/game/dice/WorkDiceOverlay.tscn': {'ConfirmButton': 'check', 'RerollButton': 'roll', 'RecoveryRetryButton': 'retry'},
}


def apply(path, mapping):
    text = io.open(path, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r\n' in text else '\n'
    lines = text.replace('\r\n', '\n').split('\n')
    needed = sorted(set(mapping.values()))
    ids = {name: f'icon_{name}' for name in needed}
    # 外部资源声明插在最后一个 ext_resource 之后（已存在则跳过）。
    last_ext = max(i for i, l in enumerate(lines) if l.startswith('[ext_resource'))
    for name in reversed(needed):
        decl = f'[ext_resource type="Texture2D" path="{ICON_ROOT}{name}.png" id="{ids[name]}"]'
        if decl not in lines:
            lines.insert(last_ext + 1, decl)
    out = []
    count = 0
    for line in lines:
        out.append(line)
        m = re.match(r'^\[node name="([^"]+)" type="Button"', line)
        if m and m.group(1) in mapping:
            out.append(f'icon = ExtResource("{ids[mapping[m.group(1)]]}")')
            count += 1
    io.open(path, 'w', encoding='utf-8', newline='').write(nl.join(out))
    print(path, count)


if __name__ == '__main__':
    for p, m in MAPPING.items():
        apply(p, m)
