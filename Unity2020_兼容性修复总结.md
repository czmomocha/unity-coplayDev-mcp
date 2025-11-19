# Unity 2020.3 LTS 兼容性修复总结

## 修复的问题

### 1. PrefabStage 和 PrefabStageUtility 命名空间问题
**错误**: `The name 'PrefabStageUtility' does not exist in the current context`

**原因**: Unity 2020.3 中这些类在 `UnityEditor.Experimental.SceneManagement` 命名空间中

**修复的文件**:
- `MCPForUnity/Editor/Tools/Prefabs/ManagePrefabs.cs`
- `MCPForUnity/Editor/Resources/Editor/PrefabStage.cs`

**修复方法**: 
- 添加 `using UnityEditor.Experimental.SceneManagement;`
- 添加类型别名：`using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;`

### 2. PrefabStageUtility.OpenPrefab 方法不存在
**错误**: `'PrefabStageUtility' does not contain a definition for 'OpenPrefab'`

**原因**: Unity 2020.3 的 `PrefabStageUtility` 没有 `OpenPrefab` 方法

**修复的文件**:
- `MCPForUnity/Editor/Tools/Prefabs/ManagePrefabs.cs`

**修复方法**: 使用 `AssetDatabase.OpenAsset()` 打开预制体，然后用 `PrefabStageUtility.GetCurrentPrefabStage()` 获取

```csharp
// 旧代码（Unity 2021+）
PrefabStage stage = PrefabStageUtility.OpenPrefab(sanitizedPath);

// 新代码（Unity 2020.3）
bool opened = AssetDatabase.OpenAsset(prefabAsset);
PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
```

### 3. ZipFile 类不可用
**错误**: `The type name 'ZipFile' could not be found in the namespace 'System.IO.Compression'`

**原因**: Unity 2020.3 中 `ZipFile` 需要额外的程序集引用

**修复的文件**:
- `MCPForUnity/Editor/Helpers/ServerInstaller.cs`

**修复方法**: 使用 `ZipArchive` 手动解压文件

```csharp
// 旧代码
ZipFile.ExtractToDirectory(tempZip, tempExtractDir);

// 新代码
using (var fileStream = new FileStream(tempZip, FileMode.Open, FileAccess.Read))
using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
{
    foreach (var entry in archive.Entries)
    {
        // 手动提取每个文件
    }
}
```

### 4. 范围运算符 `[..]` 不支持
**错误**: `Predefined type 'System.Range' is not defined or imported`

**原因**: C# 7.3 不支持范围运算符

**修复的文件**:
- `MCPForUnity/Editor/MCPForUnityBridge.cs`
- `MCPForUnity/Editor/Helpers/PortManager.cs`

**修复方法**: 
- `path[7..]` → `path.Substring(7)`
- `[..50]` → `Substring(0, 50)`

### 5. FindObjectsByType 和 FindObjectsInactive 不存在
**错误**: `'Object' does not contain a definition for 'FindObjectsByType'`

**原因**: Unity 2020.3 使用旧的 `FindObjectsOfType` API

**修复的文件**:
- `MCPForUnity/Editor/Tools/ManageGameObject.cs`

**修复方法**: 使用 `FindObjectsOfType(componentType, searchInactive)` 替代新 API

### 6. Lambda 表达式类型推断问题
**错误**: `Type of conditional expression cannot be determined between 'lambda expression' and 'lambda expression'`

**原因**: C# 7.3 的类型推断限制

**修复的文件**:
- `MCPForUnity/Editor/Tools/ManageGameObject.cs`

**修复方法**: 显式指定 `Func<Type, bool>` 类型

### 7. 变量名作用域冲突
**错误**: `A local or parameter named 'table' cannot be declared in this scope`

**原因**: 模式匹配中的变量名与外部作用域冲突

**修复的文件**:
- `MCPForUnity/Editor/External/Tommy.cs`

**修复方法**: 将 `table` 重命名为 `tbl`

### 8. 其他 C# 9.0/10.0 语法修复
之前已修复的问题：
- 目标类型推断 `new()` → 完整类型声明
- 属性模式匹配 `{ IsInline: false }` → 传统布尔表达式
- `is not` 模式 → `!(is ...)`
- `or` 和 `and` 模式 → `||` 和 `&&`
- `static` lambda → 移除 static 修饰符

## 已知问题

### com.unity.visualscripting 包错误
**错误**: `Library\PackageCache\com.unity.visualscripting@1.9.4\Runtime\VisualScripting.Core\Unity\SceneSingleton.cs(101,31): error CS0103: The name 'PrefabStageUtility' does not exist in the current context`

**状态**: 这是 Unity 官方包的问题，无法直接修改

**影响**: 不影响 MCP For Unity 插件的核心功能

**解决方案**: 
1. 可以忽略此错误，MCP 插件仍可正常使用
2. 或者在 Package Manager 中移除/禁用 Visual Scripting 包（如果项目不需要）

## 验证步骤

1. 在 Unity 2020.3.25 LTS 中打开项目
2. 等待编译完成
3. 检查菜单栏是否出现 `Window > MCP For Unity > Open MCP Window`
4. 如果菜单出现，说明插件已成功加载

## 修改统计

总计修改文件数: **20+个**
- 16个文件（C# 语法修复）
- 2个文件（PrefabStage 命名空间修复）
- 2个文件（API 兼容性修复）

## Unity 2020.3 vs 新版本 API 对照表

| 功能 | Unity 2021+ | Unity 2020.3 |
|------|-------------|--------------|
| 打开 Prefab Stage | `PrefabStageUtility.OpenPrefab()` | `AssetDatabase.OpenAsset()` + `GetCurrentPrefabStage()` |
| 解压 ZIP | `ZipFile.ExtractToDirectory()` | `ZipArchive` 手动解压 |
| 查找对象 | `FindObjectsByType()` | `FindObjectsOfType()` |
| 字符串切片 | `str[7..]` | `str.Substring(7)` |
| C# 版本 | C# 9.0/10.0 | C# 7.3 |

## 注意事项

1. Unity 2020.3 仅支持 C# 7.3，不支持 C# 9.0/10.0 的新特性
2. 所有范围运算符都需要改为 `Substring()` 方法
3. 所有模式匹配都需要改为传统的布尔表达式
4. Unity 2020.3 的 API 与新版本有差异，需要使用旧版 API
5. `PrefabStageUtility` 在 2020.3 中是实验性 API，功能有限
