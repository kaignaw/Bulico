# Bulico Visual Identity / 视觉识别系统

本目录是 Bulico 所有视觉资产的唯一入口。后续新增 Logo、插件图标、界面图形时，先阅读本文，再修改 `Logo/` 或 `Icons/` 中的源文件。

```text
Brand/
├── README.md      # 本文件：视觉识别系统与设计思路
├── Logo/          # Grid Stack 标志、组合标志、App 图标
└── Icons/         # 插件功能图标系统（SVG + 32/16 PNG）
```

## 1. 核心概念：Grid Stack

Bulico 的品牌图形来自建筑制图里的**楼层堆叠**：三层圆角等轴测楼板，顶部楼板实心 Teal，中层完整 Ink，底层降低透明度。

设计思路：

- **等轴测楼板**对应 Revit 的标高、楼层和轴网语境，比通用的“字母 B”更贴近 AEC 工作流。
- **三层结构**既像楼层堆叠，也暗示插件集合中的多个工具。
- **圆角**让图形在 16–32 px 下更柔和，避免建筑线稿的冷硬感。
- **顶部实心 Teal**代表“当前操作/新生成的对象”；下方两层线稿代表上下文。
- **不放核心筒开孔**：小尺寸下更干净，也避免图形中心出现视觉空洞。
- **左右轴对称**：关于垂直中线对称；纵向因顶部实心、底层弱化而刻意保持层级差异，不做水平镜像。
- **视觉等距**：顶部是实心填充、下方两层有描边，描边会向外扩。Logo 在 512 画布中把中层和底层整体上移 10 px，以补偿描边外扩，使三条可见间距一致。缩放或重绘时必须保留这个“按可见边缘等距”的原则。

## 2. 色彩

| Token | Hex | 用途 |
|-------|-----|------|
| Ink | `#0B1727` | 主线条、深色底板、文字 |
| Teal | `#14B8A6` | 强调色、顶部楼板、选中状态 |
| Paper | `#F8FAFC` | 浅色背景、深色底板上的线稿 |

使用规则：

- 单个图形最多使用 Ink + Teal 两色；Paper 只用于反白。
- Teal 是强调色，不要大面积铺满；在功能图标中通常只用于一个关键对象或一条动作线。
- 不使用旧版插件的多彩圆角背景。旧图标体系已废弃。

## 3. Logo 系统

| 资产 | 文件 | 用途 |
|------|------|------|
| 主标志 | `Logo/01-grid-stack.svg` | 浅色背景、文档、演示 |
| 深色版 | `Logo/01-grid-stack-dark.svg` | 深色背景、GitHub 深色主题 |
| 单色版 | `Logo/01-grid-stack-mono.svg` | 单色印刷、雕刻 |
| App 图标 | `Logo/app-icon-grid-stack.svg` | Revit 面板、Windows 图标 |
| 横向组合（浅色） | `Logo/bulico-lockup.svg` | README、网站 |
| 横向组合（深色） | `Logo/bulico-lockup-dark.svg` | README 深色主题 |

使用规则：

- 组合标志只保留 **Grid Stack + Bulico**，不再在标志内部放 Revit 版本号；版本信息属于 README、About 和发布说明。
- 组合标志的 viewBox 已按内容边界收紧并居中；不要随意改回大画布，否则会出现视觉偏移。
- 单色版用于无法使用 Teal 的场景；不要自行改变 Teal 的色值。
- 文字目前使用系统无衬线字体（Segoe UI / Inter / Helvetica / Arial）。正式印刷或长期品牌使用时，建议转为轮廓。
- 最小使用尺寸：主标志不低于 16 px；组合标志建议不低于 160 px 宽。

预览：`Logo/preview.html`

## 4. 功能图标系统

所有命令图标共用一套“深色圆角底板 + 白色线稿 + Teal 强调”的模板，保证在 Revit 浅色和深色 Ribbon 上都可读。

### 图标模板

- 画布：`32 × 32`
- 底板：`30 × 30`，`rx=7`，Ink `#0B1727`
- 线稿：Paper `#F8FAFC`
- 强调：Teal `#14B8A6`
- 安全区：四周至少 `4 px`
- 主线条：`1.4–2.2 px`
- 强调线/填充：`2.2–2.6 px` 或实心 Teal
- 端点/转角：`stroke-linecap="round"`、`stroke-linejoin="round"`
- 不在图标内放文字；功能名称由 Ribbon 按钮文字承担

### 图标矩阵

| 文件 | 功能 |
|------|------|
| `grid_wall` | 框轴生墙 |
| `wall_beam` | 框墙生梁 |
| `wall_beam_floor` | 墙梁建板 |
| `room_floor` | 房间建板 |
| `elevation_grid` | 立面轴网标注 |
| `mullion` | 一键横挺 |
| `door_window_mark` | 一键门窗类型标记 |
| `same_type` | 同型过滤 |
| `category_filter` | 类别过滤 |
| `fine_filter` | 精细过滤 |
| `about` | 关于 |

### 设计思路

- **深色底板**：Revit Ribbon 可能随主题变亮或变暗；固定 Ink 底板 + 白色线稿能保证对比度。
- **一个图标只讲一个动作**：例如框轴生墙是“网格 + Teal 墙段”，框墙生梁是“白墙 + Teal 梁”。
- **Teal 只标记动作结果/选中对象**，不要同时强调多个元素。
- **小尺寸优先**：先保证 16 px 能读出主体，再增加 32 px 细节。避免细碎文字、过多交叉线。
- **同族区分**：同型过滤用“三个相同方块 + 中间 Teal”；类别过滤用“Teal 类别框 + 不同形状”；精细过滤用“层级树 + Teal 叶子”。不要用三个几乎一样的漏斗。

### 新增或修改图标

```bash
cd Brand/Icons
npm install
npm run build
```

1. 在 `Brand/Icons/src/` 新建或修改 `32 × 32` SVG，严格复用上面的底板、颜色、线宽和安全区。
2. 运行 `npm run build`，生成 `Brand/Icons/<name>_32.png` 与 `<name>_16.png`。
3. 在 `Bulico.csproj` 添加两条 `<EmbeddedResource>`。
4. 在 `App.cs` 用 `Icon("<name>", 32/16)` 注册。
5. 打开 `Brand/Icons/preview.html`，检查 64 / 32 / 16 px 与浅色/深色背景。
6. 重新编译，确认嵌入资源名为 `Bulico.Brand.Icons.<name>_<size>.png`。

禁止直接手改生成的 PNG；PNG 是构建产物，源文件永远是 `src/*.svg`。

## 5. 字体

- 组合标志和界面文字使用系统无衬线字体：Segoe UI / Inter / Helvetica / Arial。
- 品牌正式物料建议把 “Bulico” 转为轮廓，避免不同机器字体替换导致宽度变化。
- 功能图标不使用字体。

## 6. 检查清单

- [ ] 图形是否仍遵循 Grid Stack 的圆角、楼层、等距语言？
- [ ] 是否只使用 Ink / Teal / Paper？
- [ ] 16 px 下主体是否仍然可读？
- [ ] Teal 是否只强调一个动作或一个对象？
- [ ] 是否从 SVG 源生成 PNG，而不是手改 PNG？
- [ ] 是否更新了 `App.cs`、`Bulico.csproj` 和预览页？
- [ ] 是否重新编译并确认嵌入资源存在？

---

维护入口：

- Logo：`Logo/README.md`、`Logo/preview.html`
- 功能图标：`Icons/README.md`、`Icons/preview.html`
