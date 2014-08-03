using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.IO;

namespace CASClonerDemo.Core
{
    public class TS4Registry
    {
        /// <summary>
        /// Returns the CAS Demo directory from registry. If the registry doesn't exit, returns null.
        /// </summary>
        public static string CASDemoPath
        {
            get
            {
                RegistryKey rk = Registry.LocalMachine.OpenSubKey("SOFTWARE" + (Environment.Is64BitOperatingSystem ? @"\Wow6432Node" : "") + @"\Maxis\The Sims 4 Create A Sim Demo", false);
                return rk.GetValue("Install Dir") as string;
            }
        }

        /// <summary>
        /// Return the CAS Demo FullBuild.package path. If the file doesn't exit, returns null.
        /// </summary>
        public static string CASDemoFullBuildPath
        {
            get
            {
                var rootPath = CASDemoPath;
                var possiblePath = (string.IsNullOrEmpty(rootPath)) ? null : Path.Combine(rootPath, @"Data\Client\CASDemoFullBuild.package");
                return File.Exists(possiblePath) ? possiblePath : null;
            }
        }

        /// <summary>
        /// Return the CAS Demo Thumbnail.package. If the file doesn't exit, returns null.
        /// </summary>
        public static string CASDemoThumPath
        {
            get
            {
                var rootPath = CASDemoPath;
                var possiblePath = (string.IsNullOrEmpty(rootPath)) ? null : Path.Combine(rootPath, @"Data\Client\CASDemoThumbnails.package");
                return File.Exists(possiblePath) ? possiblePath : null;
            }
        }
    }
}
