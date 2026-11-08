using System;
using System.Runtime.InteropServices;

namespace CourseCapture
{
    public static class PowerPointCom
    {
        private static string _progId;
        private static DateTime _lastFailure = DateTime.MinValue;

        public static bool TryGetInfo(out int slideNumber, out string fullName, out bool presenting)
        {
            slideNumber = 0;
            fullName = null;
            presenting = false;

            if ((DateTime.Now - _lastFailure).TotalSeconds < 20) return false;

            string[] progIds = new string[] { "PowerPoint.Application", "KWPP.Application", "WPP.Application" };
            foreach (string progId in progIds)
            {
                object app = null;
                object windows = null;
                object window = null;
                object view = null;
                object presentation = null;
                try
                {
                    app = Marshal.GetActiveObject(progId);
                    if (app == null) continue;

                    dynamic dapp = app;
                    try
                    {
                        windows = dapp.SlideShowWindows;
                        dynamic dwindows = windows;
                        int count = Convert.ToInt32(dwindows.Count);
                        if (count > 0)
                        {
                            window = dwindows[1];
                            dynamic dwindow = window;
                            try
                            {
                                view = dwindow.View;
                                dynamic dview = view;
                                slideNumber = Convert.ToInt32(dview.CurrentShowPosition);
                                presenting = slideNumber > 0;
                            }
                            catch { }
                            try
                            {
                                presentation = dwindow.Presentation;
                                dynamic dpres = presentation;
                                fullName = Convert.ToString(dpres.FullName);
                            }
                            catch { }
                        }
                    }
                    catch { }

                    if (string.IsNullOrEmpty(fullName))
                    {
                        try
                        {
                            presentation = dapp.ActivePresentation;
                            dynamic dpres = presentation;
                            fullName = Convert.ToString(dpres.FullName);
                        }
                        catch { }
                    }

                    _progId = progId;
                    if (slideNumber > 0 || !string.IsNullOrEmpty(fullName)) return true;
                }
                catch (Exception ex)
                {
                    if (!(ex is COMException))
                    {
                        // 动态绑定失败不需要反复写日志。
                    }
                }
                finally
                {
                    Release(view);
                    Release(window);
                    Release(windows);
                    Release(presentation);
                    Release(app);
                }
            }

            _lastFailure = DateTime.Now;
            return false;
        }

        private static void Release(object obj)
        {
            if (obj == null) return;
            try
            {
                if (Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
            }
            catch { }
        }
    }
}
