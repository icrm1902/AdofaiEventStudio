# ADOFAI 事件工坊（AdofaiEventStudio）

[![Release](https://img.shields.io/github/v/release/ICRM1902/AdofaiEventStudio?label=release)](https://github.com/ICRM1902/AdofaiEventStudio/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20.NET%20Framework%204.8-lightgrey)

一个用于 **A Dance of Fire and Ice** 关卡编辑器的批量事件工具，以 [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) 模组的形式加载。

在编辑器左侧的「事件工坊」页签中，可以批量生成、复制、调整和删除事件与装饰，并用表达式批量计算参数。

> 作者：橚 ｜ 当前版本：1.0.0 ｜ 许可证：MIT

---

## 功能

- **批量生成**：在指定楼层按次数生成普通事件，按事件类型展示全部参数。
- **批量装饰**：支持 `AddDecoration`、`AddText`、`AddObject`、`AddParticle`。
- **推广动画**：复制源楼层的 `MoveDecorations` 动画，批量改变标签和角度偏移。
- **批量调整**：批量移动事件楼层，或对数值参数做运算。
- **批量删除**：按事件类型和楼层范围删除。
- **表达式**：`i` / `n` / `v` 变量，`pi` / `e` 常数，四则运算与常用数学函数，还有 `inc`、`random` 和文本序号 `a[1]`。
- **文件工具**：载入、解析 `.adofai`，另存为新文件（不覆盖源文件）。
- **撤销**：所有批量操作只改内存，保存前可用游戏原生撤销栈回退。
- **双语界面**：跟随游戏 / 中文 / English。

## 安装

1. 安装 [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)（本模组基于 `ManagerVersion 0.32.4.0`）。
2. 从 [Releases](https://github.com/ICRM1902/AdofaiEventStudio/releases) 下载 `AdofaiEventStudio-1.0.0.zip`。
3. 用 UMM 的 **Install Mod** 直接安装，或解压到：

```text
A Dance of Fire and Ice\Mods\AdofaiEventStudio\
```

模组目录中应包含 `AdofaiEventStudio.dll` 和 `Info.json`。

## 快速上手

1. 启动游戏，进入关卡编辑器并打开一个可编辑关卡。
2. 点击编辑器左侧的「事件工坊」页签。
3. 选择「批量生成」「批量装饰」「推广」「批量调整」或「批量删除」。
4. 填写楼层、次数和参数，参数支持表达式，例如：

```text
sin(i / n * 2 * pi) * 100 + 50
inc(0, 5)
random(0, 1) * 10 - 5
```

5. 操作只修改内存；确认后点击另存为新 `.adofai` 文件。

完整操作说明见 [docs/操作手册.md](docs/操作手册.md)，表达式语法见 [EXPRESSIONS.md](EXPRESSIONS.md)。

## 从源码构建

需要 Windows、.NET Framework 4.x 的 `csc.exe`，以及一份装好 UMM 的游戏本体（用于引用游戏 DLL）。不需要安装 .NET SDK。

```powershell
.\build.ps1
```

游戏不在默认路径时：

```powershell
.\build.ps1 -GameDir "D:\path\to\A Dance of Fire and Ice"
```

构建产物：

```text
dist\
  AdofaiEventStudio.dll
  Info.json
..\AdofaiEventStudio-1.0.0.zip
```

构建脚本默认还会运行冒烟测试和 UMM 入口测试；跳过测试：

```powershell
.\build.ps1 -SkipTests
```

## 仓库结构

```text
src/                 模组源码
tests/               冒烟测试与 UMM 入口测试
docs/操作手册.md      完整中文操作手册
EXPRESSIONS.md       表达式语法
Info.json            UMM 模组信息
AdofaiEventStudio.csproj
build.ps1            一键构建与打包
```

## English

**AdofaiEventStudio** is a Unity Mod Manager mod for *A Dance of Fire and Ice* that adds an event workshop tab to the level editor: batch-create events and decorations, propagate `MoveDecorations` animations, shift events across floors, batch-edit numeric fields with math expressions, and batch-delete events by type and floor range. Requires UMM 0.32.4.0 and .NET Framework 4.x to build. Licensed under MIT.

## 许可证

[MIT](LICENSE) © 2026 橚
