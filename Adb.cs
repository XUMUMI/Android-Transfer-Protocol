using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Android_Transfer_Protocol
{
    /**<summary>Adb 对接静态类</summary>**/
    internal static class Adb
    {
        public const int RESULT = 0, ERROR = 1;

        /***** 常量表 *****/
        private static readonly string ADB = $"/C \"{Environment.CurrentDirectory}\\adb\\adb.exe\"";
        private const string CONNECT = "connect";
        private const string DELETE = "rm -rf";
        private const string DELETE_HEAD = "rm: ";
        private const string DISCONNECT = "disconnect";
        private const string GET_DEVICES = "devices -l";
        private const string SHELL = "shell";
        private const string SHELL_NAME = "cmd";
        private const string KILL_SERVER = "kill-server";
        private const string LS = "ls -alh";
        private const string MKDIR = "mkdir -p";
        private const string MKDIR_HEAD = "mkdir: ";
        private const string MV = "mv";
        private const string MV_HEAD = "mv: ";
        private const string TOUCH = "touch";
        private const string TOUCH_HEAD = "touch: ";

        public static readonly string PATH_ERROR = Properties.Resources.PathError;

        /* 调用 CMD 进程, 只创建一次 */
        private static readonly Process cmd = new Process
        {
            StartInfo =
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = SHELL_NAME,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                Arguments = ADB
            }
        };

        /***** 运行函数 *****/

        /* 当前操作设备, 在执行运行函数前必须先指定 */
        public static Device Device { get; set; }

        /**<summary>执行 ADB 命令, 返回结果字符串和错误字符串</summary>**/
        private static string[] Exec(string command = "")
        {
            cmd.StartInfo.Arguments = $"{ADB} {command}";
            cmd.Start();
            return new string[] { cmd.StandardOutput.ReadToEnd().Replace("\r", ""), cmd.StandardError.ReadToEnd() };
        }

        /**<summary>执行 Shell 命令, 返回结果字符串和错误字符串</summary>**/
        private static string[] Shell(string command) => Exec($"-s {Device.UsbSerialNum} {SHELL} {command}");

        /**<summary>检测 ADB 是否错误</summary>**/
        public static bool CheckAdb() => string.IsNullOrEmpty(Exec()[ERROR]);


        /***** 交互函数 *****/
        private static string[] ExecResult;

        /*** 获取设备列表 ***/
        private static Device DeviceCache;
        private static string[] DevicesStringList;
        private static string[] DeviceBuf;

        /**<summary>根据字符串返回设备状态</summary>**/
        private static string String2DeviceStatus(string e)
        {
            switch (e)
            {
                case "device": return Device.CONNECTED;
                case "offline": return Device.OFFLINE;
                case "unauthorized": return Device.UNAUTHORIZED;
                default: return e;
            }
        }

        /**<summary>字符串转设备类, 可能返回 null</summary>**/
        private static Device String2Device(string device)
        {
            DeviceCache = null;
            /* 空字符串直接返回 null */
            if (string.IsNullOrEmpty(device)) return DeviceCache;

            DeviceBuf = new Regex("[\\s]+").Replace(device, " ").Split(' ');
            DeviceCache = new Device
            {
                UsbSerialNum = DeviceBuf[0],
                Model = DeviceBuf.Length > 3 ?
                    DeviceBuf[3].Replace("model:", "").Replace("_", " ") :
                    Properties.Resources.Unknow,
                Status = String2DeviceStatus(DeviceBuf[1])
            };
            return DeviceCache;
        }

        /**<summary>传入一个已初始化的设备列表, 该函数可以对其进行赋值, 如有错误将返回错误信息</summary>**/
        public static string GetDevicesList(ObservableCollection<Device> DevicesList)
        {
            if (DevicesList is null) throw new ArgumentNullException(nameof(DevicesList));

            DevicesList.Clear();
            /* 读取设备准备处理 */
            ExecResult = Exec(GET_DEVICES);
            DevicesStringList = ExecResult[RESULT].Split('\n');
            /* 批量处理字符串并存入数据表 */
            foreach (string device in DevicesStringList.Skip(1))
            {
                if (String2Device(device) != null) DevicesList.Add(DeviceCache);
            }
            return ExecResult[ERROR];
        }

        /**<summary>连接新设备, 返回提示和错误信息</summary>**/
        public static string[] Connect(string address, uint port = 5555) => Exec($"{CONNECT} {address}:{port}");

        /**<summary>断开设备, 返回提示和错误</summary>**/
        public static string[] Disconnect(Device device) => Exec($"{DISCONNECT} {device.UsbSerialNum}");

        /*** 读取文件列表 ***/

        /* 路径纪录 */
        public static string Path { get; private set; } = "/";
        private static readonly List<string> PathHistory = new List<string>();
        private static int Step = 0;

        /* 缓存 */
        private static AFile FileCache;
        private static string[] FilesStringList;
        private static string[] FileBuf;

        private static string[] Ls(string path) => Shell($"{LS} '{path}'");

        /**<summary>检测路径是否存在</summary>**/
        public static bool CheckPath(string path = "/") => !string.IsNullOrEmpty(Ls(path)[RESULT]);

        /**<summary>根据字符返回文件类型, 可能返回 null</summary>**/
        private static string Char2FileType(char e)
        {
            switch (e)
            {
                case '-': return AFile.FILE;
                case 'd': return AFile.DIR;
                case 'c': return AFile.CHAR;
                case 'b': return AFile.BLOCK;
                case 's': return AFile.SOCKET;
                case 'l': return AFile.LINK;
                case 'p': return AFile.PIPE;
                default: return null;
            }
        }

        /**<summary>字符串转文件类, 可能返回 null</summary>**/
        private static AFile String2Afile(string sfile)
        {
            FileCache = null;
            /* 空字符串直接返回 null */
            if (string.IsNullOrEmpty(sfile))
            {
                return FileCache;
            }

            FileBuf = new Regex("[\\s]+").Replace(sfile, " ").Split(' ');
            int index = 0;
            /* 正常的文件返回格式第一个字段一定是 10 个字符 */
            if (FileBuf[index].Length == 10)
            {
                /* 不记录当前路径和上一层路径 */
                if (FileBuf[7].Equals(".") || FileBuf[7].Equals("..")) return FileCache;

                /* 新建文件对象并初始化 类型、权限、用户和组 */
                FileCache = new AFile
                {
                    Type = Char2FileType(FileBuf[index][0]),
                    Pression = FileBuf[index++].Remove(0, 1),
                    User = FileBuf[++index],
                    Group = FileBuf[++index]
                };

                /* 字符和块设备比其他文件多出一个设备号字段 */
                if (FileCache.Type == Properties.Resources.CHAR || FileCache.Type == Properties.Resources.BLOCK)
                {
                    FileCache.DeviceNum = FileBuf[++index].Replace(",", "");
                }

                /* 设置 大小、修改日期 */
                FileCache.Size = FileBuf[++index];
                FileCache.DateTime = $"{FileBuf[++index]} {FileBuf[++index]}";

                /* 文件名可能带空格, 因此需要读取到结尾或链接符处 */
                FileCache.Name = FileBuf[++index];
                for (++index; index < FileBuf.Length && FileBuf[index] != "->"; ++index)
                {
                    FileCache.Name += $" {FileBuf[index]}";
                }

                /* 链接地址可能带空格, 需要读到结尾 */
                if (FileCache.Type.Equals(AFile.LINK) && index < FileBuf.Length)
                {
                    FileCache.Link = FileBuf[++index];
                    for (++index; index < FileBuf.Length; ++index)
                    {
                        FileCache.Link += $" {FileBuf[index]}";
                    }
                }
            }
            return FileCache;
        }

        /**<summary>错误字符串转文件类, 可能返回 null</summary>**/
        private static AFile ErrString2Afile(string efile)
        {
            FileCache = null;
            if (string.IsNullOrEmpty(efile)) return FileCache;

            string name = new Regex("//.*?:").Match(efile).Value;
            if (!string.IsNullOrEmpty(name))
            {
                FileCache = new AFile
                {
                    Name = name.Replace("//", "").Replace(":", ""),
                    Type = AFile.ERROR
                };
            }
            return FileCache;
        }

        /**<summary>传入一个已初始化的文件列表和路径, 该函数可以对文件列表进行赋值, 如有错误将返回错误信息</summary>**/
        public static string GetFilesList(ObservableCollection<AFile> files_list, string path = "/")
        {
            if (files_list is null) throw new ArgumentNullException(nameof(files_list));
            /* 路径不能访问直接返回错误 */
            if (!CheckPath(path)) return PATH_ERROR;

            files_list.Clear();
            Path = path;
            /* 读取结果准备处理 */
            ExecResult = Ls(Path);
            /* 切割字符串存到列表 */

            /* 批量处理字符串并存入数据表 */
            FilesStringList = ExecResult[ERROR].Split('\n');
            foreach (string file in FilesStringList)
            {
                if (ErrString2Afile(file) != null) files_list.Add(FileCache);
            }

            FilesStringList = ExecResult[RESULT].Split('\n');
            foreach (string file in FilesStringList)
            {
                if (String2Afile(file) != null) files_list.Add(FileCache);
            }
            return null;
        }

        /**<summary>清除 start_index 之后的历史路径</summary>**/
        private static void CleanNextHistory(int start_index) => PathHistory.RemoveRange(start_index, PathHistory.Count - start_index);

        /**<summary>打开文件列表, 传入一个已初始化的文件列表和路径, 该函数可以对文件列表进行赋值, 如有错误将返回错误信息</summary>**/
        public static string OpenFilesList(ObservableCollection<AFile> files_list, string path = "/")
        {
            string error_message = GetFilesList(files_list, path);
            if (!string.IsNullOrEmpty(error_message)) return error_message;

            if (PathHistory.Count == 0 || PathHistory[Step - 1] != path)
            {
                CleanNextHistory(Step++);
                PathHistory.Add(path);
            }
            return null;
        }

        /**<summary>获取上一个文件列表, 传入一个已初始化的文件列表, 该函数可以对文件列表进行赋值, 如有错误将返回错误信息</summary>**/
        public static string LastFileList(ObservableCollection<AFile> files_list)
        {
            string error_message = null;
            if (Step > 1)
            {
                error_message = GetFilesList(files_list, PathHistory[--Step - 1]);
                if (error_message != null)
                {
                    CleanNextHistory(Step - 1);
                    error_message = LastFileList(files_list);
                }
            }
            return error_message;
        }

        /**<summary>获取上一个文件列表, 传入一个已初始化的文件列表, 该函数可以对文件列表进行赋值, 如有错误将返回错误信息</summary>**/
        public static string NextFileList(ObservableCollection<AFile> files_list)
        {
            string error_message = null;
            if (Step < PathHistory.Count)
            {
                error_message = GetFilesList(files_list, PathHistory[Step++]);
                if (error_message != null)
                {
                    CleanNextHistory(--Step);
                    error_message = NextFileList(files_list);
                }
            }
            return error_message;
        }


        /*** 新建文件(夹) ***/
        private static string Create(string command, string name)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(command)) throw new ArgumentException();
            name = Path + name;
            if (CheckPath(name)) return Properties.Resources.FileExist;
            return Shell($"{command} '{name}'")[ERROR];
        }

        public static string CreateFile(string file_name) => Create(TOUCH, file_name).Replace(TOUCH_HEAD, "");
        public static string CreateDir(string dir_name) => Create(MKDIR, dir_name).Replace(MKDIR_HEAD, "");


        /*** 重命名及移动 ***/

        private static string Mv(string new_path, string old_path)
        {
            if (string.IsNullOrEmpty(new_path) || string.IsNullOrEmpty(old_path)) throw new ArgumentException();
            /* 目标与源相同, 不需要任何操作 */
            if (new_path == old_path) return null;
            else return Shell($"{MV} '{old_path}' '{new_path}'")[ERROR].Replace(MV_HEAD, "");
        }

        /**<summary>重命名</summary>**/
        public static string Rename(string new_name, string old_name)
        {
            if (CheckPath(new_name)) return Properties.Resources.FileExist;
            else return Mv(Path + new_name, Path + old_name);
        }

        /*** 删除 ***/
        public static string Rm(IList<AFile> files)
        {
            string files_name = "";
            foreach (AFile file in files) files_name += $" \'{Path}{file.Name}\'";
            return Shell($"{DELETE}{files_name}")[ERROR].Replace(DELETE_HEAD, "");
        }

        /***** 回收函数 *****/

        /**<summary>停止 Adb 服务</summary>**/
        public static void KillServer() => Exec(KILL_SERVER);

        /**<summary>关闭进程</summary>**/
        public static void Exit()
        {
            cmd.WaitForExit();
            cmd.Close();
        }
    }

    /* 设备类 */
    public class Device
    {
        /*** 常量表 ***/
        public static string CONNECTED { get; private set; } = Properties.Resources.CONNECTED;
        public static string OFFLINE { get; private set; } = Properties.Resources.OFFLINE;
        public static string UNAUTHORIZED { get; private set; } = Properties.Resources.UNAUTHORIZED;

        /*** 属性表 ***/
        public string UsbSerialNum { get; set; }
        public string Model { get; set; }
        public string Status { get; set; }
    }

    /* 文件类 */
    internal class AFile
    {
        /*** 常量表 ***/
        public static string FILE { get; private set; } = Properties.Resources.FILE;
        public static string DIR { get; private set; } = Properties.Resources.DIR;
        public static string CHAR { get; private set; } = Properties.Resources.CHAR;
        public static string BLOCK { get; private set; } = Properties.Resources.BLOCK;
        public static string SOCKET { get; private set; } = Properties.Resources.SOCKET;
        public static string LINK { get; private set; } = Properties.Resources.LINK_FILE;
        public static string PIPE { get; private set; } = Properties.Resources.PIPE;
        public static string ERROR { get; private set; } = Properties.Resources.PermissionDenied;

        /*** 属性表 ***/
        public string Type { get; set; }
        public string Pression { get; set; }
        public string User { get; set; }
        public string Group { get; set; }
        public string DeviceNum { get; set; }
        public string Size { get; set; }
        public string DateTime { get; set; }
        public string Name { get; set; }
        public string Link { get; set; }
    }
}
