### Codebuddy: MCP for Unity 配置指南

#### 关于 Codebuddy

Codebuddy 是一款智能AI编程助手，支持通过MCP（Model Context Protocol）协议与Unity Editor进行集成。本文档将帮助你在Codebuddy中正确配置MCP for Unity。

---

## 快速配置指南

### 方式一：自动配置（推荐）

1. 在Unity中打开 `Window > MCP for Unity`
2. 在MCP客户端选择中找到 **Codebuddy**（如果支持）
3. 点击 **Auto Configure** 按钮
4. 等待配置完成，查看绿色状态指示器 🟢 和 "Connected ✓"

### 方式二：手动配置

如果自动配置不可用或失败，请按照以下步骤手动配置：

#### 1. 找到Codebuddy配置文件

Codebuddy的MCP配置文件通常位于：

**Windows:**
```
%APPDATA%\Codebuddy\mcp.json
或
%USERPROFILE%\.codebuddy\mcp.json
```

**macOS:**
```
~/Library/Application Support/Codebuddy/mcp.json
或
~/.codebuddy/mcp.json
```

**Linux:**
```
~/.config/codebuddy/mcp.json
或
~/.codebuddy/mcp.json
```

> **提示**: 如果文件不存在，请手动创建该文件。

#### 2. 编辑配置文件

在配置文件中添加或更新 `mcpServers` 部分：

**Windows 配置示例:**

```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "C:\\Users\\YOUR_USERNAME\\AppData\\Local\\Microsoft\\WinGet\\Links\\uv.exe",
      "args": [
        "--directory",
        "C:\\Users\\YOUR_USERNAME\\AppData\\Local\\Programs\\UnityMCP\\UnityMcpServer\\src",
        "run",
        "server.py"
      ]
    }
  }
}
```

**macOS 配置示例:**

```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "uv",
      "args": [
        "--directory",
        "/Users/YOUR_USERNAME/Library/Application Support/UnityMCP/UnityMcpServer/src",
        "run",
        "server.py"
      ]
    }
  }
}
```

**Linux 配置示例:**

```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "uv",
      "args": [
        "--directory",
        "/home/YOUR_USERNAME/.local/share/UnityMCP/UnityMcpServer/src",
        "run",
        "server.py"
      ]
    }
  }
}
```

> **重要**: 请将 `YOUR_USERNAME` 替换为你的实际用户名，并确保路径使用正确的斜杠（Windows使用双反斜杠 `\\`）。

#### 3. 验证配置

保存配置文件后：

1. 重启 Codebuddy（配置文件通常不会热重载）
2. 在Unity中检查 `Window > MCP for Unity` 窗口的连接状态
3. 在Codebuddy中尝试发送Unity相关的指令，例如：
   - "列出当前场景中的所有GameObject"
   - "创建一个红色的立方体"

---

## Windows 系统：uv 路径问题诊断与修复

### 问题描述

在Windows系统上，某些机器可能存在多个 `uv.exe` 位置。如果配置了不稳定的路径，可能导致：
- Codebuddy无法启动MCP for Unity服务器
- 配置文件在刷新后自动恢复到错误的路径
- 连接状态显示"无法连接"或"服务器未启动"

### 常见症状

- Codebuddy显示MCP for Unity服务器但从不连接
- Unity或MCP for Unity窗口刷新时，配置文件中的 `command` 路径被自动改写
- 错误日志显示"找不到uv"或"命令执行失败"

### 真实案例

**❌ 错误/不稳定的路径（自动选择）:**
```
C:\Users\username.local\bin\uv.exe  (格式错误，非标准路径)
C:\Users\username\AppData\Local\Microsoft\WinGet\Packages\astral-sh.uv_Microsoft.Winget.Source_8wekyb3d8bbwe\uv.exe
```

**✅ 正确/稳定的路径（推荐使用）:**
```
C:\Users\username\AppData\Local\Microsoft\WinGet\Links\uv.exe
```

### 快速修复步骤（推荐）

1. 在Unity中打开 `Window > MCP for Unity`
2. 如果看到 "uv Not Found"，点击 **"Choose `uv` Install Location"** 按钮
3. 浏览并选择以下路径：
   ```
   C:\Users\<YOUR_USERNAME>\AppData\Local\Microsoft\WinGet\Links\uv.exe
   ```
