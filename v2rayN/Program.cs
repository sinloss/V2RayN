using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using v2rayN.Forms;

namespace v2rayN
{
    class Opt {
        public readonly Mode.Config config;

        public Opt(Mode.Config config) {
            this.config = config;
        }
    }
    static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Opt opt = Parse(args);

            Process instance = RunningInstance();
            if (instance == null)
            {
                Utils.SaveLog("`v2rayN` starting up");

                //设置语言环境
                string lang = Utils.RegReadValue(Global.MyRegPath, Global.MyRegKeyLanguage, "zh-Hans");
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(opt != null ? opt.config : null));
            }
            else if (opt != null)
            {
                HttpProxyHandler.ActionClient.Start();
                HttpProxyHandler.ActionClient.Svr.Switch(opt.config);
                HttpProxyHandler.ActionClient.Ctx.Close();
                System.Environment.Exit(System.Environment.ExitCode);
            } else {
                UI.Show("An instance of `v2rayN` exists");
            }
        }

        private static Opt Parse(string[] args) {
            if (args.Length >= 1)
            {
                AttachConsole(-1);
                switch (args[0])
                {
                    case "--switch":
                        Mode.Config config = Handler.ConfigHandler.lazyLoadedConfig();
                        try
                        {
                            config.listenerType = args.Length >= 2 ? int.Parse(args[1]) : -1;
                        }
                        catch (FormatException)
                        {
                            Console.Write("Expecting a number yet got ");
                            ColorWrite(args[1], ConsoleColor.Red, true);
                            System.Environment.Exit(1);
                        }
                        return new Opt(config);
                    default:
                        Console.Write("Unkown option ");
                        ColorWrite(args[0], ConsoleColor.Red, true);
                        System.Environment.Exit(1);
                        break;
                }
            }
            return null;
        }

        private static void ColorWrite(String text, ConsoleColor color, Boolean ln = false) {
            ConsoleColor cc = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (ln)
            {
                Console.WriteLine(text);
                System.Windows.Forms.SendKeys.SendWait("{ENTER}");
            }
            else {
                Console.Write(text);
            }
            Console.ForegroundColor = cc;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string resourceName = "v2rayN." + new AssemblyName(args.Name).Name + ".dll";
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    byte[] assemblyData = new byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary> 
        /// 获取正在运行的实例，没有运行的实例返回null; 
        /// </summary> 
        public static Process RunningInstance()
        {
            Process current = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(current.ProcessName);
            foreach (Process process in processes)
            {
                if (process.Id != current.Id)
                {
                    if (Assembly.GetExecutingAssembly().Location.Replace("/", "\\") == process.MainModule.FileName)
                    {
                        return process;
                    }
                }
            }
            return null;
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Utils.SaveLog("Application_ThreadException", e.Exception);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Utils.SaveLog("CurrentDomain_UnhandledException", (Exception)e.ExceptionObject);
        }

    }
}
