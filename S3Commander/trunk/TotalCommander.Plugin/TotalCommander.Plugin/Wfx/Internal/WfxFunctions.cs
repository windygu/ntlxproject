﻿using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using TotalCommander.Plugin.Utils;

namespace TotalCommander.Plugin.Wfx.Internal
{
    static class WfxFunctions
    {
        private static ITotalCommanderWfxPlugin Plugin
        {
            get { return PluginHolder.GetWfxPlugin(); }
        }

        private static object enumerator;


        public static Int32 FsInit(Int32 number, ProgressCallback progress, LogCallback log, RequestCallback request)
        {
            try
            {
                Plugin.Init(
                    new Progress(number, progress),
                    new Logger(number, log),
                    new Request(number, request)
                );
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return 0;
        }

        public static IntPtr FsFindFirst(string path, IntPtr pFindData)
        {
            var findData = new FindData();
            var result = false;
            try
            {
                result = Plugin.FindFirst(path, findData, out enumerator);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            findData.CopyTo(pFindData);
            return result ? IntPtr.Zero : PluginConst.INVALID_HANDLE_VALUE;
        }

        public static bool FsFindNext(IntPtr handle, IntPtr pFindData)
        {
            var findData = new FindData();
            var result = false;
            try
            {
                result = Plugin.FindNext(enumerator, findData);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            findData.CopyTo(pFindData);
            return result;
        }

        public static Int32 FsFindClose(IntPtr handle)
        {
            try
            {
                Plugin.FindClose(enumerator);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            enumerator = null;
            return 0;
        }

        public static void FsSetDefaultParams(IntPtr dps)
        {
            try
            {
                Plugin.SetDefaultParams(new DefaultParam(dps));
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
        }

        public static string FsGetDefRootName()
        {
            try
            {
                return PluginHolder.GetWfxPluginName();
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return null;
        }

        public static int FsExecuteFile(IntPtr mainWin, string remoteName, string verb)
        {
            var result = ExecuteResult.Error;
            try
            {
                result = Plugin.ExecuteFile(new MainWindow(mainWin), remoteName, verb);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return (int)result;
        }

        public static int FsGetFile(string remoteName, string localName, int copyFlags, IntPtr ri)
        {
            var result = FileOperationResult.NotSupported;
            try
            {
                result = Plugin.GetFile(remoteName, localName, (CopyFlags)copyFlags, new RemoteInfo(ri));
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return (int)result;
        }

        public static int FsPutFile(string localName, string remoteName, int copyFlags)
        {
            var result = FileOperationResult.NotSupported;
            try
            {
                result = Plugin.PutFile(localName, remoteName, (CopyFlags)copyFlags);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return (int)result;
        }

        public static int FsRenMovFile(string oldName, string newName, bool move, bool overWrite, IntPtr ri)
        {
            var result = FileOperationResult.NotSupported;
            try
            {
                result = Plugin.RenameMoveFile(oldName, newName, move, overWrite, new RemoteInfo(ri));
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return (int)result;
        }

        public static bool FsDeleteFile(string remoteName)
        {
            var result = false;
            try
            {
                result = Plugin.RemoveFile(remoteName);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return result;
        }

        public static bool FsMkDir(string path)
        {
            var result = false;
            try
            {
                result = Plugin.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return result;
        }

        public static bool FsRemoveDir(string remoteName)
        {
            var result = false;
            try
            {
                result = Plugin.RemoveDirectory(remoteName);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return result;
        }

        public static bool FsSetAttr(string remoteName, int newAttr)
        {
            var result = false;
            try
            {
                result = Plugin.SetAttributes(remoteName, (FileAttributes)newAttr);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return result;
        }

        public static bool FsSetTime(string remoteName, FILETIME creationTime, FILETIME lastAccessTime, FILETIME lastWriteTime)
        {
            var result = false;
            try
            {
                result = Plugin.SetTime(
                    remoteName,
                    DateTimeUtil.FromFileTime(creationTime),
                    DateTimeUtil.FromFileTime(lastAccessTime),
                    DateTimeUtil.FromFileTime(lastWriteTime)
                );
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return result;
        }

        public static bool FsDisconnect(string disconnectRoot)
        {
            var result = false;
            try
            {
                result = Plugin.Disconnect(disconnectRoot);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return result;
        }

        public static void FsStatusInfo(string remoteDir, int infoStartEnd, int infoOperation)
        {
            try
            {
                Plugin.StatusInfo(remoteDir, (StatusInfo)infoStartEnd, (StatusOperation)infoOperation);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
        }

        public static int FsExtractCustomIcon(string remoteName, int extractFlags, IntPtr iconHandle)
        {
            var result = ExtractIconResult.UseDefault;
            try
            {
                Icon icon = null;
                result = Plugin.ExtractCustomIcon(remoteName, (ExtractIconFlag)extractFlags, out icon);
                if (icon != null) iconHandle = icon.Handle;
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return (int)result;
        }

        public static int FsGetPreviewBitmap(string remoteName, int width, int height, IntPtr bitmapHandle)
        {
            var result = PreviewBitmapResult.None;
            try
            {
                Bitmap bitmap = null;
                result = Plugin.GetPreviewBitmap(remoteName, new Size(width, height), out bitmap);
                if (bitmap != null) bitmapHandle = bitmap.GetHbitmap();
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            return (int)result;
        }


        private static void ProcessError(Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }
    }
}