4. 如果uv已找到但路径不正确，仍然点击 **"Choose `uv` Install Location"** 并选择上述 `Links\uv.exe` 路径
5. 点击 **"Auto Configure"**（或重新配置你的MCP客户端）
6. 重启 Codebuddy

这会在Unity Editor中保存一个持久化的覆盖设置（键：`MCPForUnity.UvPath`），防止MCP for Unity在后续操作中自动改写配置。

### 验证修复

1. **确认配置文件位置**

   根据你的系统找到Codebuddy的配置文件（参见上文"找到Codebuddy配置文件"部分）

2. **检查配置内容**

   配置文件应该类似于：
   ```json
   {
     "mcpServers": {
       "unityMCP": {
         "command": "C:\\Users\\YOUR_USERNAME\\AppData\\Local\\Microsoft\\WinGet\\Links\\uv.exe",
         "args": [
           "--directory",
           "C:\\Users\\YOUR_USERNAME\\AppData\\Local\\Programs\\UnityMCP\\UnityMcpServer\\src",
           "run",
           "server.py"
         ]
       }
     }
   }
   ```

3. **手动测试命令**

   在PowerShell中运行以下命令验证路径是否正确：
   ```powershell
   "C:\Users\YOUR_USERNAME\AppData\Local\Microsoft\WinGet\Links\uv.exe" --directory "C:\Users\YOUR_USERNAME\AppData\Local\Programs\UnityMCP\UnityMcpServer\src" run server.py
   ```

   如果命令运行无错误，重启Codebuddy后应该能够正常连接。

### 为什么会出现这个问题

- Windows上可能存在多个 `uv.exe`（WinGet Packages路径、WinGet Links shim、Python Scripts等）
- Links shim是最稳定的目标，适合GUI应用程序启动
- 早期版本的自动配置可能选择了第一个找到的路径，并在刷新时重新写入配置
- 通过MCP窗口选择路径可以固定一个已知良好的绝对路径，防止自动改写

### 为什么使用 WinGet Links Shim

- Windows通常有多个 `uv.exe` 安装位置，GUI客户端（Codebuddy/Cursor/VSCode）可能以受限的 `PATH` 启动
- 使用绝对路径比 `"command": "uv"` 更安全
- WinGet在以下位置发布稳定的启动shim：
  - **用户范围**: `%LOCALAPPDATA%\Microsoft\WinGet\Links\uv.exe`
  - **机器范围**: `C:\Program Files\WinGet\Links\uv.exe`
- 这些shim在升级后仍然有效，是可移植的入口点
- `Packages` 根目录是有效负载所在位置，可能在更新时发生变化，应避免直接指向它

### 推荐做法

- **优先使用** 上述WinGet Links shim路径。如果存在，通过 "Choose `uv` Install Location" 选择其中一个
- 如果Unity窗口持续改写到不同的 `uv.exe`，再次选择Links shim；MCP for Unity会保存固定的覆盖设置并停止自动改写
- 如果两个Links路径都不存在，合理的备选方案是 `~/.local/bin/uv.exe`（uv tools bin）或Scoop shim，但Links是首选

### 额外注意事项

- 修改 `mcp.json` 后重启Codebuddy；它不总是热重载该文件
- 如果你的Unity项目文件夹中也有项目级的 `.codebuddy\mcp.json`，该文件会覆盖全局配置

---

## 常见问题排查

### 问题1: Codebuddy无法连接到MCP服务器

**可能原因:**
- Unity Editor未打开
- MCP for Unity包未正确安装
- 配置文件路径错误
- uv未安装或路径不正确

**解决方案:**
1. 确保Unity Editor已打开并加载了项目
2. 在Unity中检查 `Window > MCP for Unity` 窗口状态
3. 验证配置文件中的路径是否正确（特别是 `--directory` 参数）
4. 在终端中运行 `uv --version` 确认uv已安装
5. 尝试手动运行服务器命令（参见上文"验证修复"部分）

### 问题2: 配置文件被自动改写

**可能原因:**
- 未设置固定的uv路径覆盖
- 使用了不稳定的uv路径

**解决方案:**
1. 在Unity的MCP for Unity窗口中使用 "Choose `uv` Install Location"
2. 选择稳定的WinGet Links路径
3. 这会在Editor中保存持久化设置，防止自动改写

### 问题3: Codebuddy显示"工具不可用"

