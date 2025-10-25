# 项目架构文档

## 项目概述

DataEditor 是一个基于 WPF 的桌面应用程序，用于编辑和管理游戏数据。支持数据加载、保存、CSV 导入/导出等功能。

## 技术栈

- **框架**: .NET 7.0
- **UI**: WPF (XAML)
- **数据绑定**: MVVM 模式

## 核心模块

### MainWindow

- **功能**: 主界面，展示数据表、条目和详细信息。
- **特性**:
  - 数据表列表
  - 条目增删改查
  - 详细信息编辑

### SettingsWindow

- **功能**: 设置界面，管理数据文件夹路径和 CSV 导出/导入路径。
- **特性**:
  - 路径配置
  - 默认展开节点设置

### ViewModels

- **MainViewModel**: 处理主界面的业务逻辑。
- **SettingsViewModel**: 处理设置界面的业务逻辑。

### Models

- **AppSettings**: 存储应用程序设置（如数据文件夹路径）。

## 功能特性

1. **数据加载与保存**
2. **CSV 导入/导出**
3. **设置管理**
4. **条目增删改查**

## 项目结构

- **DataEditor.csproj**: 项目配置文件，定义依赖和目标框架。
- **MainWindow.xaml**: 主界面布局文件。
- **MainWindow.xaml.cs**: 主界面逻辑代码。
- **SettingsWindow.xaml**: 设置界面布局文件。
- **SettingsWindow.xaml.cs**: 设置界面逻辑代码。
- **ViewModels/**: 视图模型目录。
- **Models/**: 数据模型目录。