﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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
            Activate();
            /* 多线程初始化, 以免界面无法显示 */
            Task.Factory.StartNew(() => Dispatcher.Invoke(Init));
        }

        private void Init()
        {
            if (Adb.CheckAdb())
            {
                /* 绑定数据源 */
                DevicesList.ItemsSource = DevicesListData;
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
        }

        /***** 刷新 *****/

        /**<summary>刷新设备列表</summary>**/
        private void Reflush() => Adb.GetDevicesList(DevicesListData);

        /**<summary>强制刷新设备列表</summary>**/
        bool IsReflushing = false;
        private void Force_Reflush_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (IsReflushing) return;
            IsReflushing = true;
            Cursor = Cursors.Wait;
            Dispatcher.Invoke(() =>
            {
                Adb.KillServer();
                Reflush();
                Cursor = Cursors.Arrow;
                IsReflushing = false;
            }, DispatcherPriority.ContextIdle);
        }

        /**<summary>直接刷新设备列表</summary>**/
        private void Reflush_Devices_List_Execute(object sender, ExecutedRoutedEventArgs e) => Reflush();

        /***** 添加设备 *****/

        /**<summary>添加设备事件</summary>**/
        private void Add_Device_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var add_device = new AddDevice();
            add_device.ShowDialog();
            Reflush();
        }

        /***** 选中设备 *****/
        private bool Opening = false;
        private void OpenDevice()
        {
            if (CurrentDevice == null || Opening) return;
            Opening = true;

            Dictionary<string, Configure.DeviceProp> DevicesProp = Configure.Configurer.conf.Device;
            Configure.DeviceProp DeviceProp;
            if (!DevicesProp.ContainsKey(CurrentDevice.UsbSerialNum))
            {
                DevicesProp.Add(CurrentDevice.UsbSerialNum, new Configure.DeviceProp { Mod = CurrentDevice.Model });
            }
            DeviceProp = DevicesProp[CurrentDevice.UsbSerialNum];
            if (!DeviceProp.Mod.Equals(CurrentDevice.Model))
            {
                DeviceProp.Mod = CurrentDevice.Model;
                DeviceProp.Path = "/";
            }
            Adb.ChangeDevice(CurrentDevice, DeviceProp.Path);

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
            Opening = false;
        }
        /**<summary>选中设备事件</summary>**/
        private void OpenDevice_Execute(object sender, ExecutedRoutedEventArgs e) => OpenDevice();

        /***** 断开连接 *****/
        private void Disconnect_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (CurrentDevice != null)
            {
                if (MessageBox.Show(Properties.Resources.Message_DisconnectConfirm,
                                    Properties.Resources.Tip,
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question)
                    == MessageBoxResult.No) return;

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
            if (CilckedControl is ScrollViewer || CilckedControl is Border) CancelSelect();
        }

        /**<summary>鼠标双击数据行事件</summary>**/
        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            /* 左键双击 */
            if (e.ChangedButton.Equals(MouseButton.Left)) OpenDevice();
        }
        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        /***** 菜单事件 *****/

        /**<summary>菜单展开</summary>**/
        private void Menu_Device_Opened(object sender, RoutedEventArgs e)
        {
            Menu_ForcedReflush.Visibility = Context_ForcedReflush.Visibility =
                Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ?
                Visibility.Visible :
                Visibility.Collapsed;
        }

        private void Menu_About_Click(object sender, RoutedEventArgs e) => new About().ShowDialog();
    }
}