**可能原因:**
- MCP服务器未成功启动
- Unity Bridge未运行
- 网络连接问题

**解决方案:**
1. 重启Unity Editor
2. 重启Codebuddy
3. 检查Unity Console是否有错误信息
4. 查看MCP服务器日志：
   - Windows: `%USERPROFILE%\AppData\Local\Programs\UnityMCP\Logs\unity_mcp_server.log`
   - macOS: `~/Library/Application Support/UnityMCP/Logs/unity_mcp_server.log`
   - Linux: `~/.local/share/UnityMCP/Logs/unity_mcp_server.log`

### 问题4: Python或uv未安装

**解决方案:**

1. **安装Python 3.10或更高版本**
   - 访问 [Python官网](https://www.python.org/downloads/) 下载安装

2. **安装uv**
   ```bash
   # macOS / Linux
   curl -LsSf https://astral.sh/uv/install.sh | sh

   # Windows (PowerShell)
   winget install --id=astral-sh.uv -e
   ```

3. 安装完成后重启终端和Codebuddy

---

## 使用示例

配置成功后，你可以在Codebuddy中使用自然语言与Unity交互：

### 场景管理
```
"创建一个新场景并添加一个主相机和方向光"
"列出当前场景中的所有GameObject"
"保存当前场景为 MainLevel"
```

### GameObject操作
```
"创建一个红色的立方体，位置在(0, 1, 0)"
"给Player对象添加Rigidbody组件"
"删除所有名为Enemy的对象"
```

### 脚本管理
```
"创建一个PlayerController脚本，包含WASD移动功能"
"读取PlayerController.cs的内容"
"检查控制台是否有编译错误"
```

### 资源操作
```
"导入Assets/Models文件夹中的所有FBX文件"
"创建一个名为PlayerPrefab的预制体"
"列出项目中所有的材质"
```

### 编辑器控制
```
"进入播放模式"
"停止播放"
"运行所有EditMode测试"
```

---

## 高级配置

### 多Unity实例支持

MCP for Unity支持同时连接多个Unity Editor实例。要切换目标实例：

1. 询问Codebuddy："列出所有Unity实例"
2. 使用 `set_active_instance` 工具指定目标实例
3. 后续所有操作将路由到该实例

### 环境变量配置

你可以通过环境变量自定义MCP服务器行为：

```bash
# 禁用遥测
DISABLE_TELEMETRY=true

# 启用调试日志
LOG_LEVEL=DEBUG

# 指定默认Unity实例
UNITY_MCP_DEFAULT_INSTANCE="MyProject"
```

在Windows中设置环境变量：
```powershell
# 临时设置（当前会话）
$env:DISABLE_TELEMETRY="true"

# 永久设置（系统级）
[System.Environment]::SetEnvironmentVariable("DISABLE_TELEMETRY", "true", "User")
```

### 项目级配置

如果你希望为特定Unity项目使用不同的配置，可以在项目根目录创建 `.codebuddy\mcp.json`，该配置会覆盖全局配置。

---

## 参考资源

- **MCP for Unity 主仓库**: [https://github.com/CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)
- **Discord社区**: [https://discord.gg/y4p8KfzrN4](https://discord.gg/y4p8KfzrN4)
- **完整文档**: [README.md](https://github.com/CoplayDev/unity-mcp#readme)
- **自定义工具开发**: [CUSTOM_TOOLS.md](https://github.com/CoplayDev/unity-mcp/blob/main/docs/CUSTOM_TOOLS.md)
- **开发者指南**: [README-DEV.md](https://github.com/CoplayDev/unity-mcp/blob/main/docs/README-DEV.md)
- **WinGet Links说明**: [GitHub讨论](https://github.com/microsoft/winget-pkgs/discussions/184459)
- **uv工具文档**: [Astral文档](https://docs.astral.sh/uv/)

---

## 获取帮助

如果遇到问题：

1. 查看本文档的"常见问题排查"部分
2. 检查Unity Console和MCP服务器日志
3. 访问 [GitHub Issues](https://github.com/CoplayDev/unity-mcp/issues) 搜索或提交问题
4. 加入 [Discord社区](https://discord.gg/y4p8KfzrN4) 获取实时帮助

---

**注意**: 本文档基于MCP for Unity v7.0.0编写。如果你使用的是不同版本，某些细节可能有所不同。
