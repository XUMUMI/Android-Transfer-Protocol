using Android_Transfer_Protocol.Configure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Android_Transfer_Protocol
{
    /// <summary>
    /// FileManager.xaml 的交互逻辑
    /// </summary>
    public partial class FileManager : Window
    {
        private Dictionary<string, ColHeaderProp> ColHeaderConf;
        private Dictionary<string, ToolBarProp> ToolBarConf;
        private WindowProp FileManagerConf;

        private ObservableCollection<AFile> FileListData;
        private readonly Dictionary<string, AFile> SelectedFile = new Dictionary<string, AFile>();
        public FileManager()
        {
            InitializeComponent();
            InitConf();
            InitWindow();
            InitStatusConfig();
            InitFileListHeader();
            InitToolBar();
            /* 根据 Root 状态修改 UI */
            RootStatus();
            /* 绑定任务状态数据源 */
            TaskListBox.ItemsSource = TaskList.Keys;
        }

        private void InitConf()
        {
            ShowProp show = Configurer.conf.Show;
            ColHeaderConf = show.ColHeaderConf;
            ToolBarConf = show.ToolBarConf;
            if (!show.WindowConf.ContainsKey("FileManager")) show.WindowConf.Add("FileManager", new WindowProp());
            FileManagerConf = Configurer.conf.Show.WindowConf["FileManager"];
        }

        /* 设置标题 */
        private void RootStatus()
        {
            Title = $"{Properties.Resources.ATP} | {Adb.CurrentDevice.Model}";
            if (Adb.CheckRoot(Adb.CurrentDevice))
            {
                Title += $" | {Properties.Resources.Rooted}";
                Menu_Elevation.Visibility = Context_Elevation.Visibility = Visibility.Collapsed;
                Menu_Deauthorization.Visibility = Context_Deauthorization.Visibility = Visibility.Visible;
            }
            else
            {
                Menu_Elevation.Visibility = Context_Elevation.Visibility = Visibility.Visible;
                Menu_Deauthorization.Visibility = Context_Deauthorization.Visibility = Visibility.Collapsed;
            }
        }

        /* 状态栏 */
        readonly List<string> StatusList = new List<string> { null, };

        int StatusIndex = -1;
        private void SetStatus(int index = -1)
        {
            StatusIndex = index >= 0 && index < StatusList.Count ? index : (StatusIndex + 1) % StatusList.Count;
            try
            {
                Dispatcher.Invoke(() => StatusMessage.Text = StatusList[StatusIndex]);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Dispatcher.Invoke(() => StatusMessage.Text = StatusList[0]);
                _ = e;
            }
            TaskListBox.Dispatcher.Invoke(TaskListBox.Items.Refresh);
        }

        private void DefaultStatus(string status = null)
        {
            StatusList[0] = string.IsNullOrEmpty(status) ?
               $"{Properties.Resources.Sum}: {FileList.Items.Count}, " +
               $"{Properties.Resources.Selected}: {FileList.SelectedItems.Count}" :
               status;
            SetStatus(0);
        }

        private void AddStatus(string status)
        {
            StatusList.Add(status);
            SetStatus(StatusList.Count - 1);
        }

        private void RmStatus(string status) => StatusList.Remove(status);

        private void InitStatusConfig()
        {
            Adb.AddStatus = AddStatus;
            Adb.RmStatus = RmStatus;
            ForceReflush();
            DefaultStatus($"{Properties.Resources.Connect} {Properties.Resources.Success}");
            new DispatcherTimer(
                TimeSpan.FromSeconds(1),
                DispatcherPriority.Normal,
                (sender, e) => SetStatus(),
                Dispatcher.CurrentDispatcher)
            .Start();
        }

        /**<summary>检查连接状态</summary>**/
        private bool Check()
        {
            if (!Adb.CheckPath())
            {
                Stop();
                MessageBox.Show($"\"{Adb.CurrentDevice.Model}\" {Properties.Resources.ConnectFailed}",
                                Properties.Resources.Error,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
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
            if (ShowWarnMessage(message)) if (Check()) OpenDir(Adb.GetUpperDir(Adb.Path));

            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                FileList.ItemsSource = FileListData;
                if (SelectedFile.ContainsKey(Adb.Path)) Go2File(SelectedFile[Adb.Path]);
            }, DispatcherPriority.ContextIdle);

            FileList.Focus();
            DefaultStatus();
        }

        /**<summary>打开目标路径</summary>**/
        private void OpenDir(string path)
        {
            if (path[path.Length - 1] != '/') path += '/';
            LoadDir(Adb.OpenFilesList(ref FileListData, path));
        }

        /**<summary>刷新</summary>**/
        private void Reflush()
        {
            AddStatus($"{Properties.Resources.Reflushing}");
            FileList.Dispatcher.Invoke(() =>
            {
                FileList.CommitEdit();
                FileList.CancelEdit();
                OpenDir(Adb.Path);
            });
            RmStatus($"{Properties.Resources.Reflushing}");
        }

        /**<summary>刷新缓存</summary>**/
        private void ForceReflush()
        {
            Adb.FlushCache(Adb.Path);
            FileList.Dispatcher.Invoke(() => OpenDir(Adb.Path));
        }

        /**<summary>打开选中的文件夹</summary>**/
        private void OpenSelectedDir()
        {
            if (!SelectedFile.ContainsKey(Adb.Path)) return;

            if (SelectedFile[Adb.Path].Type.Equals(AFile.DIR) || SelectedFile[Adb.Path].Type.Equals(AFile.LINK))
            {
                OpenDir($"{Adb.Path}{SelectedFile[Adb.Path].Name}/");
            }
        }

        /***** 数据表事件 *****/

        /**<summary>初始化列表头</summary>**/
        private void InitFileListHeader()
        {
            foreach (DataGridColumn col in FileList.Columns)
            {
                string header = col.Header.ToString();
                if (ColHeaderConf.ContainsKey(header))
                {
                    DataGridColumnToggle(col, ColHeaderConf[header].Visible);
                    col.DisplayIndex = ColHeaderConf[header].index;
                    col.Width = new DataGridLength(ColHeaderConf[header].Width);
                }
            }
            DataGridColumnToggle(FileNameCol, true);
        }

        private void SaveFileListHeader()
        {
            foreach (DataGridColumn col in FileList.Columns)
            {
                string header = col.Header.ToString();
                if (ColHeaderConf.ContainsKey(header))
                {
                    ColHeaderConf[header].Visible = col.Visibility == Visibility.Visible;
                    ColHeaderConf[header].index = col.DisplayIndex;
                    ColHeaderConf[header].Width = col.Width.DisplayValue;
                }
                else
                {
                    ColHeaderConf.Add(header, new ColHeaderProp
                    {
                        index = col.DisplayIndex,
                        Visible = col.Visibility == Visibility.Visible,
                        Width = col.Width.DisplayValue
                });
                }
            }
        }

        /**<summary>取消列表选中或结束编辑状态</summary>**/
        private void CancelSelectOrEdit()
        {
            if (FileNameCol.IsReadOnly) FileList.SelectedIndex = -1;
            FileList.CancelEdit();
            FileNameCol.IsReadOnly = true;
        }

        /**<summary>打开选中的文件夹或结束编辑状态</summary>**/
        private void Enter_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (FileNameCol.IsReadOnly) OpenSelectedDir();
            else FileList.CancelEdit();
        }

        /**<summary>编辑上一个文件名</summary>**/
        private void EditLast_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (FileList.SelectedIndex <= 0) return;
            if (!FileNameCol.IsReadOnly)
            {
                --FileList.SelectedIndex;
                BeginRename();
            }
            else --FileList.SelectedIndex;
        }

        /**<summary>编辑下一个文件名</summary>**/
        private void EditNext_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!FileNameCol.IsReadOnly)
            {
                ++FileList.SelectedIndex;
                BeginRename();
            }
            else ++FileList.SelectedIndex;
        }

        /**<summary>跳转到文件所在位置</summary>**/
        private void Go2File(AFile file)
        {
            if (file == null) return;
            /* 选中文件 */
            FileList.SelectedItem = file;
            /* 对焦单元格 */
            FileList.CurrentCell = new DataGridCellInfo(file, FileNameCol);
            /* 滚动至目标 */
            FileList.ScrollIntoView(file);
        }

        /**<summary>刷新</summary>**/
        private void Reflush_Executed(object sender, ExecutedRoutedEventArgs e) => Reflush();

        private void ForceReflush_Executed(object sender, ExecutedRoutedEventArgs e) => ForceReflush();

        /**<summary>选中内容发生变化</summary>**/
        private void FileList_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (FileList.SelectedItem is AFile file)
            {
                SelectedFile[Adb.Path] = file;
                FileList.CurrentCell = new DataGridCellInfo(file, FileNameCol);
            }
            DefaultStatus();
        }

        /**<summary>鼠标点击数据表</summary>**/
        private void FileList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            FileList.Focus();
            /* 获取鼠标点击的内容 */
            IInputElement CilckedControl = FileList.InputHitTest(e.GetPosition(FileList));
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
                if (cell.Column != FileNameCol)
                {
                    FileList.CancelEdit();
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

        /***** 导航事件 *****/

        /**<summary>上一级目录</summary>**/
        private void Upper() => OpenDir(Adb.GetUpperDir(Adb.Path));
        private void Upper_Executed(object sender, ExecutedRoutedEventArgs e) => Upper();

        /**<summary>后退</summary>**/
        private void Last() => LoadDir(Adb.LastFileList(ref FileListData));
        private void Last_Executed(object sender, ExecutedRoutedEventArgs e) => Last();

        /**<summary>前进</summary>**/
        private void Next() => LoadDir(Adb.NextFileList(ref FileListData));
        private void Next_Executed(object sender, ExecutedRoutedEventArgs e) => Next();


        /***** 地址栏事件 *****/

        /**<summary>打开地址栏地址</summary>**/
        private void OpenPathDir_Executed(object sender, ExecutedRoutedEventArgs e) => OpenDir(PathBar.Text);

        /**<summary>取消对地址栏的编辑</summary>**/
        private void CancelPath_Executed(object sender, ExecutedRoutedEventArgs e) => FileList.Focus();
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


        /***** 编辑事件 *****/
        private bool IsNewFile = false;
        private AFile EditingFile = null;
        private bool IsMove = false;
        private readonly Dictionary<string, Task> TaskList = new Dictionary<string, Task>();

        /**<summary>新建</summary>**/
        private void Preview_Create(string file_name, string type)
        {
            AddStatus(Properties.Resources.Creating);
            if (!FileNameCol.IsReadOnly) return;
            FileList.Focus();
            /* 避免文件名冲突, 添加前缀 */
            while (Adb.CheckPath($"{Adb.Path}{file_name}")) file_name = $"{Properties.Resources.New}{file_name}";
            var new_file = new AFile { Name = file_name, Type = type };
            FileListData.Add(new_file);
            IsNewFile = true;
            BeginRename(new_file);
        }

        private void CreateEnding()
        {
            /* 不允许空文件或文件名 */
            if (EditingFile == null)
            {
                FileListData.Remove(EditingFile);
                return;
            }
            string error_message = EditingFile.Type.Equals(AFile.FILE) ? Adb.CreateFile(EditingFile.Name) : Adb.CreateDir(EditingFile.Name);
            if (ShowErrMessage(error_message)) FileListData.Remove(EditingFile);
            RmStatus(Properties.Resources.Creating);
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
            if (file == null || OldName == null) return;
            /* 文件名不接受斜杠 */
            NewName = file.Name;
            /* 不接受空文件名 */
            if (string.IsNullOrEmpty(NewName)) file.Name = NewName = OldName;
            /* 同名无需操作 */
            if (NewName == OldName) return;
            bool result = ShowErrMessage(Adb.Rename(NewName, OldName));
            /* 更新列表 */
            ForceReflush();
            string filename = result ? OldName : NewName;
            /* 选中被修改的文件 */
            FileList.SelectedItem = FileList.Items.Cast<AFile>().First(new_file => new_file.Name.ToString().Equals(filename));
            /* 清空新文件名缓存 */
            NewName = null;
        }

        /**<summary>开始重命名</summary>**/
        private void BeginRename(AFile file = null)
        {
            Go2File(file);
            FileNameCol.IsReadOnly = false;
            FileList.BeginEdit();
        }
        private void Rename_Executed(object sender, ExecutedRoutedEventArgs e) => BeginRename();

        /**<summary>记录重命名前状态</summary>**/
        private void FileList_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is AFile file)
            {
                EditingFile = file;
                OldName = EditingFile.Name;
            }
        }

        private void FileList_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            /* 避免重复提交 */
            if (FileNameCol.IsReadOnly) return;

            FileNameCol.IsReadOnly = true;
            EditingFile.Name = EditingFile.Name.Replace("/", string.Empty);
            if (IsNewFile) CreateEnding();
            else Rename(EditingFile);
            IsNewFile = false;
        }

        private void Sent2Clipboard(bool move)
        {
            if (FileList.SelectedItems.Cast<AFile>().ToList() is IList<AFile> files)
            {
                if (files.Count < 1) return;
                IsMove = move;
                Clipboard.Clear();
                Clipboard.SetData(DataFormats.UnicodeText, Adb.Files2String(files));
            }
        }

        private bool CoverTip(string file_name) => MessageBox.Show($"{Properties.Resources.Message_CoverFile}: {file_name}",
                                                                   Properties.Resources.Tip,
                                                                   MessageBoxButton.YesNo,
                                                                   MessageBoxImage.Question)
                                                == MessageBoxResult.Yes;

        /**<summary>剪切</summary>**/
        private void Cut() => Sent2Clipboard(true);
        private void Cut_Executed(object sender, ExecutedRoutedEventArgs e) => Cut();

        /**<summary>移动</summary>**/
        private void Move(string path)
        {
            string task_name = $"{Properties.Resources.Moving} {path.Replace("\n", ", ")}";
            if (TaskList.ContainsKey(task_name)) return;
            TaskList.Add(task_name, Task.Factory.StartNew(() =>
            {
                ShowErrMessage(Adb.Move(path, CoverTip, task_name));
                Reflush();
                TaskList.Remove(task_name);
            }));
        }

        /**<summary>复制</summary>**/
        private void Copy() => Sent2Clipboard(false);

        private void Copy_Executed(object sender, ExecutedRoutedEventArgs e) => Copy();

        private void Duplicate(string files)
        {
            string task_name = $"{Properties.Resources.Copying} {files.Replace("\n", ", ")}";
            if (TaskList.ContainsKey(task_name)) return;
            TaskList.Add(task_name, Task.Factory.StartNew(() =>
            {
                ShowErrMessage(Adb.Copy(Adb.Path, files, CoverTip, task_name));
                Reflush();
                TaskList.Remove(task_name);
            }));
        }

        private void Duplicate(IList<AFile> files)
        {
            string task_name = $"{Properties.Resources.Copying} {string.Join(", ", files.Select(file => file.Name))}";
            if (TaskList.ContainsKey(task_name)) return;
            TaskList.Add(task_name, Task.Factory.StartNew(() =>
            {
                ShowErrMessage(Adb.Copy(Adb.Path, files, task_name));
                Reflush();
                TaskList.Remove(task_name);
            }));
        }

        private void Duplicate_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (FileList.SelectedItems.Cast<AFile>().ToList() is IList<AFile> files)
            {
                if (files.Count < 1) return;
                Duplicate(files);
                Reflush();
            }
        }

        /**<summary>粘贴</summary>**/
        private void Paste_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (Clipboard.GetData(DataFormats.UnicodeText) is string old_path)
            {
                if (IsMove)
                {
                    Move(old_path);
                    Clipboard.Clear();
                }
                else Duplicate(old_path);
                IsMove = false;
            }
        }

        /*** 删除  ***/
        private bool DeleteTip(string files_name)
        {
            files_name = files_name.Remove(0, files_name.IndexOf('\n'));
            int count = files_name.Count(letter => letter == '\n');
            if (count > 10) files_name = $"{string.Join("\n", files_name.Split('\n').Take(11))}\n...";
            return MessageBox.Show($"{Properties.Resources.Message_DeleteConfirm} {Properties.Resources.Sum}: {count}\n" +
                            $"{files_name}",
                            Properties.Resources.Tip,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question)
                == MessageBoxResult.Yes;
        }

        /**<summary>删除文件</summary>**/
        private void Delete()
        {
            if (FileList.SelectedItems.Cast<AFile>().ToList() is IList<AFile> files)
            {
                if (files.Count < 1) return;
                string files_name = Adb.Files2String(files);
                if (!DeleteTip(files_name)) return;
                string task_name = $"{Properties.Resources.Deleting} {string.Join(", ", files.Select(file => file.Name))}";
                if (TaskList.ContainsKey(task_name)) return;
                TaskList.Add(task_name, Task.Factory.StartNew(() =>
                {
                    ShowWarnMessage(Adb.Delete(Adb.Path, files, task_name));
                    Reflush();
                    TaskList.Remove(task_name);
                }));
            }
        }

        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e) => Delete();

        /**<summary>上传</summary>**/
        private void Upload(string[] files)
        {
            string task_name = $"{Properties.Resources.Uploading} {string.Join(", ", files)}";
            if (TaskList.ContainsKey(task_name)) return;
            TaskList.Add(task_name, Task.Factory.StartNew(() =>
            {
                ShowWarnMessage(Adb.UpLoad(files, Adb.Path, CoverTip, task_name));
                Reflush();
                TaskList.Remove(task_name);
            }));
        }

        private void FileList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files) Upload(files);
        }

        private void Upload_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var files = new System.Windows.Forms.OpenFileDialog
            {
                Multiselect = true,
                Title = Properties.Resources.Upload
            };
            if (files.ShowDialog() == System.Windows.Forms.DialogResult.OK) Upload(files.FileNames);
        }

        /**<summary>下载</summary>**/
        private void Download(string path)
        {
            if (FileList.SelectedItems.Cast<AFile>().ToList() is IList<AFile> files)
            {
                string task_name = $"{Properties.Resources.Downloading} {string.Join(", ", files.Select(file => file.Name))}";
                if (TaskList.ContainsKey(task_name)) return;
                TaskList.Add(task_name, Task.Factory.StartNew(() =>
                {
                    ShowWarnMessage(Adb.Download(path, Adb.Path, files, CoverTip, task_name));
                    TaskList.Remove(task_name);
                }));
            }
        }

        private void Download_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (FileList.SelectedIndex < 0) return;
            var path = new System.Windows.Forms.FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                Description = Properties.Resources.Message_DownloadPath
            };
            if (path.ShowDialog() == System.Windows.Forms.DialogResult.OK) Download($"{path.SelectedPath}\\");
        }

        /*** 权限 ***/
        private void RootChange(Func<bool> Change)
        {
            if (Change())
            {
                RootStatus();
                Reflush();
            }
            else MessageBox.Show(Properties.Resources.Message_OperationFailed,
                                 Properties.Resources.Error,
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
        }

        private void Root_Executed(object sender, ExecutedRoutedEventArgs e) => RootChange(Adb.Root);

        private void Unroot_Executed(object sender, ExecutedRoutedEventArgs e) => RootChange(Adb.UnRoot);

        /***** 菜单 *****/

        /**<summary>全选</summary>**/
        private void SelectAll(object sender, RoutedEventArgs e) => FileList.SelectAll();

        /**<summary>取消选中</summary>**/
        private void CancelSelected(object sender, RoutedEventArgs e) => FileList.SelectedIndex = -1;


        /**<summary>断开连接</summary>**/
        private void Disconnect_Click(object sender, RoutedEventArgs e) => Close();

        /***** 显示 *****/
        private void Menu_Browse_Opened(object sender, RoutedEventArgs e)
        {
            Menu_ForcedReflush.Visibility = Context_ForcedReflush.Visibility =
                Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ?
                Visibility.Visible :
                Visibility.Collapsed;
        }

        /**<summary>切换文件属性显示状态</summary>**/
        private bool DataGridColumnToggle(DataGridColumn col, bool? sw = null)
        {
            if (col == null) return false;
            if (sw != null) col.Visibility = (bool)sw ? Visibility.Visible : Visibility.Collapsed;
            else col.Visibility = col.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            bool status = col.Visibility == Visibility.Visible;
            ContextMenu HeaderContextMenu = Resources["HeaderContextMenu"] as ContextMenu;
            MenuItem HeaderContextMenuItem = HeaderContextMenu.Items.Cast<MenuItem>().First(menu => menu.Header == col.Header);
            MenuItem HeaderMenuItem = HeaderMenu.Items.Cast<MenuItem>().First(menu => menu.Header == col.Header);
            HeaderContextMenuItem.IsChecked = HeaderMenuItem.IsChecked = status;
            return status;
        }

        private void HeaderToggle(object sender, RoutedEventArgs e)
        {
            DataGridColumn col = FileList.Columns.First(col => col.Header == (sender as MenuItem).Header);
            DataGridColumnToggle(col);
        }

        /**<summary>切换工具栏显示状态</summary>**/
        private bool ToolBarToogle(ToolBar obj, bool? sw = null)
        {
            if (obj == null) return false;
            if (sw != null) obj.Visibility = (bool)sw ? Visibility.Visible : Visibility.Collapsed;
            else obj.Visibility = obj.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            bool status = obj.Visibility == Visibility.Visible;
            ToolbarMenu.Items.Cast<MenuItem>().First(menu => menu.Name == obj.Name + "Menu").IsChecked = status;
            return obj.Visibility == Visibility.Visible;
        }

        /*** 工具栏显示 ***/

        private void InitToolBar()
        {
            foreach(ToolBar tool in ToolBarDock.ToolBars)
            {
                if (ToolBarConf.ContainsKey(tool.Name))
                {
                    tool.Band = ToolBarConf[tool.Name].Band;
                    tool.BandIndex = ToolBarConf[tool.Name].BandIndex;
                    ToolBarToogle(tool, ToolBarConf[tool.Name].Visible);
                }
            }
        }

        private void SaveToolBar()
        {
            foreach (ToolBar tool in ToolBarDock.ToolBars)
            {
                if (!ToolBarConf.ContainsKey(tool.Name)) ToolBarConf.Add(tool.Name, new ToolBarProp());
                ToolBarConf[tool.Name].Visible = tool.Visibility == Visibility.Visible;
                ToolBarConf[tool.Name].Band = tool.Band;
                ToolBarConf[tool.Name].BandIndex = tool.BandIndex;
            }
        }

        /* 箭头图片资源 */
        private readonly BitmapImage foldImg = new BitmapImage(new Uri("pack://application:,,,/Android Transfer Protocol;component/fold.png"));
        private readonly BitmapImage unfoldImg = new BitmapImage(new Uri("pack://application:,,,/Android Transfer Protocol;component/unfold.png"));
        private void TaskListToggle(object sender, RoutedEventArgs e)
        {
            bool status = TaskListBox.Visibility != Visibility.Visible;
            TaskListBox.Visibility = status ? Visibility.Visible : Visibility.Collapsed;
            UnfoldImg.Source = status ? foldImg : unfoldImg;
        }
        private void BrowseToolsToggle(object sender, RoutedEventArgs e) => ToolBarToogle(BrowseTools);
        private void TransmissionToolsToggle(object sender, RoutedEventArgs e) => ToolBarToogle(TransmissionTools);
        private void EditToolsToggle(object sender, RoutedEventArgs e) => ToolBarToogle(EditTools);

        /***** 窗口 *****/
        private void InitWindow()
        {
            Width = FileManagerConf.Width;
            Height = FileManagerConf.Height;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FileManagerConf.Width = Width;
            FileManagerConf.Height = Height;
        }

        /***** 停 *****/
        private void Stop(string task_name = null) => Adb.Stop(task_name);

        private void TaskListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Stop((sender as ListBox).SelectedItem.ToString());

        private void Esc_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!FileNameCol.IsReadOnly && SelectedFile.ContainsKey(Adb.Path)) SelectedFile[Adb.Path].Name = NewName = OldName;
            CancelSelectOrEdit();
        }

        private void Stop_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (MessageBox.Show(Properties.Resources.Message_StopTip,
                                Properties.Resources.Tip,
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question)
                == MessageBoxResult.Yes) Stop();
        }

        private bool ClosingRequest()
        {
            bool ret = true;
            if (TaskList.Count > 0)
            {
                if (MessageBox.Show($"{Properties.Resources.Message_ExitConfirm}({Properties.Resources.Sum} {TaskList.Count})",
                                    Properties.Resources.Tip,
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question)
                == MessageBoxResult.Yes) Stop();
                else ret = false;
            }
            return ret;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveFileListHeader();
            SaveToolBar();
            Configurer.conf.Device[Adb.CurrentDevice.UsbSerialNum].Path = Adb.Path;
            if (!ClosingRequest()) e.Cancel = true;
            else new DeviceChoose().Show();
        }
    }
}
