# Command Icons / 功能图标

Bulico 的命令图标使用统一的 Grid Stack 视觉语言：

- **深色圆角底板**：`#0B1727`，`30 × 30`，`rx=7`
- **白色线稿**：`#F8FAFC`
- **Teal 强调**：`#14B8A6`
- **画布**：`32 × 32`，四周至少 `4 px` 安全区
- **线条**：主线条 `1.8–2.2 px`，强调线 `2.2–2.6 px`，圆角端点

## 文件结构

```text
Resources/Icons/
├── src/                 # SVG 源文件（32 × 32）
├── *_32.png             # Revit 大图标
├── *_16.png             # Revit 小图标
├── generate-icons.mjs   # SVG -> PNG 生成脚本
├── package.json
└── preview.html         # 图标预览
```

## 重新生成 PNG

```bash
cd Resources/Icons
npm install
npm run build
```

生成的 PNG 会写入 `Resources/Icons/`，并由 `Bulico.csproj` 显式嵌入。

嵌入资源命名为：

```text
Bulico.Resources.Icons.<name>_<size>.png
```

例如：

```text
Bulico.Resources.Icons.grid_wall_32.png
Bulico.Resources.Icons.grid_wall_16.png
```

## 新增图标

1. 在 `src/` 新建 `32 × 32` SVG，沿用上面的底板、颜色和线宽。
2. 运行 `npm run build` 生成 32/16 PNG。
3. 在 `Bulico.csproj` 添加两条 `<EmbeddedResource>`。
4. 在 `App.cs` 注册对应按钮图标。

## 预览

浏览器打开 `preview.html`，可同时查看 64 / 32 / 16 px 和浅色/深色背景效果。
