/* Copyright (C) 2014-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Utilities;

namespace SMBLibrary.Authentication.Win32
{
    public enum LogonType
    {
        Interactive = 2, // LOGON32_LOGON_INTERACTIVE
        Network = 3,     // LOGON32_LOGON_NETWORK
        Service = 5,     // LOGON32_LOGON_SERVICE
    }

    public class LoginAPI
    {
        private const int LOGON32_PROVIDER_WINNT40 = 2;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LogonUser(
            string lpszUsername,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken
            );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        public static bool ValidateUserPassword(string userName, string password, LogonType logonType)
        {
            IntPtr token;
            bool success = LogonUser(userName, String.Empty, password, (int)logonType, LOGON32_PROVIDER_WINNT40, out token);
            if (!success)
            {
                uint error = (uint)Marshal.GetLastWin32Error();
                if (error == (uint)Win32Error.ERROR_ACCOUNT_RESTRICTION ||
                    error == (uint)Win32Error.ERROR_ACCOUNT_DISABLED ||
                    error == (uint)Win32Error.ERROR_LOGON_FAILURE ||
                    error == (uint)Win32Error.ERROR_LOGON_TYPE_NOT_GRANTED)
                {
                    return false;
                }
                throw new Exception("ValidateUser failed, error: 0x" + ((uint)error).ToString("X"));
            }
            CloseHandle(token);
            return success;
        }

        public static bool HasEmptyPassword(string userName)
        {
            IntPtr token;
            bool success = LogonUser(userName, String.Empty, String.Empty, (int)LogonType.Network, LOGON32_PROVIDER_WINNT40, out token);
            if (success)
            {
                CloseHandle(token);
                return true;
            }
            else
            {
                uint error = (uint)Marshal.GetLastWin32Error();
                return (error == (uint)Win32Error.ERROR_ACCOUNT_RESTRICTION ||
                        error == (uint)Win32Error.ERROR_ACCOUNT_DISABLED ||
                        error == (uint)Win32Error.ERROR_LOGON_TYPE_NOT_GRANTED);
            }
        }
    }
}
