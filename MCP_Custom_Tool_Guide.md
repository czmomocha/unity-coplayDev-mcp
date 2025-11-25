# Unity MCP 自定义工具修改、验证与发布指南

## 1. 自定义工具生命周期（增 / 删 / 改）

### 1.1 核心组件
- **PythonToolsAsset**：在 Unity 中创建 ScriptableObject，将任意项目内的 `.py` 文件加入列表后，系统会在构建服务器时同步这些脚本。`Window > MCP For Unity > Tool Sync` 可重新导入或强制同步。
- **Python 端装饰器**：在脚本中使用 `@mcp_for_unity_tool`，配合 `ctx.info()` 记录日志、`send_command_with_retry` 调 Unity，并返回统一结构 `{"success": bool, "message": str, "data": Any}`。
- **C# 端处理器**：创建带 `[McpForUnityTool("command")]` 的静态类，实现 `public static object HandleCommand(JObject @params)` 并通过 `Response.Success/Error` 返回结果。

### 1.2 增加工具推荐流程
1. **需求拆解**：确定 Python 输入/输出与 Unity 行为。
2. **实现 Python 脚本**：使用注释良好的 `Annotated` 参数、日志、统一返回值，并在需要时整合 Unity 命令调用。
3. **资产注册**：将脚本加入 `PythonToolsAsset`，触发自动同步。
4. **实现 C# Handler**：解析参数、校验输入、封装 Unity 逻辑。
5. **重建服务器**：在 MCP for Unity 窗口点击 **Rebuild Server** 以刷新 Python + C# 两侧注册。

### 1.3 删除 / 禁用工具
- 从 `PythonToolsAsset` 移除脚本或删除文件，哈希同步系统会在下一次同步时自动清理 `UnityMcpServer~/src/tools/custom/` 中的残留。
- 删除或注释掉带 `[McpForUnityTool]` 的类，Unity 在启动/刷新程序集时会重新扫描并更新命令表。
- 若工具未消失，可通过 Tool Sync 面板手动同步并重建服务器，再观察 Unity Console 与 Python 日志确认状态。

## 2. 验证与质量把关

### 2.1 同步与重建
- 修改后执行 **Rebuild Server**，确保最新 Python 文件被复制、装饰器注册完成，同时 Unity 侧重新编译并注册处理器。
- 自动同步会在 Unity 启动、文件变更或资产更新时触发，必要时通过 Tool Sync 面板的 **Sync Now / Reimport / Auto-Sync** 控制。

### 2.2 日志与排查
- **Python 日志**：`~/Library/Application Support/UnityMCP/Logs/unity_mcp_server.log`，启动时关注 “Registered X MCP tools”。
- **Unity Console**：查看 “MCP-FOR-UNITY: Auto-discovered X tools” 以及任何警告（例如缺少 `HandleCommand`）。
- **Checklist**：若工具缺失，确认 `.py` 已加入资产并同步、`[McpForUnityTool]` 存在、命令名一致，再执行 Rebuild 和日志检查。

## 3. 验证后的开发环境替换

### 3.1 `deploy-dev.bat` — 一键热替换
1. 运行脚本并输入 Unity Package Cache 路径、MCP Server 安装路径（默认 `%LOCALAPPDATA%\UnityMCP\UnityMcpServer\src`）以及备份目录（默认桌面）。
2. 脚本会验证路径、创建带时间戳的备份，然后将 `MCPForUnity/Editor` 与 `UnityMcpServer~/src` 拷贝到正在使用的 Unity 包缓存与本地服务器，实现快速联调。
3. 完成后重启 Unity 和 MCP 客户端即可加载新版本。

### 3.2 `restore-dev.bat` — 回滚保障
- 选择备份目录中的历史快照，脚本会清空当前安装位置并恢复 Unity Bridge（Editor/Runtime）以及 Python Server。

### 3.3 快速切换 / 发布验证
- 在多个包来源之间快速切换可使用 `python mcp_source.py --choice {1|2|3}`，对照 `manifest.json` 定位目标包（上游、当前分支或本地目录）。
- 由于 Python 服务器源码已随 Unity 包一同打包在 `UnityMcpServer~/src`，下游项目只需更新包即可获得最新服务器，无需额外安装流程。
- 推荐遵循官方 Workflow：修改 → deploy → Unity 中测试 → 迭代 → restore，确保开发环境干净可控。

## 4. 生产环境发布与替换

### 4.1 包分发路径
- Unity Package Manager：`Add package from git URL...` 输入 `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity`，可配合 Git tag 固定版本。
- OpenUPM：`openupm add com.coplaydev.unity-mcp` 向团队分发稳定包。

### 4.2 生产替换建议
1. **锁定版本**：更新 `MCPForUnity/package.json` 并创建 Git tag。
2. **同步 Python Server**：包内已包含服务器源码，项目升级包后即可获得新版本。
3. **发布前验证清单**：
   - `uv pip install -e ".[dev]"` 并执行 `uv run pytest tests/ -v`（含 `-m integration` / `-m unit`）。
   - Tool Sync + Rebuild Server，检查 Unity Console / Python 日志。
   - 使用 `deploy-dev.bat` 做真实项目冒烟测试，用 `restore-dev.bat` 可随时回滚。

## 5. 持续集成与自动化建议

| 层次 | 推荐动作 | 说明 |
|------|----------|------|
| Python/MCP Server 单元与集成测试 | `uv run pytest tests/ -v (-m unit / -m integration)` | 在 `UnityMcpServer~/src` 执行，可在 CI 中直接复用 `uv` 环境 |
| Unity 连接压力测试 | `tools/stress_mcp.py --clients N --duration T`（可选） | 验证多客户端并发与刷新流程，建议作为手动或夜间任务 |
| GitHub Actions NLU 套件 | 触发 `Claude NL/T Full Suite (Unity live)`，需配置 `ANTHROPIC_API_KEY` 与 Unity 凭据 | 生成 JUnit + Markdown 报告，辅助回归 |
| 包源一致性 | `mcp_source.py` 快速切换 upstream / 分支 / 本地 | 确认不同来源的包表现一致 |

## 6. 快速清单
1. 设计：定义 Python + C# 接口，记录描述与参数。
2. 实现：完成脚本、Handler，加入 `PythonToolsAsset`。
3. 验证：Sync/Auto-Sync → Rebuild Server → 检查日志。
4. 部署：`deploy-dev.bat` 热替换 → Unity 实测 → `restore-dev.bat` 回滚。
5. 发布：pytest /（可选）压力测试 / GitHub Actions → 更新版本与 Tag → 下游通过 Git/OpenUPM 获取。

---

通过以上流程，可系统化地增删自定义工具、验证稳定性，并将改动安全推广到开发与生产环境。