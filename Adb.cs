using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Threading;

namespace Android_Transfer_Protocol
{
    /**<summary>Adb 对接静态类</summary>**/
    internal static class Adb
    {
        public const int RESULT = 0, ERROR = 1;

        /***** 常量表 *****/
        private const string ADB = "adb\\adb.exe";
        private const string CONNECT = "connect";
        private const string CP = "cp -a";
        private const string DELETE = "rm -rf";
        private const string DISCONNECT = "disconnect";
        private const string GET_DEVICES = "devices -l";
        private const string GET_USER = "whoami";
        private const string SHELL = "shell";
        private const string KILL_SERVER = "kill-server";
        private const string LS = "ls -alh";
        private const string MKDIR = "mkdir -p";
        private const string MV = "mv";
        private const string PUSH = "push";
        private const string PULL = "pull";
        private const string REMOUNT = "remount";
        private const string ROOT = "root";
        private const string TOUCH = "touch";
        private const string UNROOT = "unroot";

        public static readonly string PATH_ERROR = Properties.Resources.PathError;

        private static readonly ProcessStartInfo AdbInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            FileName = ADB,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        private static readonly Dictionary<String, Process> ProcessList = new Dictionary<string, Process>();

        //private static readonly Process Command = new Process()
        //{
        //    StartInfo = AdbInfo
        //};

        /***** 运行函数 *****/


        /* 路径纪录 */
        public static string Path { get; private set; } = "/";
        private static readonly List<string> PathHistory = new List<string>();
        private static int Step = 0;

        /* 缓存 */
        private static readonly Dictionary<string, ObservableCollection<AFile>> FilesListCache = new Dictionary<string, ObservableCollection<AFile>>();

        /* 当前操作设备, 在执行运行函数前必须先指定 */
        public static Device CurrentDevice { get; private set; }
        private static int ColCount = 7;

        public static void ChangeDevice(Device device)
        {
            CurrentDevice = device ?? throw new ArgumentNullException(nameof(device));
            ColCount = new Regex("[\\s]+").Replace(Ls("/")[RESULT].Split('\n')[1], " ").Split(' ').Count();
            FlushCache();
            Path = "/";
            PathHistory.Clear();
            Step = 0;
        }

        /* 状态处理 action */
        public static Action<string> SetStatus { private get; set; } = null;
        private static void Status(string message) => SetStatus?.Invoke(message);

