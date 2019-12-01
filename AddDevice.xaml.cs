using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace Android_Transfer_Protocol
{
    /// <summary>
    /// AddDevice.xaml 的交互逻辑
    /// </summary>
    public partial class AddDevice : Window
    {
        public AddDevice()
        {
            InitializeComponent();
        }

        /**<summary>提交数据</summary>**/
        private void Submit()
        {
            string message;
            string address = Address_TextBox.Text;
            if (string.IsNullOrEmpty(address))
            {
                message = Properties.Resources.Message_NoAddressTip;
            }
            else if (string.IsNullOrEmpty(Port_TextBox.Text))
            {
                message = Adb.Connect(address)[Adb.RESULT];
            }
            else
            {
                uint port = Convert.ToUInt16(Port_TextBox.Text);
                message = Adb.Connect(address, port)[Adb.RESULT];
            }
            /* 如果连接成功直接关闭窗口 */
            if (string.IsNullOrEmpty(message) || new Regex("^already+").IsMatch(message) || new Regex("^connected+").IsMatch(message))
            {
                Close();
            }
            else
            {
                MessageBox.Show(message, Properties.Resources.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Focus_Port_Executed(object sender, ExecutedRoutedEventArgs e) => Port_TextBox.Focus();

        private void Submit_Executed(object sender, ExecutedRoutedEventArgs e) => Submit();

        /**<summary>限制端口输入内容</summary>**/
        private void Port_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        /***** 按钮 *****/
        private void OK_Click(object sender, RoutedEventArgs e) => Submit();

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
