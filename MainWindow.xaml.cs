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
    }
}