        /**<summary>执行 ADB 命令, 返回结果字符串和错误字符串</summary>**/
        private static string[] Exec(string command = "", string name = null)
        {
            var ret = new string[2];

            if (string.IsNullOrEmpty(name)) name = command;

            ProcessList.Add(name, new Process() { StartInfo = AdbInfo });
            ProcessList[name].StartInfo.Arguments = command;
            ProcessList[name].EnableRaisingEvents = true;
            ProcessList[name].Exited += (sender, e) => ProcessList.Remove(name);

            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                try
                {
                    ProcessList[name].Start();
                    ret[RESULT] = ProcessList[name]?.StandardOutput.ReadToEnd()?.Replace("\r", string.Empty);
                    ret[ERROR] = ProcessList[name]?.StandardError.ReadToEnd()?.Replace("\r", string.Empty);
                    ret[ERROR] = ret[ERROR].IndexOf(":") > 0 ? ret[ERROR].Substring(ret[ERROR].IndexOf(":") + 2) : ret[ERROR];
                    ProcessList[name].WaitForExit();
                }
                catch (InvalidOperationException e)
                {
                    _ = e;
                }
                catch (KeyNotFoundException e)
                {
                    _ = e;
                }
                catch (Exception)
                {
                    throw;
                }
            }, DispatcherPriority.Loaded);
            return ret;
        }

        /**<summary>执行 Shell 命令, 返回结果字符串和错误字符串</summary>**/
        private static string[] Shell(string command, string name = null) => Exec($"-s {CurrentDevice.UsbSerialNum} {SHELL} {command}", name);

        /**<summary>检测 ADB 是否错误</summary>**/
        public static bool CheckAdb() => string.IsNullOrEmpty(Exec()[ERROR]);

        /**<summary>提权</summary>**/
        public static bool Root()
        {
            if (CurrentDevice.Root) return CurrentDevice.Root;
            Exec($"-s {CurrentDevice.UsbSerialNum} {ROOT}");
            Exec($"-s {CurrentDevice.UsbSerialNum} {REMOUNT}");
            if(CurrentDevice.Root != CheckRoot(CurrentDevice))
            {
                CurrentDevice.Root = CheckRoot(CurrentDevice);
                FlushCache();
            }
            return CurrentDevice.Root;
        }

        /**<summary>降权</summary>**/
        public static bool UnRoot()
        {
            if (!CurrentDevice.Root) return !CurrentDevice.Root;
            Exec($"-s {CurrentDevice.UsbSerialNum} {UNROOT}");
            if (CurrentDevice.Root != CheckRoot(CurrentDevice))
            {
                CurrentDevice.Root = CheckRoot(CurrentDevice);
                FlushCache();
            }
            return !CurrentDevice.Root;
        }

        /***** 传输函数 *****/

        /**<summary>获取本地文件名</summary>**/
        private static string GetLoaclFileName(string path)
        {
            int file_name_index = path.LastIndexOf('\\') + 1;
            return file_name_index < path.Length ? path.Substring(path.LastIndexOf('\\') + 1) : path;
        }

        /**<summary>上传</summary>**/
        private static string[] Push(string remote_path, string local_path)
        {
            var timer = new Timer(1000);
            timer.Elapsed += (sender, e) => SetStatus($"↑{GetLoaclFileName(local_path)}");
            timer.Start();
            string[] ret = Exec($"-s {CurrentDevice.UsbSerialNum} {PUSH} \"{local_path}\" \"{remote_path}\"");
            timer.Stop();
            return ret;
        }

        public static string UpLoad(string[] local_paths_list, string remote_path, Func<string, bool> cover)
        {
            StopFlag = false;
            var ret = new List<string>();
            foreach (string local_path in local_paths_list)
            {
                if (StopFlag) break;
                string file_name = GetLoaclFileName(local_path);
                if (CheckPath($"{remote_path}{file_name}") && !cover(file_name)) continue;
                ret.Add(Push(remote_path + file_name, local_path)[RESULT]);
            }
            FlushCache(remote_path);
            ret.RemoveAll(message => string.IsNullOrEmpty(message));
            return string.Join("\n", ret);
        }

        /**<summary>下载</summary>**/
        private static string[] Pull(string local_path, string remote_path)
        {
            return Exec($"-s {CurrentDevice.UsbSerialNum} {PULL} {remote_path} {local_path}");
        }


        /***** 交互函数 *****/
        private static string[] ExecResult;

        /*** 获取设备列表 ***/
        private static Device DeviceCache;

        /**<summary>根据字符串返回设备状态</summary>**/
        private static string String2DeviceStatus(string status) => status switch
        {
            "device" => Device.CONNECTED,
            "offline" => Device.OFFLINE,
            "recovery" => Device.RECOVERY,
            "unauthorized" => Device.UNAUTHORIZED,
            _ => status,
        };

        /**<summary>字符串转设备类, 可能返回 null</summary>**/
        private static Device String2Device(string device)
        {
            DeviceCache = null;
            /* 空字符串直接返回 null */
            if (string.IsNullOrEmpty(device)) return DeviceCache;

            var DeviceBuf = new Regex("[\\s]+").Replace(device, " ").Split(' ');
            DeviceCache = new Device
            {
                UsbSerialNum = DeviceBuf[0],
                Model = DeviceBuf.Length > 3 ?
                    DeviceBuf[3].Replace("model:", string.Empty).Replace("_", " ") :
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
            string[] DevicesStringList = ExecResult[RESULT].Split('\n');
            /* 批量处理字符串并存入数据表 */
            foreach (string device in DevicesStringList.Skip(1))
            {
                if (String2Device(device) != null)
                {
                    DevicesList.Add(DeviceCache);
                    DeviceCache.Root = CheckRoot(DeviceCache);
                }
            }
            return ExecResult[ERROR];
        }

        /**<summary>连接新设备, 返回提示和错误信息</summary>**/
        public static string[] Connect(string address, uint port = 5555) => Exec($"{CONNECT} {address}:{port}");

        /**<summary>断开设备, 返回提示和错误</summary>**/
        public static string[] Disconnect(Device device) => Exec($"{DISCONNECT} {device.UsbSerialNum}");

        /*** 读取文件列表 ***/
        private static AFile FileCache;

        private static string[] Ls(string path) => Shell($"{LS} {FileNameEscape(path)}");

        /**<summary>检测路径是否存在</summary>**/
        public static bool CheckPath(string path = "/") => !string.IsNullOrEmpty(Ls(path)[RESULT]);

        /**<summary>检测识别是否获得 Root 权限</summary>**/
        public static bool CheckRoot(Device device) => Exec($"-s {device.UsbSerialNum} {SHELL} {GET_USER}")[RESULT].Replace("\n", string.Empty).Equals(ROOT);

        /**<summary>清除缓存</summary>**/
        public static void FlushCache(string path = null)
        {
            if (string.IsNullOrEmpty(path)) FilesListCache.Clear();
            else FilesListCache.Remove(path);
        }

        /**<summary>根据字符返回文件类型, 可能返回 null</summary>**/
        private static string Char2FileType(char e) => e switch
        {
            '-' => AFile.FILE,
            'd' => AFile.DIR,
            'c' => AFile.CHAR,
            'b' => AFile.BLOCK,
            's' => AFile.SOCKET,
            'l' => AFile.LINK,
            'p' => AFile.PIPE,
            _ => null,
        };

        /**<summary>字符串转文件类, 返回无法处理的字符串</summary>**/
        private static string String2Afile(string sfile)
        {
            FileCache = null;
            /* 空字符串直接返回 null */
            if (string.IsNullOrEmpty(sfile)) return null;
            /* 按照以空格作为切割依据 */
            var FileBuf = new Regex("\\s*\\S*").Matches(sfile).Cast<Match>().Select(file => file.Value).ToArray();
            int index = 0;
            /* 正常的文件返回格式第一个字段一定是 10 个字符 */
            if (FileBuf[index].Length == 10)
            {
                /* 不记录当前路径和上一层路径 */
                if (FileBuf[ColCount - 1].Trim().Equals(".") || 
                    FileBuf[ColCount - 1].Trim().Equals("..")) return null;

                /* 新建文件对象并初始化 类型、权限、用户和组 */
                FileCache = new AFile
                {
                    Type = Char2FileType(FileBuf[index][0]),
                    Pression = FileBuf[index++].Trim().Remove(0, 1),
                    User = FileBuf[++index].Trim(),
                    Group = FileBuf[++index].Trim()
                };

                /* 字符和块设备比其他文件多出一个设备号字段 */
                if (FileCache.Type.Equals(AFile.CHAR) || FileCache.Type.Equals(AFile.BLOCK))
                {
                    FileCache.DeviceNum = FileBuf[++index].Trim().Replace(",", string.Empty);
                }

                /* 大小 */
                FileCache.Size = FileBuf[++index].Trim();

                /* 日期 */
                int col = ColCount - index - 2;
                FileCache.DateTime = $"{FileBuf[++index].Trim()}";
                while (--col > 0) FileCache.DateTime += $"{FileBuf[++index]}";

                /* 文件名和链接地址可能带空格, 因此需要读取到结尾或链接符处 */
                FileCache.Name = FileBuf[++index].Remove(0, 1);
                if (FileCache.Type.Equals(AFile.LINK))
                {
                    for (++index; index < FileBuf.Length && FileBuf[index].Trim() != "->"; ++index) FileCache.Name += $"{FileBuf[index]}";
                    FileCache.Name = FileCache.Name;
                    FileCache.Link = FileBuf[++index].Remove(0, 1);
                    for (++index; index < FileBuf.Length; ++index) FileCache.Link += $"{FileBuf[index]}";
                }
                else for (++index; index < FileBuf.Length; ++index) FileCache.Name += $"{FileBuf[index]}";
            }
            else return sfile;
            return null;
        }

        /**<summary>错误字符串转文件类, 可能返回 null</summary>**/
        private static string ErrString2Afile(string efile)
        {
            FileCache = null;
            if (string.IsNullOrEmpty(efile)) return null;

            var name = new Regex("//.*?:").Match(efile).Value;
            if (!string.IsNullOrEmpty(name))
            {
                FileCache = new AFile
                {
                    Name = name.Replace("//", string.Empty).Replace(":", string.Empty),
                    Type = AFile.ERROR
                };
            }
            else return efile;
            return null;
        }

        /**<summary>传入一个文件列表和路径, 该函数可以对文件列表进行赋值, 如有错误将返回错误信息</summary>**/
        private static string GetFilesList(ref ObservableCollection<AFile> files_list, string path = "/")
        {
            Status($"{Properties.Resources.Reading} {path}");
            /* 是否存在缓存 */
            if (FilesListCache.ContainsKey(path))
            {
                Path = path;
                files_list = FilesListCache[path];
                return null;
            }
            /* 路径不能访问直接返回错误 */
            if (!CheckPath(path)) return PATH_ERROR;
            Path = path;
            files_list = FilesListCache[path] = new ObservableCollection<AFile>();
            /* 读取结果准备处理 */
            ExecResult = Ls(Path);

            var error_messages = new List<string>();
            string error_message;

            string[] FilesStringList = ExecResult[ERROR].Split('\n');
            foreach (string file in FilesStringList)
            {
                error_message = ErrString2Afile(file);
                if (FileCache != null) files_list.Add(FileCache);
                else if (error_message != null) error_messages.Add(file.Replace("\n", string.Empty));
            }

            FilesStringList = ExecResult[RESULT].Split('\n');
            foreach (string file in FilesStringList)
            {
                error_message = String2Afile(file);
                if (FileCache != null) files_list.Add(FileCache);
                else if (error_message != null)
                {
                    if (new Regex("total").IsMatch(error_message)) continue;
                    error_messages.Add(error_message.Substring(error_message.IndexOf(":") + 2).Replace("\n", string.Empty));
                }
            }

            error_messages.RemoveAll(message => string.IsNullOrEmpty(message));

            return string.Join("\n", error_messages);
        }

        /**<summary>清除 start_index 之后的历史路径</summary>**/
        private static void CleanNextHistory(int start_index) => PathHistory.RemoveRange(start_index, PathHistory.Count - start_index);

        /**<summary>打开文件列表, 传入一个已初始化的文件列表和路径, 该函数可以对文件列表进行赋值, 如有错误将返回错误信息</summary>**/
        public static string OpenFilesList(ref ObservableCollection<AFile> files_list, string path = "/")
        {
            string error_message = GetFilesList(ref files_list, path);
            if (!string.IsNullOrEmpty(error_message)) return error_message;

            if (PathHistory.Count == 0 || PathHistory[Step - 1] != path)
            {
                CleanNextHistory(Step++);
                PathHistory.Add(path);
            }
            return null;
        }

        /**<summary>获取上一个文件列表, 传入一个已初始化的文件列表, 该函数可以对文件列表进行赋值, 如有错误将返回错误信息</summary>**/
        public static string LastFileList(ref ObservableCollection<AFile> files_list)
        {
            string error_message = null;
            if (Step > 1)
            {
                error_message = GetFilesList(ref files_list, PathHistory[--Step - 1]);
                if (error_message != null)
                {
                    CleanNextHistory(Step - 1);
                    error_message = LastFileList(ref files_list);
                }
            }
            return error_message;
        }

        /**<summary>获取上一个文件列表, 传入一个已初始化的文件列表, 该函数可以对文件列表进行赋值, 如有错误将返回错误信息</summary>**/
        public static string NextFileList(ref ObservableCollection<AFile> files_list)
        {
            string error_message = null;
            if (Step < PathHistory.Count)
            {
                error_message = GetFilesList(ref files_list, PathHistory[Step++]);
                if (error_message != null)
                {
                    CleanNextHistory(--Step);
                    error_message = NextFileList(ref files_list);
                }
            }
            return error_message;
        }

        /**<summary>文件名特殊符号转义</summary>*/
        public static string FileNameEscape(string e)
        {
            e = e?
            .Replace("\\", "\\\\")
            .Replace(" ", "\\ ")
            .Replace("`", "\\`")
            .Replace("'", "\\\'")
            .Replace("\"", "\\\\\\\"")
            .Replace(";", "\\;")
            .Replace("?", "\\?")
            .Replace("*", "\\*")
            .Replace("&", "\\&")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace("|", "\\|");
            return $"\"{e}\"";
        }

        /**<summary>多文件名转换</summary>*/
        public static string Files2String(IList<AFile> files)
        {
            string files_name = Path;
            foreach (AFile file in files) files_name += $"\n{file.Name}";
            return files_name;
        }

        /**<summary>获取上一层目录地址</summary>*/
        public static string GetUpperDir(string path) => path.Length > 1 ? path.Remove(path.LastIndexOf('/', path.Length - 2)) + "/" : path;

        /**<summary>获取文件名</summary>*/
        public static string GetThisFileName(string path) => path.Length > 1 ? path.Remove(0, path.LastIndexOf('/', path.Length - 2)).Trim('/') : path;

        /***** 编辑函数 *****/
        private static bool StopFlag = false;

        /*** 新建文件(夹) ***/
        private static string Create(string command, string name)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(command)) throw new ArgumentException();
            name = Path + name;
            if (CheckPath(name)) return Properties.Resources.FileExist;
            return Shell($"{command} {FileNameEscape(name)}")[ERROR];
        }

        public static string CreateFile(string file_name) => Create(TOUCH, file_name);
        public static string CreateDir(string dir_name) => Create(MKDIR, dir_name);


        /**<summary>粘贴</summary>**/
        private static string Paste(string new_path, string old_path, string command)
        {
            if (string.IsNullOrEmpty(new_path) || string.IsNullOrEmpty(old_path)) throw new ArgumentException();
            if (!CheckPath(old_path)) return $"{Properties.Resources.InvalidPath}: {old_path}";
            old_path = FileNameEscape(old_path);
            new_path = FileNameEscape(new_path);
            return Shell($"{command} {old_path} {new_path}")[ERROR];
        }

        /*** 复制 ***/

        private static string Cp(string new_path, string old_path)
        {
            Status($"{old_path} >=> {new_path}");
            return Paste(new_path, old_path, CP);
        }

        /**<summary>生成一个副本名称</summary>**/
        private static string CopyRename(string path, string old_file_name)
        {
            int suffix_pos = old_file_name.LastIndexOf('.');
            if (suffix_pos < 1) suffix_pos = old_file_name.Length;
            string suffix = old_file_name.Substring(suffix_pos, old_file_name.Length - suffix_pos);
            string new_file_name = $"{old_file_name.Remove(suffix_pos, old_file_name.Length - suffix_pos)}_{Properties.Resources.Duplicate}";
            if (CheckPath($"{path}{new_file_name}{suffix}"))
            {
                int same_name_index = 2;
                while (CheckPath($"{path}{new_file_name}({same_name_index}){suffix}")) ++same_name_index;
                new_file_name = $"{new_file_name}({same_name_index}){suffix}";
            }
            else new_file_name = $"{new_file_name}{suffix}";
            return new_file_name;
        }

        
        /**<summary>通过剪贴板来源的复制</summary>**/
        public static string Copy(string new_path, string old_files, Func<string, bool> cover)
        {
            StopFlag = false;
            var ret = new List<string>();
            string[] old_files_list = old_files.Split('\n');
            string old_path = old_files_list[0];
            foreach (string old_file_name in old_files_list.Skip(1))
            {
                if (StopFlag) break;
                string new_file_name = old_path == new_path ? CopyRename(old_path, old_file_name) : old_file_name;
                if (CheckPath($"{new_path}{new_file_name}") && !cover(old_file_name)) continue;
                ret.Add(Cp($"{new_path}{new_file_name}", $"{old_path}{old_file_name}"));
            }
            FlushCache(new_path);
            ret.RemoveAll(message => string.IsNullOrEmpty(message));
            return string.Join("\n", ret);
        }

        /**<summary>同一目录下的副本</summary>**/
        public static string Copy(string path, IList<AFile> old_files)
        {
            StopFlag = false;
            var ret = new List<string>();
            foreach (AFile old_file in old_files)
            {
                if (StopFlag) break;
                ret.Add(Cp($"{path}{CopyRename(path, old_file.Name)}", $"{path}{old_file.Name}"));
            }
            FlushCache(path);
            ret.RemoveAll(message => string.IsNullOrEmpty(message));
            return string.Join("\n", ret);
        }

        /*** 移动及重命名 ***/

        private static string Mv(string new_path, string old_path)
        {
            Status($"{old_path} => {new_path}");
            /* 目标与源相同, 不需要任何操作 */
            if (new_path == old_path) return null;
            else return Paste(new_path, old_path, MV);
        }

        /**<summary>移动</summary>**/
        public static string Move(string old_files, Func<string, bool> cover)
        {
            StopFlag = false;
            var ret = new List<string>();
            string[] old_files_list = old_files.Split('\n');
            string old_path = old_files_list[0];
            foreach (string file_name in old_files_list.Skip(1))
            {
                if (StopFlag) break;
                if (CheckPath($"{Path}{file_name}") && !cover(file_name)) continue;
                ret.Add(Mv(Path, $"{old_path}{file_name}"));
            }
            FlushCache(old_path);
            FlushCache(Path);
            ret.RemoveAll(message => string.IsNullOrEmpty(message));
            return string.Join("\n", ret);
        }

        /**<summary>重命名</summary>**/
        public static string Rename(string new_name, string old_name)
        {
            if (CheckPath($"{Path}{new_name}")) return Properties.Resources.FileExist;
            else return Mv($"{Path}{new_name}", $"{Path}{old_name}");
        }

        /*** 删除 ***/
        public static string Delete(string path, IList<AFile> files)
        {
            StopFlag = false;
            var ret = new List<string>();
            foreach (AFile file in files)
            {
                if (StopFlag) break;
                Status($"{Properties.Resources.Deleting} {file.Name}");
                ret.Add(Shell($"{DELETE} {FileNameEscape($"{path}{file.Name}")}")[ERROR]?.Replace("\n", string.Empty));
                FlushCache($"{path}{file.Name}/");
            }
            FlushCache(path);
            ret.RemoveAll(message => string.IsNullOrEmpty(message));
            return string.Join("\n", ret);
        }

        /***** 停止函数 *****/

        /**<summary>停止 Adb 服务</summary>**/
        public static void KillServer() => Exec(KILL_SERVER);

        /**<summary>停止进程</summary>**/
        public static void Stop()
        {
            StopFlag = true;
            try
            {
                foreach (KeyValuePair<string, Process> process in ProcessList)
                {
                    process.Value.Kill();
                }
            }
            catch (InvalidOperationException e)
            {
                _ = e;
            }
        }
    }

    /* 设备类 */
    public class Device
    {
        /*** 常量表 ***/
        public static string CONNECTED { get; private set; } = Properties.Resources.CONNECTED;
        public static string OFFLINE { get; private set; } = Properties.Resources.OFFLINE;
        public static string RECOVERY { get; private set; } = Properties.Resources.RECOVERY;
        public static string UNAUTHORIZED { get; private set; } = Properties.Resources.UNAUTHORIZED;

        /*** 属性表 ***/
        public string UsbSerialNum { get; set; }
        public string Model { get; set; }
        public bool Root { get; set; }
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
