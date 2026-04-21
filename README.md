# PaoGameLun - 鸣潮游戏启动器

<p align="center">
  <img src="Assets/app.png" alt="PaoGameLun" width="128" height="128" />
</p>

<p align="center">
  一款基于 WPF 的现代化游戏启动器，macOS 风格 UI，专为鸣潮设计。
</p>

---

## 功能特性

### 🎮 游戏管理
- **添加游戏** — 支持拖拽 `.exe` / `.lnk` 快捷方式添加，也可浏览选择文件
- **快捷方式解析** — 自动解析 `.lnk` 文件获取真实 exe 路径和图标
- **游戏启动** — 一键启动已添加的游戏
- **游戏编辑/删除** — 灵活管理游戏列表
- **进程检测** — 自动检测游戏是否正在运行，运行中显示「终止运行」按钮

### 🎨 界面设计
- **macOS 风格窗口** — 红绿灯按钮（关闭/最小化/最大化），无边框透明窗口
- **亚克力模糊效果** — Windows Acrylic 毛玻璃背景
- **自定义背景** — 支持设置网络 URL 或本地路径的图片/视频背景
- **紧凑布局** — 4px 间距精度，像素级对齐

### 🔊 音量控制
- **滑块调节** — 音量滑块替代静音按钮，支持拖动调节
- **实时显示** — 音量百分比 + 游戏名称显示

### ⚙️ 设置面板
- **背景自定义** — 支持本地图片、视频或网络 URL 作为启动器背景
- **进程名称配置** — 为每个游戏自定义检测的进程名
- **界面透明度** — 可调节窗口透明度

### 📦 安装部署
- **自包含安装包** — 使用 Inno Setup 6 打包，内置 .NET 8 运行时
- **无需预装环境** — 目标机器无需安装 .NET 即可运行
- **桌面快捷方式** — 安装时可选创建桌面图标和开机自启

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
| **Win32 API** | 自定义窗口标题栏、拖拽文件支持 |
| **COM 接口** | ShellLink 解析 .lnk 快捷方式 |
| **Inno Setup 6** | 安装包制作 |
| **C#** | 主要开发语言 |

## 项目结构

```
PaoGameLun/
├── App.xaml / App.xaml.cs          # 应用入口
├── GameLauncher.csproj              # 项目配置
├── installer.iss                    # Inno Setup 安装脚本
├── Assets/                          # 图标、图片资源
├── Models/
│   └── Game.cs                      # 游戏数据模型
├── Services/
│   ├── GameManager.cs               # 游戏增删改查、启动、进程检测
│   ├── IconExtractor.cs             # 图标提取、名称推断
│   ├── ShortcutResolver.cs          # .lnk 快捷方式解析
│   └── AcrylicBlurService.cs        # 亚克力模糊效果
├── Views/
│   ├── MainWindow.xaml/.cs          # 主窗口
│   ├── AddGameDialog.xaml/.cs       # 添加/编辑游戏对话框（支持拖拽）
│   ├── SettingsPanel.xaml/.cs       # 设置面板（音量、背景等）
│   └── GameDetailWindow.xaml/.cs    # 游戏详情窗口
└── Converters/                      # WPF 值转换器
```

## 许可证

MIT License
