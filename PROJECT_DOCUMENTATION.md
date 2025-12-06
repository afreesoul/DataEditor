# 游戏数据编辑器 - 项目文档

## 1. 项目概览

使用 C# + WPF (.NET 7) 构建的 Windows 桌面应用，用于编辑游戏 JSON 数据文件。

### UI 布局（三栏）
1. **Data Tables** - 数据表列表（支持目录分组）
2. **Entries** - 当前表的数据行列表
3. **Details** - 选中行的字段编辑（TreeView层级展示）

## 2. 项目结构

```
DataEditor/
├── Commands/           # ICommand 实现
├── Models/
│   ├── DataEntries/    # 数据实体类（继承 BaseDataRow）
│   │   └── Complex/    # 复杂嵌套类型
│   ├── Settings/       # 应用设置模型
│   └── Utils/          # 工具类（ForeignKey, FixedLengthArray等）
├── Services/
│   ├── TableCommentService.cs      # 表注释服务 → table_comments.json
│   ├── FieldCommentService.cs      # 字段注释服务 → field_comments.json
│   └── DirectoryStructureService.cs # 目录结构服务 → directory_structure.json
├── ViewModels/
│   ├── MainViewModel.cs      # 主窗口 ViewModel
│   ├── FieldViewModel.cs     # 字段显示 ViewModel
│   └── SettingsViewModel.cs  # 设置窗口 ViewModel
├── Utils/              # CSV导入导出等工具
├── MainWindow.xaml     # 主界面
├── CommentDialogWindow.xaml  # 注释输入弹框
├── DirectoryDialogWindow.xaml # 目录创建弹框
└── SettingsWindow.xaml # 设置窗口
```

## 3. 核心架构 (MVVM)

### 3.1 数据流
```
JSON文件 → LoadFromFolder() → GameTables集合 → UI绑定
UI编辑 → FieldViewModel.Value → 反射写入对象 → SaveAllToFolder() → JSON文件
```

### 3.2 关键类

| 类 | 职责 |
|---|---|
| `MainViewModel` | 核心控制器，管理表/行选择、命令、数据加载保存 |
| `FieldViewModel` | 单个字段的显示逻辑，支持简单类型/枚举/外键/数组/复杂对象 |
| `GameDataTable` | 数据表模型，包含表名、数据类型、行集合、注释 |
| `BaseDataRow` | 数据行基类，包含 ID、Name、State 公共属性 |

### 3.3 类型映射
`MainViewModel.TableTypeMapping` 字典自动通过反射扫描所有继承 `BaseDataRow` 的类，生成表名到类型的映射（类名复数化为表名）。

## 4. 主要功能

### 4.1 数据编辑
- **支持的字段类型**: 简单类型、枚举（下拉框）、外键（关联下拉）、数组/列表（可展开）、复杂嵌套对象
- **外键**: 使用 `ForeignKey<T>` 泛型类型，自动显示关联表的选项
- **固定长度数组**: 使用 `FixedLengthArray<T>` 或在属性初始化时指定长度

### 4.2 注释功能
- **表注释**: DataTables区域右键 → 注释，保存到 `table_comments.json`
- **字段注释**: Details区域右键字段 → 注释，保存到 `field_comments.json`
- 注释会显示在名称后面，格式：`名称 - 注释内容`

### 4.3 目录管理
- DataTables区域支持创建目录分组
- 表可以拖放到目录中
- 结构保存到 `directory_structure.json`

### 4.4 CSV导入/导出
- 嵌套对象和数组会被"扁平化"为多列（如 `Auras.0.Name`）
- 列顺序：基类属性优先，然后按代码声明顺序

## 5. 扩展指南

### 5.1 添加新数据表
1. 在 `Models/DataEntries/` 创建新类，继承 `BaseDataRow`
2. 定义属性（支持简单类型、枚举、`ForeignKey<T>`、`List<T>`、嵌套类）
3. 重新编译，表会自动出现（通过反射自动发现）

```csharp
public class Skill : BaseDataRow
{
    public int Damage { get; set; }
    public SkillType Type { get; set; }  // 枚举
    public ForeignKey<Monster> Target { get; set; } = new();  // 外键
    public List<string> Tags { get; set; } = new();  // 数组
}
```

### 5.2 添加新命令
1. 在 `MainViewModel` 添加 `ICommand` 属性
2. 在构造函数中初始化（使用 `RelayCommand`）
3. 在 XAML 中绑定

### 5.3 添加新服务
1. 在 `Services/` 创建服务类
2. 在 `MainViewModel` 添加私有字段并在 `LoadFromFolder()` 中初始化
3. 数据保存到 Data 目录的 JSON 文件

## 6. 注意事项

- **ContextMenu绑定**: WPF的ContextMenu不在可视树中，需要通过 `Tag` 属性传递DataContext
- **文件锁定**: 编译前需关闭正在运行的程序
- **换行符**: 项目使用 LF 换行符
