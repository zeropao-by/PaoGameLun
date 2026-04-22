# PaoGameLun - 游戏启动器

<p align="center">
  <img src="Assets/app.png" alt="PaoGameLun" width="128" height="128" />
</p>

<p align="center">
  一款基于 WPF 的现代化游戏/软件启动器，macOS 风格 UI。
</p>

---

## ✨ 功能特性

### 🎮 游戏管理
- **添加游戏** — 支持拖拽 `.exe` / `.lnk` 快捷方式添加，也可浏览选择文件
- **快捷方式解析** — 自动解析 `.lnk` 文件获取真实 exe 路径和图标
- **游戏启动** — 一键启动已添加的游戏
- **游戏编辑/删除** — 灵活管理游戏列表
- **进程检测与终止** — 智能多层检测，运行中显示红色「终止运行」按钮

### 🎯 精准进程管理（核心特性）
支持多种方式检测和终止游戏进程，按优先级逐层匹配，精准不误伤：

| 优先级 | 匹配方式 | 说明 |
|--------|----------|------|
| ① **最高** | PID 精确匹配 | 启动时捕获的实际进程 ID |
| ② **新增** | exe 路径精确匹配 | 直接用用户填入的 exe 路径对比 `MainModule.FileName`（lnk 自动解析为真实 exe） |
| ③ | 进程名 + 目录验证 | UE 游戏等特殊场景的目录级别校验 |
| ④ | 进程名模糊匹配 | 纯进程名匹配 |
| ⑤ **兜底** | 名称模糊匹配 | 进程名与游戏名互相包含 |

> **特别支持：** 通过桌面快捷方式（`.lnk`）添加的软件（如网易云音乐、Steam 游戏），系统会自动解析真实 exe 路径进行精确的进程检测和终止。

### 🎨 界面设计
- **macOS 风格窗口** — 红绿灯按钮（关闭/最小化/最大化），无边框透明窗口
- **亚克力模糊效果** — Windows Acrylic 毛玻璃背景，可调透明度
- **自定义背景** — 支持设置网络 URL 或本地路径的图片/视频背景
- **紧凑布局** — 4px 间距精度，像素级对齐

### 🔊 音量控制
- **滑块调节** — 音量滑块替代静音按钮，支持拖动调节
- **实时显示** — 音量百分比 + 游戏名称显示
- **暂停播放** — 视频背景暂停/播放切换

### ⭐ 收藏与搜索
- **收藏置顶** — 星标收藏的游戏排在最前
- **搜索过滤** — 实时关键词搜索快速定位

### ⚙️ 设置面板
- **背景自定义** — 本地图片、视频或网络 URL 作为背景
- **进程名称配置** — 为每个游戏自定义检测进程名（逗号分隔多个）
- **液态玻璃** — 可调节窗口模糊度（0-100%）
- **开机自启** — 可选开机自动启动
- **最小化托盘** — 可选最小化到系统托盘

### 📊 游戏时长统计
- **累计时长统计** — 记录每个游戏的累计游玩时间
- **上次启动记录** — 显示最近一次启动时间
- **实时计时** — 当前游玩会话实时计算

### 📦 安装部署
- **自包含安装包** — Inno Setup 6 打包，内置 .NET 8 运行时
- **无需预装环境** — 目标机器无需安装 .NET 即可运行
- **桌面快捷方式** — 安装时可选创建桌面图标和开机自启
- **卸载清理** — 卸载时可选清除用户数据

---

## 截图

<img width="1387" height="803" alt="image" src="https://github.com/user-attachments/assets/1877f8aa-015c-44f6-9790-aef1e642babc" />
<img width="1387" height="803" alt="image" src="https://github.com/user-attachments/assets/8d69c4a9-2717-476d-a3a9-0cef54ae1f2a" />
<img width="1389" height="800" alt="image" src="https://github.com/user-attachments/assets/cdb3f56e-c071-4163-b803-8ce1bab52517" />
<img width="1390" height="802" alt="image" src="https://github.com/user-attachments/assets/0debcdc8-3626-4471-b832-f6d204416f38" />

---

## 下载

从 [Releases](https://github.com/zeropao-by/PaoGameLun/releases) 下载最新安装包。

## 技术栈

| 技术 | 说明 |
|------|------|
| **WPF (.NET 8)** | UI 框架 |
| **C# 12** | 开发语言 |
| **Win32 API** | 自定义窗口、亚克力模糊、拖拽文件 |
| **COM 接口 (ShellLink)** | .lnk 快捷方式解析 |
| **System.Text.Json** | 数据持久化 (JSON) |
| **Inno Setup 6** | 安装包制作 |

## 项目结构

```
PaoGameLun/
├── App.xaml / App.xaml.cs          # 应用入口 & 全局异常处理
├── GameLauncher.csproj              # 项目配置 (.NET 8)
├── installer.iss                    # Inno Setup 安装脚本
├── Assets/                          # 图标、图片资源
├── Models/
│   └── Game.cs                      # GameInfo / AppSettings 数据模型
├── Services/
│   ├── GameManager.cs               # 游戏增删改查、启动、进程检测/终止
│   ├── IconExtractor.cs             # 图标提取、名称推断、颜色猜测
│   ├── ShortcutResolver.cs          # .lnk 快捷方式解析 (COM + WSH)
│   └── AcrylicBlurService.cs        # 亚克力毛玻璃效果
├── Views/
│   ├── MainWindow.xaml/.cs          # 主窗口（核心逻辑）
│   ├── AddGameDialog.xaml/.cs       # 添加/编辑游戏对话框（支持拖拽）
│   ├── SettingsPanel.xaml/.cs       # 设置面板
│   └── Converters/                  # WPF 值转换器
│       ├── IconPathConverter.cs     # 图标路径转换
│       ├── BoolToVisibilityConverter.cs
│       └── HintVisibilityConverter.cs
```

## 更新日志

### v0.3.7 (v37)
- 🆕 **exe 路径精确匹配** — 用用户填入的 exe 路径直接匹配 `MainModule.FileName` 进行进程检测和终止
- 🆕 **lnk 快捷方式完整支持** — 通过桌面快捷方式添加的软件能正确检测和终止进程
- 🔧 **代码质量提升** — 启用 nullable 引用类型，修复所有编译警告（100 → 0）

## 许可证

MIT License
