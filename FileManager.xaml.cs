using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Android_Transfer_Protocol
{
    /// <summary>
    /// FileManager.xaml 的交互逻辑
    /// </summary>
    public partial class FileManager : Window
    {
        private readonly ObservableCollection<AFile> FilesListData = new ObservableCollection<AFile>();
        private AFile CurrentFile = null;

        public FileManager()
        {
            InitializeComponent();
            /* 绑定数据源 */
            FilesList.ItemsSource = FilesListData;
            Reflush();
        }

        /**<summary>检查连接状态</summary>**/
        private bool Check()
        {
            if (!Adb.CheckPath())
            {
                MessageBox.Show($"\"{Adb.Device.Model}\" {Properties.Resources.ConnectFailed}",
                                Properties.Resources.Error,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                new DeviceChoose().Show();
                Close();
            }
            return true;
        }

        /**<summary>用于处理警告提示信息</summary>**/
        private bool ShowWarnMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            MessageBox.Show(message,
                            Properties.Resources.Warning,
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
            return true;
        }

        /**<summary>用于处理错误提示信息</summary>**/
        private bool ShowErrMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            MessageBox.Show(message,
                            Properties.Resources.Error,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
            return true;
        }


        /**<summary>用于处理数据列表发生变化后的事件</summary>**/
        private void LoadDir(string message)
        {
            PathBar.Text = Adb.Path;
            if (ShowWarnMessage(message)) if (Check()) Reflush();
            FilesList.Focus();
        }

        /**<summary>打开目标路径</summary>**/
        private void OpenDir(string path)
        {
            if (path[path.Length - 1] != '/') path += '/'; 
            LoadDir(Adb.OpenFilesList(FilesListData, path));
        }

        private void Reflush()
        {
            OpenDir(Adb.Path);
            FilesList.Items.Refresh();
        }

        /**<summary>打开选中的文件夹</summary>**/
        private void OpenSelectedDir()
        {
            if (CurrentFile == null) return;

            if (CurrentFile.Type.Equals(AFile.DIR) || CurrentFile.Type.Equals(AFile.LINK))
            {
                OpenDir($"{Adb.Path}{CurrentFile.Name}/");
            }
        }

        /***** 数据表事件 *****/

        /**<summary>取消列表选中或结束编辑状态</summary>**/
        private void CancelSelectOrEdit()
        {
            if (FilesNameCol.IsReadOnly) FilesList.SelectedIndex = -1;

            FilesList.CancelEdit();
        }

        /**<summary>打开选中的文件夹或结束编辑状态</summary>**/
        private void Enter_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (FilesNameCol.IsReadOnly) OpenSelectedDir(); 
            else FilesList.CancelEdit();
        }

        /**<summary>取消选中</summary>**/
        private void Esc_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!FilesNameCol.IsReadOnly) CurrentFile.Name = NewName = OldName;
            CancelSelectOrEdit();
        }

        /**<summary>刷新</summary>**/
        private void Reflush_Executed(object sender, ExecutedRoutedEventArgs e) => Reflush();

        /**<summary>选中内容发生变化</summary>**/
        private void FilesList_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            CurrentFile = FilesList.SelectedItem as AFile;
            FilesList.CurrentCell = new DataGridCellInfo(CurrentFile, FilesNameCol);
        }

        /**<summary>鼠标点击数据表</summary>**/
        private void FilesList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            FilesList.Focus();
            /* 获取鼠标点击的内容 */
            IInputElement CilckedControl = FilesList.InputHitTest(e.GetPosition(FilesList));
            /* 点击空白处则取消选中状态 */
            if (CilckedControl is ScrollViewer || CilckedControl is Border) CancelSelectOrEdit();

            switch (e.ChangedButton)
            {
                case MouseButton.XButton1: Last(); break;
                case MouseButton.XButton2: Next(); break;
                default: break;
            }
        }

        /* 鼠标左键单击数据行 */
        private DataGridCell SelectedCell = null;
        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
           if (sender is DataGridCell cell && e.ClickCount == 1)
            {
                if (cell.Column != FilesNameCol)
                {
                    FilesList.CancelEdit();
                    return;
                }
                if (SelectedCell == cell) BeginRename();
                else SelectedCell = cell;
            }
        }

        /**<summary>鼠标双击数据行</summary>**/
        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton.Equals(MouseButton.Left)) OpenSelectedDir();
        }

        /***** 导航事件 ******/

        /**<summary>上一级目录</summary>**/
        private void Upper()
        {
            if (Adb.Path.Length > 1) OpenDir(Adb.Path.Remove(Adb.Path.LastIndexOf('/', Adb.Path.Length - 2)) + "/"); 
        }
        private void Upper_Executed(object sender, ExecutedRoutedEventArgs e) => Upper();

        /**<summary>后退</summary>**/
        private void Last() => LoadDir(Adb.LastFileList(FilesListData));
        private void Last_Executed(object sender, ExecutedRoutedEventArgs e) => Last();

        /**<summary>前进</summary>**/
        private void Next() => LoadDir(Adb.NextFileList(FilesListData));
        private void Next_Executed(object sender, ExecutedRoutedEventArgs e) => Next();


        /***** 地址栏事件 ******/

        /**<summary>打开地址栏地址</summary>**/
        private void OpenPathDir_Executed(object sender, ExecutedRoutedEventArgs e) => OpenDir(PathBar.Text);

        /**<summary>取消对地址栏的编辑</summary>**/
        private void CancelPath_Executed(object sender, ExecutedRoutedEventArgs e) => FilesList.Focus();
        private void PathBar_LostFocus(object sender, RoutedEventArgs e) => PathBar.Text = Adb.Path;

        /**<summary>地址栏自动全选</summary>**/
        private void PathBar_GotFocus(object sender, RoutedEventArgs e) => PathBar.SelectAll();

        private void PathBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!PathBar.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                PathBar.Focus();
            }
        }

        /**<summary>对焦到地址栏</summary>**/
        private void FocusPath_Executed(object sender, ExecutedRoutedEventArgs e) => PathBar.Focus();


        /***** 编辑事件 ******/
        private bool IsNewFile = false;
        private AFile EditingFile = null;

        /**<summary>新建</summary>**/
        private void Preview_Create(string file_name, string type)
        {
            CancelSelectOrEdit();
            FilesList.Focus();

            /* 避免文件名冲突, 添加前缀 */
            while (Adb.CheckPath($"{Adb.Path}{file_name}")) file_name = $"{Properties.Resources.New}{file_name}";
            AFile new_file = new AFile { Name = file_name, Type = type };
            FilesListData.Add(new_file);
            IsNewFile = true;
            BeginRename(new_file);
        }

        private void CreateEnding()
        {
            /* 不允许空文件或文件名 */
            if (EditingFile == null)
            {
                FilesListData.Remove(EditingFile);
                return;
            }
            string error_message = EditingFile.Type.Equals(AFile.FILE) ? Adb.CreateFile(EditingFile.Name) : Adb.CreateDir(EditingFile.Name);
            if (!ShowErrMessage(error_message)) Reflush();
            else FilesListData.Remove(EditingFile);
        }

        /**<summary>新建文件夹</summary>**/
        private void CreateDir_Executed(object sender, ExecutedRoutedEventArgs e) => Preview_Create(Properties.Resources.NewDir, AFile.DIR);

        /**<summary>新建文件</summary>**/
        private void CreateFile_Executed(object sender, ExecutedRoutedEventArgs e) => Preview_Create(Properties.Resources.NewFile, AFile.FILE);

        /*** 重命名 ***/
        private string OldName, NewName;
        private void Rename(AFile file)
        {
            /* 不接受空文件 */
            if (file == null) return;
            /* 文件名不接受斜杠 */
            NewName = file.Name.Replace("\\", "").Replace("/", "");
            /* 不接受空文件名 */
            if (string.IsNullOrEmpty(NewName)) file.Name = NewName = OldName;
            /* 同名无需操作 */
            if (NewName == OldName) return;
            string error_message = Adb.Rename(NewName, OldName);
            if (!ShowErrMessage(error_message)) file.Name = NewName;
            else file.Name = OldName;
            /* 清空新文件名缓存 */
            NewName = null;
        }

        /**<summary>开始重命名</summary>**/
        private void BeginRename(AFile file = null)
        {
            if (file != null)
            {
                /* 选中文件 */
                FilesList.SelectedItem = file;
                /* 对焦单元格 */
                FilesList.CurrentCell = new DataGridCellInfo(file, FilesNameCol);
                /* 滚动至目标 */
                FilesList.ScrollIntoView(file);
            }
            FilesNameCol.IsReadOnly = false;
            FilesList.BeginEdit();
        }
        private void Rename_Executed(object sender, ExecutedRoutedEventArgs e) => BeginRename();

        /**<summary>记录重命名前状态</summary>**/
        private void FilesList_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is AFile file)
            {
                EditingFile = file;
                OldName = EditingFile.Name;
            }
        }

        private void FilesList_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            /* 避免重复提交 */
            if (FilesNameCol.IsReadOnly) return;

            FilesNameCol.IsReadOnly = true;
            if (IsNewFile) CreateEnding();
            else Rename(EditingFile);
            IsNewFile = false;
        }

        /**<summary>删除文件</summary>**/
        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            /* 确定删除对话框 */
            MessageBoxResult delete_confirm = MessageBox.Show ( Properties.Resources.Message_DeleteConfirm,
                                                                Properties.Resources.Warning,
                                                                MessageBoxButton.YesNo,
                                                                MessageBoxImage.Question);
            if(FilesList.SelectedItems.Cast<AFile>().ToList() is IList<AFile> files && delete_confirm == MessageBoxResult.Yes)
            {
                string error_message = Adb.Rm(files);
                ShowWarnMessage(error_message);
                Reflush();
            }
        }

    }
}
