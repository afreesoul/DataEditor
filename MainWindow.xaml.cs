using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GameDataEditor.ViewModels;
using GameDataEditor.Models;

namespace GameDataEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogScrollViewer.ScrollToBottom();
        }

        private void EntriesListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (DataContext is MainViewModel viewModel && viewModel.DeleteRowCommand.CanExecute(null))
                {
                    viewModel.DeleteRowCommand.Execute(null);
                }
            }
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem treeViewItem && 
                treeViewItem.DataContext is Models.IDataItem dataItem)
            {
                if (dataItem.ItemType == Models.DataItemType.Directory)
                {
                    // 切换目录展开状态
                    dataItem.IsExpanded = !dataItem.IsExpanded;
                    
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.SaveDirectoryStructure();
                    }
                    e.Handled = true;
                }
            }
        }

        private void TablesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                // 直接设置SelectedTable属性，它会自动触发PropertyChanged
                if (e.NewValue is IDataItem selectedItem && 
                    selectedItem.ItemType == DataItemType.Table && 
                    selectedItem is DataTableWrapper tableWrapper)
                {
                    viewModel.SelectedTable = tableWrapper.Table;
                }
                else
                {
                    viewModel.SelectedTable = null;
                }
            }
        }

        private void ItemContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && 
                contextMenu.DataContext is IDataItem dataItem &&
                DataContext is MainViewModel viewModel)
            {
                // 清除现有菜单项
                contextMenu.Items.Clear();
                
                // 如果是目录，添加重命名和删除菜单项
                if (dataItem.ItemType == DataItemType.Directory)
                {
                    var renameMenuItem = new MenuItem { Header = "重命名" };
                    renameMenuItem.Click += (s, args) => 
                    {
                        if (dataItem is DataDirectory directory)
                        {
                            RenameDirectory(directory, viewModel);
                        }
                    };
                    contextMenu.Items.Add(renameMenuItem);
                    
                    var deleteMenuItem = new MenuItem { Header = "删除目录" };
                    deleteMenuItem.Click += (s, args) => 
                    {
                        if (dataItem is DataDirectory directory)
                        {
                            DeleteDirectory(directory, viewModel);
                        }
                    };
                    contextMenu.Items.Add(deleteMenuItem);
                    contextMenu.Items.Add(new Separator());
                }
                
                // 添加注释菜单项
                var commentMenuItem = new MenuItem { Header = "注释" };
                commentMenuItem.Click += (s, args) => 
                {
                    viewModel.AddTableComment(dataItem);
                };
                contextMenu.Items.Add(commentMenuItem);
                
                // 添加移动相关的菜单项
                if (dataItem.ItemType == DataItemType.Table)
                {
                    contextMenu.Items.Add(new Separator());
                    
                    // 如果表在目录中，显示"移动到外层"
                    if (dataItem.Parent != null)
                    {
                        var moveToOuterMenuItem = new MenuItem { Header = "移动到外层" };
                        moveToOuterMenuItem.Click += (s, args) => 
                        {
                            if (dataItem is DataTableWrapper tableWrapper)
                            {
                                MoveTableToOuter(tableWrapper, viewModel);
                            }
                        };
                        contextMenu.Items.Add(moveToOuterMenuItem);
                    }
                    // 如果表不在目录中，显示"移动到目录"
                    else
                    {
                        var moveToDirectoryMenuItem = new MenuItem { Header = "移动到目录" };
                        
                        // 添加所有目录作为子菜单项
                        foreach (var directory in viewModel.DataItems.OfType<DataDirectory>())
                        {
                            var directoryMenuItem = new MenuItem { Header = directory.Name };
                            directoryMenuItem.Click += (s, args) => 
                            {
                                if (dataItem is DataTableWrapper tableWrapper)
                                {
                                    MoveTableToDirectory(tableWrapper, directory, viewModel);
                                }
                            };
                            moveToDirectoryMenuItem.Items.Add(directoryMenuItem);
                        }
                        
                        contextMenu.Items.Add(moveToDirectoryMenuItem);
                    }
                }
                
                contextMenu.Items.Add(new Separator());
                
                // 添加创建目录菜单项
                var createDirectoryMenuItem = new MenuItem { Header = "创建目录" };
                createDirectoryMenuItem.Click += (s, args) => 
                {
                    viewModel.CreateDirectory();
                };
                contextMenu.Items.Add(createDirectoryMenuItem);
            }
        }

        private void MoveTableToDirectory(DataTableWrapper tableWrapper, DataDirectory targetDirectory, MainViewModel viewModel)
        {
            if (tableWrapper == null || targetDirectory == null) return;
            
            // 从原位置移除
            if (tableWrapper.Parent != null)
            {
                if (tableWrapper.Parent is DataDirectory oldParent)
                {
                    oldParent.RemoveChild(tableWrapper);
                }
                else
                {
                    viewModel.DataItems.Remove(tableWrapper);
                }
            }
            else
            {
                viewModel.DataItems.Remove(tableWrapper);
            }

            // 添加到目标目录
            targetDirectory.AddChild(tableWrapper);
            targetDirectory.IsExpanded = true;
            
            viewModel.SaveDirectoryStructure();
            viewModel.Log($"将表 '{tableWrapper.Name}' 移动到目录 '{targetDirectory.Name}'");
        }

        private void MoveTableToOuter(DataTableWrapper tableWrapper, MainViewModel viewModel)
        {
            if (tableWrapper == null) return;
            
            // 从目录中移除
            if (tableWrapper.Parent is DataDirectory parentDirectory)
            {
                parentDirectory.RemoveChild(tableWrapper);
                
                // 添加到根层级
                viewModel.DataItems.Add(tableWrapper);
                tableWrapper.Parent = null;
                
                viewModel.SaveDirectoryStructure();
                viewModel.Log($"将表 '{tableWrapper.Name}' 从目录 '{parentDirectory.Name}' 移动到外层");
            }
        }

        private void RenameDirectory(DataDirectory directory, MainViewModel viewModel)
        {
            if (directory == null || viewModel == null) return;
            
            // 显示输入对话框获取新名称
            var dialog = new DirectoryDialogWindow("重命名目录", directory.Name);
            dialog.Owner = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.DirectoryName))
            {
                string newName = dialog.DirectoryName.Trim();
                
                // 检查名称是否重复（排除自身）
                bool nameExists = viewModel.DataItems
                    .OfType<DataDirectory>()
                    .Any(d => d != directory && d.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                
                if (nameExists)
                {
                    MessageBox.Show($"目录名称 '{newName}' 已存在，请使用其他名称。", "名称重复", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                string oldName = directory.Name;
                directory.Name = newName;
                
                // 保存目录结构
                viewModel.SaveDirectoryStructure();
                viewModel.Log($"将目录 '{oldName}' 重命名为 '{newName}'");
                
                // 刷新TreeView显示 - 通过重新加载目录结构实现
                viewModel.ReloadDirectoryStructure();
            }
        }

        private void DeleteDirectory(DataDirectory directory, MainViewModel viewModel)
        {
            if (directory == null || viewModel == null) return;
            
            // 确认删除操作
            string message = "确定要删除目录 '" + directory.Name + "'?" 
                + (directory.Children.Count > 0 ? $"\n\n该目录包含 {directory.Children.Count} 个表，删除后这些表将移动到外层。" : "");
                
            var result = MessageBox.Show(message, "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            
            if (result != MessageBoxResult.Yes) return;
            
            // 如果目录包含表，将它们移动到外层
            if (directory.Children.Count > 0)
            {
                // 复制子项列表以避免在循环时修改集合
                var childrenToMove = directory.Children.ToList();
                
                foreach (var child in childrenToMove)
                {
                    if (child is DataTableWrapper tableWrapper)
                    {
                        // 从目录中移除表
                        directory.RemoveChild(tableWrapper);
                        
                        // 添加到根层级
                        viewModel.DataItems.Add(tableWrapper);
                        tableWrapper.Parent = null;
                        
                        viewModel.Log($"将表 '{tableWrapper.Name}' 从目录 '{directory.Name}' 移动到外层");
                    }
                }
            }
            
            // 从数据项中移除目录
            viewModel.DataItems.Remove(directory);
            
            // 保存目录结构
            viewModel.SaveDirectoryStructure();
            viewModel.Log($"删除目录 '{directory.Name}'" + 
                (directory.Children.Count > 0 ? $"，并将 {directory.Children.Count} 个表移动到外层" : ""));
            
            // 刷新TreeView显示
            viewModel.ReloadDirectoryStructure();
        }
    }
}