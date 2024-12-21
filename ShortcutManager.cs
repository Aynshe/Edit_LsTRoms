using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Edit_LsTRoms
{
    [SupportedOSPlatform("windows")]
    public class ShortcutManager
    {
        public static void CreateShortcut(string targetPath, string shortcutPath)
        {
            ArgumentNullException.ThrowIfNull(targetPath);
            ArgumentNullException.ThrowIfNull(shortcutPath);

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException("Could not create WScript.Shell COM object");
            }

            dynamic shell = Activator.CreateInstance(shellType) 
                ?? throw new InvalidOperationException("Failed to create shell instance");
            
            try 
            {
                var shortcut = shell.CreateShortcut(shortcutPath);
                if (shortcut == null)
                {
                    throw new InvalidOperationException("Failed to create shortcut");
                }
                shortcut.TargetPath = targetPath;
                shortcut.Save();
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
} 