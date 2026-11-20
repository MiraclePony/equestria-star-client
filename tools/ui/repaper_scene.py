"""
游戏内覆盖层场景的纸艺换皮（只改视觉属性，不改节点、路径、脚本、信号）。
规则：
  1. 主题从入口页共用的 client_theme.tres 换成游戏内专用的 ingame_paper_theme.tres；
  2. 删除为深色底准备的字色覆盖、深色面板 / 按钮样式覆盖，让纸艺主题生效；
  3. 按节点名给标题、状态、错误、取消 / 关闭、放弃 / 丢弃 / 遣散等加上主题变体；
  4. 遮罩 ColorRect 换成暖棕半透明。
用法：python tools/ui/repaper_scene.py <scene.tscn> [...]
"""
import io
import re
import sys

THEME_OLD = re.compile(r'\[ext_resource type="Theme"( uid="[^"]+")? path="res://assets/theme/client_theme.tres" id="([^"]+)"\]')
THEME_NEW = '[ext_resource type="Theme" path="res://assets/theme/ingame_paper_theme.tres" id="\\2"]'

DROP = [
    re.compile(r'^theme_override_colors/font_color = .*$'),
    re.compile(r'^theme_override_colors/font_disabled_color = .*$'),
    re.compile(r'^theme_override_styles/(panel|normal|hover|pressed|disabled) = SubResource\("(Style_|StyleBox_)[^"]*"\)$'),
]

# 节点名 → 主题变体（只加在没有变体的节点上）。
VARIATIONS = [
    (re.compile(r'^(Title|TitleLabel)$'), 'PaperTitleLabel'),
    (re.compile(r'^(ErrorLabel)$'), 'PaperErrorLabel'),
    (re.compile(r'^(Status|StatusLabel|HintLabel|DeadlineLabel|WorkLabel)$'), 'PaperMutedLabel'),
    (re.compile(r'^(Abandon\w*Button|Discard\w*Button|Dismiss\w*Button)$'), 'PaperDangerButton'),
    (re.compile(r'^Close\w*Button$'), 'PaperCloseButton'),
    (re.compile(r'^(Cancel\w*|No\w*|Back\w*|Return\w*)Button$'), 'PaperSecondaryButton'),
]
CARD_ROOTS = {'QuestTaskCard'}
SHADES = {'Shade', 'Backdrop', 'DimBackground'}
SHADE_COLOR = 'color = Color(0.16, 0.1, 0.05, 0.55)'
DICE_BACKDROP_COLOR = 'color = Color(0.22, 0.15, 0.1, 1)'


# 骰子层：面板尺寸会影响投掷区与按钮命中，保留原面板样式引用和原内边距，只把样式本身换成纸面。
DICE_PANEL_STYLES = {
    'StyleBox_panel': 2.0,
    'StyleBox_result': 2.0,
}
PAPER_TEXTURE_LINE = '[ext_resource type="Texture2D" path="res://assets/art/ui/paper_hud/panels/drawer_cream.png" id="paper_tex"]'


def paper_stylebox(style_id, margin):
    return [
        f'[sub_resource type="StyleBoxTexture" id="{style_id}"]',
        f'content_margin_left = {margin}',
        f'content_margin_top = {margin}',
        f'content_margin_right = {margin}',
        f'content_margin_bottom = {margin}',
        'texture = ExtResource("paper_tex")',
        'texture_margin_left = 40.0',
        'texture_margin_top = 40.0',
        'texture_margin_right = 40.0',
        'texture_margin_bottom = 40.0',
    ]


def repaper(path):
    text = io.open(path, encoding='utf-8', newline='').read()
    is_dice = 'WorkDiceOverlay' in path
    nl = '\r\n' if '\r\n' in text else '\n'
    lines = text.replace('\r\n', '\n').split('\n')
    out = []
    node = None
    node_type = None
    for line in lines:
        m = re.match(r'^\[node name="([^"]+)" type="([^"]+)"', line)
        if line.startswith('['):
            node = m.group(1) if m else None
            node_type = m.group(2) if m else None
        line = THEME_OLD.sub(THEME_NEW, line)
        if node and any(p.match(line) for p in DROP):
            if not (is_dice and any(f'"{k}"' in line for k in DICE_PANEL_STYLES) and '/panel = ' in line):
                continue
        if node in SHADES and node_type == 'ColorRect' and line.startswith('color = '):
            # 骰子层背景是不透明底（3D 骰盘背后），其余是半透明遮罩。
            line = DICE_BACKDROP_COLOR if line.rstrip().endswith(', 1)') and 'Dice' in path else SHADE_COLOR
        out.append(line)
        if m:
            variation = None
            if node in CARD_ROOTS:
                variation = 'PaperCardPanel'
            else:
                for pattern, name in VARIATIONS:
                    if pattern.match(node):
                        expected_base = 'Button' if name.endswith('Button') else 'Label'
                        if node_type == expected_base:
                            variation = name
                        break
            if variation:
                out.append(f'theme_type_variation = &"{variation}"')
    if is_dice:
        out = rewrite_dice_styles(out)
    io.open(path, 'w', encoding='utf-8', newline='').write(nl.join(out))
    print('repapered', path)


def rewrite_dice_styles(lines):
    result = []
    skipping = False
    for line in lines:
        m = re.match(r'^\[sub_resource type="StyleBoxFlat" id="([^"]+)"\]', line)
        if m and m.group(1) in DICE_PANEL_STYLES:
            result.extend(paper_stylebox(m.group(1), DICE_PANEL_STYLES[m.group(1)]))
            skipping = True
            continue
        if skipping:
            if line.startswith('['):
                skipping = False
                result.append('')
            else:
                continue
        result.append(line)
    # 纸面贴图的外部资源声明放在第一个 ext_resource 之后。
    for i, line in enumerate(result):
        if line.startswith('[ext_resource') and PAPER_TEXTURE_LINE not in result:
            result.insert(i + 1, PAPER_TEXTURE_LINE)
            break
    return result


if __name__ == '__main__':
    for p in sys.argv[1:]:
        repaper(p)
