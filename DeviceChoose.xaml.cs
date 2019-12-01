using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Android_Transfer_Protocol
{
    /// <summary>
    /// DeviceChoose.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceChoose : Window
    {
        /* 设备数据列表 */
        private readonly ObservableCollection<Device> DevicesListData = new ObservableCollection<Device>();
        private Device CurrentDevice = null;

        public DeviceChoose()
        {
            InitializeComponent();
            /* 委托 DeviceList 实现多线程初始化, 以免界面无法显示 */
            Task.Factory.StartNew(() => DevicesList.Dispatcher.Invoke(() =>
            {
                if (Adb.CheckAdb())
                {
                    /* 绑定数据源 */
                    DevicesList.ItemsSource = DevicesListData;
                    /* 刷新数据 */
                    Reflush();
                }
                else
                {
                    MessageBox.Show(Properties.Resources.AdbLose,
                                    Properties.Resources.SeriousError,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    Close();
                }
            }));
        }


        /***** 刷新 *****/

        /**<summary>刷新设备列表</summary>**/
        private void Reflush() => Adb.GetDevicesList(DevicesListData);

        /**<summary>强制刷新设备列表</summary>**/
        private void Force_Reflush_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            Adb.KillServer();
            Reflush();
        }

        /**<summary>直接刷新设备列表</summary>**/
        private void Reflush_Devices_List_Execute(object sender, ExecutedRoutedEventArgs e) => Reflush();


        /***** 添加设备 *****/

        /**<summary>添加设备事件</summary>**/
        private void Add_Device_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            new AddDevice().ShowDialog();
            Reflush();
        }


        /***** 选中设备 *****/

        private void OpenDevice()
        {
            if (CurrentDevice == null)
            {
                return;
            }

            Adb.Device = CurrentDevice;
            if (Adb.CheckPath())
            {
                new FileManager().Show();
                Close();
            }
            else
            {
                MessageBox.Show(Properties.Resources.ConnectFailed,
                                Properties.Resources.Error,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Reflush();
            }
        }
        /**<summary>选中设备事件</summary>**/
        private void OpenDevice_Execute(object sender, ExecutedRoutedEventArgs e) => OpenDevice();


        /***** 断开连接 *****/
        private void Disconnect_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (CurrentDevice != null)
            {
                string[] message = Adb.Disconnect(CurrentDevice);

                if (!string.IsNullOrEmpty(message[Adb.RESULT]))
                {
                    MessageBox.Show(message[Adb.RESULT],
                                    Properties.Resources.Tip,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }

                if (!string.IsNullOrEmpty(message[Adb.ERROR]))
                {
                    MessageBox.Show(Properties.Resources.Message_CanNoDisconnect,
                                    Properties.Resources.Error,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
                Reflush();
            }
        }

        /***** 数据表事件 *****/

        /**<summary>取消列表选中状态</summary>**/
        private void CancelSelect() => DevicesList.SelectedIndex = -1;

        /**<summary>选中内容发生变化</summary>**/
        private void DevicesList_SelectedCellsChanged(object sender, System.Windows.Controls.SelectedCellsChangedEventArgs e)
        {
            CurrentDevice = DevicesList.SelectedItem as Device;
        }

        /**<summary>鼠标点击数据表事件</summary>**/
        private void DevicesList_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DevicesList.Focus();
            /* 获取鼠标点击的内容 */
            IInputElement CilckedControl = DevicesList.InputHitTest(e.GetPosition(DevicesList));
            /* 点击空白处则取消选中状态 */
            if (CilckedControl is ScrollViewer || CilckedControl is Border)
            {
                CancelSelect();
            }
        }

        /**<summary>鼠标双击数据行事件</summary>**/
        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            /* 左键双击 */
            if (e.ChangedButton.Equals(MouseButton.Left))
            {
                OpenDevice();
            }
        }
    }
}
